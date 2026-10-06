using System.Collections.Immutable;
using System.Text.Json;
using EntraAdvisor.Engine;
using EntraAdvisor.Engine.Contracts;

namespace EntraAdvisor.Guide;

public sealed record ValidationProbe(string Id, string Label, TokenAcquisition? Flow, CredentialMechanism Credential, bool BrowserSpa, bool SignInOnly);
public sealed record ValidationScript(string FileName, string Content);
public enum ValidationScriptFormat { CurlBash, CurlWindows, PowerShell }

/// <summary>Local diagnostic exports. No tenant values or credentials are collected by the advisor.</summary>
public sealed class ValidationScriptExporter
{
    public ValidationScript ExportAll(OAuthPlan plan, ValidationScriptFormat format)
    {
        var bash = format == ValidationScriptFormat.CurlBash;
        var output = new System.Text.StringBuilder(bash ? "#!/usr/bin/env bash\n" : "#requires -Version 7.2\n");
        output.AppendLine("# All connections for this scenario. Edit the settings in EACH section before running.");
        if (format == ValidationScriptFormat.CurlWindows) output.AppendLine("# Windows curl.exe, hosted by PowerShell 7.2+ for JSON, PKCE and certificate signing.");
        var index = 0;
        foreach (var probe in Probes(plan))
        {
            index++;
            var script = Export(plan, probe.Id, !bash).Content;
            output.AppendLine($"\n# ===== Scenario connection {index}: {JsonSerializer.Serialize(probe.Label)} =====");
            if (bash)
            {
                output.AppendLine("(");
                output.AppendLine("# Settings for this connection (use its caller ID and target scopes/audience).");
                output.AppendLine("TENANT_ID='<tenant-guid>'\nCLIENT_ID='<caller-client-guid>'\nSCOPE='<target-scopes>'\nRESOURCE_URI='https://<read-only-endpoint>'");
                output.AppendLine("# Set certificate/token paths for this connection as needed; do not reuse a token for another audience.");
                output.AppendLine("CERTIFICATE_PEM='<certificate.pem>'\nPRIVATE_KEY_PEM='<private-key.pem>'\nCLIENT_ASSERTION_FILE='<assertion-file>'\nINCOMING_TOKEN_FILE='<incoming-user-token-file>'\nACCESS_TOKEN_FILE='<host-access-token-file>'\nREDIRECT_URI='http://localhost:8400'\nORIGIN='http://localhost:8400'");
                output.AppendLine(script);
                output.AppendLine(")\nif [ $? -ne 0 ]; then exit 1; fi");
            }
            else
            {
                // A function gives every connection its own parameters, token response and sign-in return scope.
                var start = script.IndexOf(PowerShellParameters, StringComparison.Ordinal);
                var body = script[(start + PowerShellParameters.Length)..];
                if (format == ValidationScriptFormat.CurlWindows)
                    body = CurlWindowsHelpers + "\n" + body.Replace("Invoke-RestMethod", "Invoke-CurlRestMethod", StringComparison.Ordinal)
                        .Replace("Invoke-WebRequest", "Invoke-CurlWebRequest", StringComparison.Ordinal);
                output.AppendLine(script["#requires -Version 7.2\n".Length..start]);
                output.AppendLine($"function Test-Connection{index} {{\n{PowerShellParameters}\n{body}\n}}");
                output.AppendLine($"Test-Connection{index} -TenantId '<tenant-guid>' -ClientId '<caller-client-guid>' -Scope '<target-scopes>' -ResourceUri 'https://<read-only-endpoint>' `");
                output.AppendLine("    -CertificateThumbprint '<certificate-thumbprint>' -ClientAssertionFile '<assertion-file>' -IncomingTokenFile '<incoming-user-token-file>' -AccessTokenFile '<host-access-token-file>'");
            }
        }
        return new(bash ? "validate-authentication.sh" : format == ValidationScriptFormat.CurlWindows ? "validate-authentication-curl-windows.ps1" : "validate-authentication.ps1", output.ToString());
    }

