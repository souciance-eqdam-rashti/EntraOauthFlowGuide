# Local authentication probes

In Implementation, use **Validate authentication**, select the connection and export **PowerShell** or **cURL (Bash)**. Each script includes its plan ID and connection label. The visible script can be copied if the browser blocks saving.

Complete the guide's registrations, permission/role grants and consent first. Run probes locally with values belonging to the selected caller and target. Tokens and credentials are never entered into the advisor. Choose a read-only, protected GET endpoint. The output reports the authenticated request status and the request-without-token status without printing tokens or resource response bodies.

## PowerShell

Requires PowerShell 7.2+. Certificate mode uses an RSA certificate with its private key in the running user's Windows `CurrentUser/My` store. Its public certificate must be uploaded to the calling app's registration.

```powershell
./validate-authentication.ps1 -TenantId '<tenant GUID>' -ClientId '<caller client GUID>' `
  -Scope '<target resource>/.default' -ResourceUri 'https://<protected read-only endpoint>' `
  -CertificateThumbprint '<caller certificate thumbprint>'
```

Application access uses the resource's `/.default` scope. User flows use actual delegated scopes. For authorization-code probes, supply `-RedirectUri` matching a dedicated registered loopback callback that an existing app will not consume. Open the displayed URL, complete sign-in and paste the full callback URL; the script checks state and exchanges the code using PKCE. For sign-in-only probes include `openid` in `-Scope`; successful token issuance does not by itself validate ID-token trust or application session behavior.

For OBO, supply `-IncomingTokenFile` containing a fresh user access token issued for the middle-tier API, plus that API's client ID and certificate. The downstream scope belongs to the target. For federation, use `-ClientAssertionFile` containing a fresh assertion obtained from the configured workload provider. Device-code probes display the provider's sign-in instructions and poll within the expiry window.

For managed-identity or API-only probes, supply `-AccessTokenFile` and `-ResourceUri`. Obtain the access token from the real host identity SDK or initiating caller for the correct audience. This mode verifies API access; it does not verify acquisition by the host or replace its platform-specific setup.

## cURL

The `.sh` export runs in Bash with cURL, jq and OpenSSL. Fill the named environment variables described at its top. Certificate mode requires a matching RSA PEM certificate/private-key pair; non-exportable Windows keys should use the PowerShell export instead. Short-lived token responses and assertions use a private temporary directory removed on exit.

The cURL code-flow probe displays an authorization URL, checks the returned state and accepts the decoded code from the matching callback. Browser/SPA probes also need the redirect URI's origin for the token request. Other grants use the same connection-specific dependencies described above. Managed-identity/API-only mode reads `ACCESS_TOKEN_FILE` and uses `RESOURCE_URI` without requesting a different identity's token.

## Interpretation and coverage

An allowed call normally returns 2xx. Missing authentication should be rejected by a protected API; 401 can indicate token validation problems, while 403 can indicate denied authorization. Choose an endpoint that actually requires the permissions being tested. Repeat with disallowed identities and wrong-audience tokens through the actual app/API test procedure. Direct OBO exchange isolates the protocol grant; separately test the deployed chain and interaction-required handling.

Probes do not create registrations or grant consent. Multi-tenant, guest and cross-tenant probes need the correct tenant context and actual consent/assignments. Live tenant verification remains outstanding. Unit and offline tests verify script generation, PowerShell syntax, certificate assertion signing and local authenticated/unauthenticated HTTP behavior; Bash exports have not been executed in this Windows environment.

Sources: [certificate assertions](https://learn.microsoft.com/en-us/entra/identity-platform/certificate-credentials), [authorization code and PKCE](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-auth-code-flow), [OBO](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-on-behalf-of-flow), [device code](https://learn.microsoft.com/en-us/entra/identity-platform/v2-oauth2-device-code). Reviewed 2026-10-06.