    private const string CurlWindowsHelpers = """
        function Invoke-CurlRestMethod {
            param($Method, $Uri, $Body, $Headers = @{})
            $argsList = @('--silent', '--show-error', '--request', $Method, '--url', $Uri)
            foreach ($entry in $Headers.GetEnumerator()) { $argsList += @('--header', ($entry.Key + ': ' + $entry.Value)) }
            $argsList += @('--header', 'Content-Type: application/x-www-form-urlencoded')
            $encoded = ($Body.GetEnumerator() | ForEach-Object { [Uri]::EscapeDataString($_.Key) + '=' + [Uri]::EscapeDataString([string]$_.Value) }) -join '&'
            # Send credentials/assertions through stdin, never command-line arguments.
            $json = $encoded | & curl.exe @argsList --data-binary '@-'
            if ($LASTEXITCODE -ne 0) { throw 'curl transport failed.' }
            $response = ($json -join "`n") | ConvertFrom-Json
            if ($response.error) {
                $exception = [Exception]::new('Token request failed.')
                $record = [System.Management.Automation.ErrorRecord]::new($exception, 'OAuthError', [System.Management.Automation.ErrorCategory]::AuthenticationError, $null)
                $record.ErrorDetails = [System.Management.Automation.ErrorDetails]::new(($response | ConvertTo-Json -Compress))
                throw $record
            }
            return $response
        }
        function Invoke-CurlWebRequest {
            param($Method, $Uri, $Headers = @{}, $MaximumRedirection, [switch]$SkipHttpErrorCheck)
            $argsList = @('--silent', '--show-error', '--request', $Method, '--url', $Uri, '--output', 'NUL', '--write-out', '%{http_code}')
            $headerText = ($Headers.GetEnumerator() | ForEach-Object { $_.Key + ': ' + $_.Value }) -join "`n"
            $code = $headerText | & curl.exe @argsList --header '@-'
            if ($LASTEXITCODE -ne 0) { throw 'curl transport failed.' }
            return [pscustomobject]@{StatusCode=[int]($code -join '')}
        }
        """;

    public ImmutableArray<ValidationProbe> Probes(OAuthPlan plan)
    {
        Validate(plan);
        var probes = plan.Relationships.OrderBy(h=>h.Acquisition == TokenAcquisition.OnBehalfOf ? 1 : 0).Select(h => {
            var relationship=plan.Scenario.Relationships.Single(r=>r.Id==h.RelationshipId);
            var caller=plan.Scenario.Components.Single(c=>c.Id==relationship.CallerComponentId);
            var resource=plan.Scenario.Resources.Single(r=>r.Id==relationship.TargetResourceId);
            return new ValidationProbe(h.RelationshipId, caller.Name+" → "+resource.Name, h.Acquisition, h.Credential,
                caller.Stack.Value is ImplementationStack.BlazorWebAssembly or ImplementationStack.JavaScriptTypeScript, false);
        }).ToImmutableArray();
        if (!probes.IsEmpty) return probes;
        return plan.Components.Select(c=>new ValidationProbe(c.ComponentId, plan.Scenario.Components.Single(s=>s.Id==c.ComponentId).Name,
            c.SignIn == SignInApproach.None ? null : c.SignIn == SignInApproach.DeviceCode ? TokenAcquisition.DeviceCode : TokenAcquisition.AuthorizationCode,
            c.Credential, plan.Scenario.Components.Single(s=>s.Id==c.ComponentId).Stack.Value is ImplementationStack.BlazorWebAssembly or ImplementationStack.JavaScriptTypeScript,
            c.SignIn != SignInApproach.None)).ToImmutableArray();
    }

    public ValidationScript Export(OAuthPlan plan, string probeId, bool powershell)
    {
        var probe=Probes(plan).Single(p=>p.Id==probeId);
        var mode=probe.Credential == CredentialMechanism.ManagedIdentity || probe.Flow is null ? "token" : probe.Flow switch {
            TokenAcquisition.ClientCredentials=>"application", TokenAcquisition.OnBehalfOf=>"obo", TokenAcquisition.DeviceCode=>"device", _=>"code"
        };
        var parameters=$"# Plan: {plan.Id}\n# Probe: {JsonSerializer.Serialize(probe.Label)}\n# Export version: validation-1.0.0\n";
        var instructions="""
            # Run locally after completing registration, permissions/consent and credential setup.
            # This probe requests tokens and sends GET requests only. Choose a read-only resource URL.
            # It never creates registrations, grants permissions or changes tenant configuration.
            # Authentication success alone does not prove authorization; verify the intended identity's access.
            # Tokens and assertions are sensitive. Do not share them or enable verbose HTTP tracing.
            # For OBO, supply an incoming USER ACCESS token issued for the middle-tier API, not an ID token.
            # For certificate mode use an RSA signing certificate whose public certificate is registered.
            # For federation supply a fresh workload-provider assertion from the configured trust.
            # Managed identity: obtain the token on the actual supported Azure host using its identity SDK.
            # Token-input mode checks API access only; it does not verify the host's token acquisition.
            # For code flow, register a dedicated loopback redirect URI for this probe. No running app
            # should consume its code. After sign-in, copy the callback URL even if no listener is running.
            # For multi-tenant/guest/cross-tenant tests choose the actual resource-tenant GUID as TenantId.
            # Repeat per connection. Direct OBO exchange diagnoses the grant; also test the deployed API chain.
            # Microsoft Learn:
            # https://learn.microsoft.com/en-us/entra/identity-platform/certificate-credentials
            # https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-auth-code-flow
            # https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-on-behalf-of-flow
            # https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-device-code
            """;
        var prefix=powershell ? $"$mode = '{mode}'\n$credential = '{probe.Credential}'\n$isSpa = ${(probe.BrowserSpa ? "true" : "false")}\n$signInOnly = ${(probe.SignInOnly ? "true" : "false")}\n"
            : $"MODE='{mode}'\nCREDENTIAL='{probe.Credential}'\nIS_SPA='{probe.BrowserSpa.ToString().ToLowerInvariant()}'\nSIGNIN_ONLY='{probe.SignInOnly.ToString().ToLowerInvariant()}'\n";
        return new(powershell ? "validate-authentication.ps1" : "validate-authentication.sh",
            powershell ? "#requires -Version 7.2\n"+parameters+instructions+"\n"+PowerShellParameters+"\n"+prefix+PowerShellBody
                : "#!/usr/bin/env bash\n"+parameters+instructions+"\n"+prefix+BashBody);
    }
    private static void Validate(OAuthPlan plan)
    {
        var evaluated=new ArchitectureEvaluator().Evaluate(plan.Scenario).Plan;
        if(evaluated is null || JsonSerializer.Serialize(evaluated)!=JsonSerializer.Serialize(plan)) throw new ArgumentException("Export from the current validated plan.",nameof(plan));
    }

    private const string PowerShellParameters = """
        # Example: ./validate-authentication.ps1 -TenantId '<GUID>' -ClientId '<GUID>' `
        #   -Scope '<resource>/.default' -ResourceUri 'https://<read-only-endpoint>' `
        #   -CertificateThumbprint '<thumbprint>'
        # For delegated code/device/OBO use delegated scopes, not application permissions.
        param(
            [Guid]$TenantId,
            [Guid]$ClientId,
            [string]$Scope,
            [Uri]$ResourceUri,
            [Uri]$RedirectUri = 'http://localhost:8400',
            [string]$CertificateThumbprint,
            [string]$ClientAssertionFile,
            [string]$IncomingTokenFile,
            [string]$AccessTokenFile
        )
        """;

    private const string PowerShellBody = """
        $ErrorActionPreference = 'Stop'
        function B64Url([byte[]]$bytes) { [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+','-').Replace('/','_') }
        function Read-TokenFile([string]$path) {
            if (-not $path) { throw 'Supply the required token/assertion file path.' }
            return (Get-Content -LiteralPath $path -Raw).Trim()
        }
        function Certificate-Assertion {
            if (-not $CertificateThumbprint) { throw 'Supply CertificateThumbprint from CurrentUser/My.' }
            $thumb = $CertificateThumbprint.Replace(' ','')
            $cert = Get-Item -LiteralPath "Cert:\CurrentUser\My\$thumb"
            if (-not $cert.HasPrivateKey) { throw 'Certificate private key is unavailable.' }
            $rsa = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
            try {
                $now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
                $header = @{alg='PS256';typ='JWT';'x5t#S256'=(B64Url ([System.Security.Cryptography.SHA256]::HashData($cert.RawData)))} | ConvertTo-Json -Compress
                $payload = @{aud=$tokenUri;iss=$ClientId.ToString();sub=$ClientId.ToString();jti=[Guid]::NewGuid().ToString();nbf=$now;iat=$now;exp=($now+300)} | ConvertTo-Json -Compress
                $unsigned = (B64Url ([Text.Encoding]::UTF8.GetBytes($header)))+'.'+(B64Url ([Text.Encoding]::UTF8.GetBytes($payload)))
                $signature = $rsa.SignData([Text.Encoding]::UTF8.GetBytes($unsigned),[System.Security.Cryptography.HashAlgorithmName]::SHA256,[System.Security.Cryptography.RSASignaturePadding]::Pss)
                return $unsigned+'.'+(B64Url $signature)
            } finally { if ($rsa) { $rsa.Dispose() } }
        }
        if (-not $signInOnly -and (-not $ResourceUri -or $ResourceUri.Scheme -ne 'https' -and -not $ResourceUri.IsLoopback)) { throw 'Supply an HTTPS read-only ResourceUri (HTTP allowed only for local loopback).' }
        $tokenUri = "https://login.microsoftonline.com/$TenantId/oauth2/v2.0/token"
        if ($mode -eq 'token') { $accessToken = Read-TokenFile $AccessTokenFile }
        else {
            if ($TenantId -eq [Guid]::Empty -or $ClientId -eq [Guid]::Empty -or -not $Scope) { throw 'Supply TenantId, ClientId and Scope.' }
            $body = @{client_id=$ClientId.ToString();scope=$Scope}
            $headers = @{}
            if ($mode -eq 'code') {
                $random = [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
                $verifier = B64Url $random
                $challenge = B64Url ([System.Security.Cryptography.SHA256]::HashData([Text.Encoding]::ASCII.GetBytes($verifier)))
                $state = [Guid]::NewGuid().ToString('N')
                $query = @{client_id=$ClientId.ToString();response_type='code';response_mode='query';redirect_uri=$RedirectUri.AbsoluteUri;scope=$Scope;state=$state;code_challenge=$challenge;code_challenge_method='S256'}
                $encoded = ($query.GetEnumerator() | ForEach-Object { [Uri]::EscapeDataString($_.Key)+'='+[Uri]::EscapeDataString($_.Value) }) -join '&'
                $authorizeUri = "https://login.microsoftonline.com/$TenantId/oauth2/v2.0/authorize?$encoded"
                Write-Host 'Open this sign-in URL:' $authorizeUri
                $callback = [Uri](Read-Host 'Paste the complete callback URL after sign-in')
                if ($callback.GetLeftPart([UriPartial]::Path) -ne $RedirectUri.GetLeftPart([UriPartial]::Path)) { throw 'Unexpected callback URI.' }
                $returned = [System.Web.HttpUtility]::ParseQueryString($callback.Query)
                if ($returned['state'] -cne $state -or -not $returned['code']) { throw 'Sign-in failed or returned state/code is invalid.' }
                $body.grant_type='authorization_code';$body.code=$returned['code'];$body.redirect_uri=$RedirectUri.AbsoluteUri;$body.code_verifier=$verifier
                if ($isSpa) { $headers.Origin=$RedirectUri.GetLeftPart([UriPartial]::Authority) }
            } elseif ($mode -eq 'obo') {
                $body.grant_type='urn:ietf:params:oauth:grant-type:jwt-bearer'
                $body.requested_token_use='on_behalf_of';$body.assertion=Read-TokenFile $IncomingTokenFile
            } elseif ($mode -eq 'application') { $body.grant_type='client_credentials' }
            if ($credential -eq 'Certificate') { $body.client_assertion=Certificate-Assertion }
            elseif ($credential -eq 'WorkloadFederation') { $body.client_assertion=Read-TokenFile $ClientAssertionFile }
            if ($body.client_assertion) { $body.client_assertion_type='urn:ietf:params:oauth:client-assertion-type:jwt-bearer' }
            if ($mode -eq 'device') {
                $deviceUri="https://login.microsoftonline.com/$TenantId/oauth2/v2.0/devicecode"
                $device=Invoke-RestMethod -Method Post -Uri $deviceUri -Body @{client_id=$ClientId.ToString();scope=$Scope}
                Write-Host $device.message
                $body=@{client_id=$ClientId.ToString();grant_type='urn:ietf:params:oauth:grant-type:device_code';device_code=$device.device_code}
                $expires=[DateTimeOffset]::UtcNow.AddSeconds($device.expires_in);$interval=[int]$device.interval
                do {
                    Start-Sleep -Seconds $interval
                    try { $response=Invoke-RestMethod -Method Post -Uri $tokenUri -Body $body;break }
                    catch {
                        $errorCode=($_.ErrorDetails.Message | ConvertFrom-Json).error
                        if ($errorCode -eq 'slow_down') { $interval+=5 }
                        elseif ($errorCode -ne 'authorization_pending') { throw 'Device sign-in failed or was denied.' }
                    }
                } while ([DateTimeOffset]::UtcNow -lt $expires)
                if (-not $response) { throw 'Device sign-in expired.' }
            } else {
                try { $response=Invoke-RestMethod -Method Post -Uri $tokenUri -Headers $headers -Body $body }
                catch { throw 'Token request failed. Check tenant, grant, credential, scopes and consent. No token was displayed.' }
            }
            if ($signInOnly) {
                if (-not $response.id_token) { throw 'No ID token returned. Include openid in Scope.' }
                Write-Host 'Sign-in response received. Use the actual application/library to verify signature, issuer, audience and session behavior. This HTTP probe does not validate ID-token trust.'
                return
            }
            $accessToken=$response.access_token
            if (-not $accessToken) { throw 'No access token returned.' }
            Write-Host 'Token acquired; calling the configured resource without displaying it.'
        }
        $result=Invoke-WebRequest -Method Get -Uri $ResourceUri -Headers @{Authorization="Bearer $accessToken"} -MaximumRedirection 0 -SkipHttpErrorCheck
        Write-Host 'Authenticated GET status:' ([int]$result.StatusCode)
        $negative=Invoke-WebRequest -Method Get -Uri $ResourceUri -MaximumRedirection 0 -SkipHttpErrorCheck
        Write-Host 'GET without token status:' ([int]$negative.StatusCode)
        Write-Host 'Expected: allowed identity succeeds; missing token is rejected by a protected API. A 403 can indicate missing permissions. A 401 can indicate invalid token/audience/issuer. No response bodies or tokens were printed.'
        """;

    private const string BashBody = """
        # Requires Bash, curl, jq and OpenSSL. Run with bash validate-authentication.sh.
        # Set environment variables locally; never paste secrets into this advisor.
        # TENANT_ID, CLIENT_ID, SCOPE (application: resource/.default; delegated: actual scopes), RESOURCE_URI
        # Code flow: REDIRECT_URI (defaults to http://localhost:8400); ORIGIN for a SPA.
        # Certificate: CERTIFICATE_PEM and PRIVATE_KEY_PEM paths. Federation: CLIENT_ASSERTION_FILE.
        # OBO: INCOMING_TOKEN_FILE containing the middle-tier user access token.
        # Token input (API-only/managed identity): ACCESS_TOKEN_FILE obtained for this exact audience.
        set -euo pipefail
        umask 077
        for tool in curl jq openssl; do command -v "$tool" >/dev/null || { echo "Missing tool: $tool" >&2; exit 1; }; done
        work=$(mktemp -d)
        trap 'rm -rf -- "$work"' EXIT
        b64url() { openssl base64 -A | tr '+/' '-_' | tr -d '='; }
        if [[ "$MODE" == token ]]; then
            ACCESS_TOKEN=$(cat "${ACCESS_TOKEN_FILE:?Set ACCESS_TOKEN_FILE}")
        else
            TOKEN_URI="https://login.microsoftonline.com/${TENANT_ID:?Set TENANT_ID}/oauth2/v2.0/token"
            args=(--silent --show-error --fail-with-body --output "$work/response.json" --data-urlencode "client_id=${CLIENT_ID:?Set CLIENT_ID}" --data-urlencode "scope=${SCOPE:?Set SCOPE}")
            if [[ "$MODE" == code ]]; then
                REDIRECT_URI=${REDIRECT_URI:-http://localhost:8400}
                VERIFIER=$(openssl rand 32 | b64url)
                CHALLENGE=$(printf '%s' "$VERIFIER" | openssl dgst -sha256 -binary | b64url)
                STATE=$(openssl rand -hex 16)
                authorize_query=$(jq -rn --arg client "$CLIENT_ID" --arg uri "$REDIRECT_URI" --arg scope "$SCOPE" --arg state "$STATE" --arg challenge "$CHALLENGE" '
                  {client_id:$client,response_type:"code",response_mode:"query",redirect_uri:$uri,scope:$scope,state:$state,code_challenge:$challenge,code_challenge_method:"S256"}|to_entries|map((.key|@uri)+"="+(.value|@uri))|join("&")')
                echo "Open: https://login.microsoftonline.com/$TENANT_ID/oauth2/v2.0/authorize?$authorize_query"
                read -r -p 'Paste returned state: ' returned_state
                [[ "$returned_state" == "$STATE" ]] || { echo 'State mismatch' >&2; exit 1; }
                read -r -s -p 'Paste decoded authorization code from the matching callback URL: ' code; echo
                printf '%s' "$code" > "$work/code"
                args+=(--data-urlencode grant_type=authorization_code --data-urlencode "code@$work/code" --data-urlencode "redirect_uri=$REDIRECT_URI" --data-urlencode "code_verifier=$VERIFIER")
                if [[ "$IS_SPA" == true ]]; then args+=(--header "Origin: ${ORIGIN:?Set ORIGIN to the redirect URI origin}"); fi
            elif [[ "$MODE" == application ]]; then args+=(--data-urlencode grant_type=client_credentials)
            elif [[ "$MODE" == obo ]]; then
                args+=(--data-urlencode grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer --data-urlencode requested_token_use=on_behalf_of --data-urlencode "assertion@${INCOMING_TOKEN_FILE:?Set INCOMING_TOKEN_FILE}")
            fi
            if [[ "$CREDENTIAL" == Certificate ]]; then
                thumb=$(openssl x509 -in "${CERTIFICATE_PEM:?Set CERTIFICATE_PEM}" -outform DER | openssl dgst -sha256 -binary | b64url)
                header=$(jq -cn --arg thumb "$thumb" '{alg:"PS256",typ:"JWT","x5t#S256":$thumb}' | tr -d '\n' | b64url)
                now=$(date +%s);jti=$(openssl rand -hex 16)
                payload=$(jq -cn --arg aud "$TOKEN_URI" --arg client "$CLIENT_ID" --arg jti "$jti" --argjson now "$now" '{aud:$aud,iss:$client,sub:$client,jti:$jti,nbf:$now,iat:$now,exp:($now+300)}' | tr -d '\n' | b64url)
                unsigned="$header.$payload"
                signature=$(printf '%s' "$unsigned" | openssl dgst -sha256 -sign "${PRIVATE_KEY_PEM:?Set PRIVATE_KEY_PEM}" -sigopt rsa_padding_mode:pss -sigopt rsa_pss_saltlen:digest | b64url)
                printf '%s' "$unsigned.$signature" > "$work/assertion"
                args+=(--data-urlencode client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer --data-urlencode "client_assertion@$work/assertion")
            elif [[ "$CREDENTIAL" == WorkloadFederation ]]; then
                args+=(--data-urlencode client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer --data-urlencode "client_assertion@${CLIENT_ASSERTION_FILE:?Set CLIENT_ASSERTION_FILE}")
            fi
            if [[ "$MODE" == device ]]; then
                curl --silent --show-error --fail --output "$work/device.json" --data-urlencode "client_id=$CLIENT_ID" --data-urlencode "scope=$SCOPE" "https://login.microsoftonline.com/$TENANT_ID/oauth2/v2.0/devicecode"
                jq -r '.message' "$work/device.json"
                jq -jr '.device_code' "$work/device.json" > "$work/device_code"
                interval=$(jq -r '.interval' "$work/device.json");expires=$(( $(date +%s) + $(jq -r '.expires_in' "$work/device.json") ))
                while true; do
                    [[ $(date +%s) -lt $expires ]] || { echo 'Device code expired' >&2; exit 1; }
                    sleep "$interval"
                    curl --silent --show-error --output "$work/response.json" --data-urlencode "client_id=$CLIENT_ID" --data-urlencode grant_type=urn:ietf:params:oauth:grant-type:device_code --data-urlencode "device_code@$work/device_code" "$TOKEN_URI"
                    error=$(jq -r '.error // empty' "$work/response.json")
                    case "$error" in authorization_pending) continue;; slow_down) interval=$((interval+5));continue;; '') break;; *) echo 'Device sign-in failed' >&2;exit 1;; esac
                done
            else curl "${args[@]}" "$TOKEN_URI" || { echo 'Token request failed; check configuration and consent.' >&2;exit 1; }; fi
            if [[ "$SIGNIN_ONLY" == true ]]; then
                jq -e '.id_token | type == "string"' "$work/response.json" >/dev/null
                echo 'Sign-in response received. Validate ID-token trust and session behavior through the actual application/library.';exit 0
            fi
            ACCESS_TOKEN=$(jq -er '.access_token' "$work/response.json")
        fi
        # Supply an HTTPS read-only endpoint, or local loopback HTTP for your test API.
        RESOURCE_URI=${RESOURCE_URI:?Set RESOURCE_URI to a read-only endpoint}
        case "$RESOURCE_URI" in https://*|http://localhost:*|http://127.0.0.1:*) ;; *) echo 'Use HTTPS or loopback HTTP' >&2;exit 1;; esac
        printf 'Authorization: Bearer %s\n' "$ACCESS_TOKEN" | curl --silent --show-error --output /dev/null --write-out 'Authenticated GET HTTP %{http_code}\n' --header @- "$RESOURCE_URI"
        curl --silent --show-error --output /dev/null --write-out 'GET without token HTTP %{http_code}\n' "$RESOURCE_URI"
        echo 'Expected: allowed call succeeds; missing token is rejected. 403: inspect permissions. 401: inspect token audience/issuer. No tokens or response bodies printed.'
        """;
}
