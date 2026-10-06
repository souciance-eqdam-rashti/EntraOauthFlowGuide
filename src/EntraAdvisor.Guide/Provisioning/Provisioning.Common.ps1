# Shared by Bicep and PowerShell exports. Graph writes only occur in the entry-point scripts.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Initialize-Setup {
 param([string]$TenantId, [string]$SettingsPath, [string]$StatePath, [string[]]$Scopes)
 $script:Tenant = ([guid]$TenantId).ToString()
 $script:Plan = Get-Content -Raw (Join-Path $PSScriptRoot 'plan.json') | ConvertFrom-Json -AsHashtable
 $script:Settings = Get-Content -Raw $SettingsPath | ConvertFrom-Json -AsHashtable
 $script:StateFile = $StatePath
 if (($Settings | ConvertTo-Json -Depth 50) -match '<[^>]+>') { throw 'Fill every placeholder in settings.json before running setup.' }
 if ($Settings.deploymentKey -notmatch '^[a-zA-Z0-9._-]{3,80}$') { throw 'Use a stable deploymentKey of 3-80 letters, numbers, dots, underscores or hyphens.' }
 $script:State = if (Test-Path $StatePath) { Get-Content -Raw $StatePath | ConvertFrom-Json -AsHashtable } else { @{ planId=$Plan.planId; deploymentKey=$Settings.deploymentKey; applications=@{} } }
 if ($State.planId -ne $Plan.planId -or $State.deploymentKey -ne $Settings.deploymentKey) { throw 'State belongs to a different plan/deployment. Review and reconcile it; do not overwrite it.' }
 foreach ($app in $Plan.applications) {
  $input = $Settings.applications[$app.componentId]
  if (!$input) { throw "Missing settings for $($app.componentId)" }
  $null = [guid]$input.tenantId
  if ($app.createRegistration -and [string]::IsNullOrWhiteSpace($input.displayName)) { throw 'Application name is required.' }
  if ($app.usesManagedIdentity) { $null = [guid]$input.managedIdentityPrincipalId }
  foreach ($uri in $input.redirectUris) { if (![uri]::IsWellFormedUriString($uri, [UriKind]::Absolute)) { throw "Invalid redirect URI: $uri" } }
  if ($app.platform -ne 'None' -and !$app.deviceCode -and @($input.redirectUris).Count -eq 0) { throw "Redirect URI required for $($app.componentId)" }
 }
 foreach ($permission in $Plan.permissions) {
  $input = $Settings.permissions[$permission.key]
  if (!$input -or $input.value -notmatch '^[A-Za-z][A-Za-z0-9._-]*$' -or !$input.displayName -or !$input.description) { throw "Supply a name and descriptions for permission $($permission.key)." }
  if ($permission.mode -eq 'DelegatedScopes' -and $input.consentType -notin @('User','Admin')) { throw 'Scope consentType must be User or Admin.' }
 }
 foreach ($resource in $Settings.resources.Values) { $null = [guid]$resource.tenantId; if ($resource.appId) { $null = [guid]$resource.appId } }
 foreach ($access in $Plan.access) {
  if ($access.category -eq 'MicrosoftGraph' -and $Settings.resources[$access.resourceId].appId -ne '00000003-0000-0000-c000-000000000000') { throw 'Microsoft Graph resource app ID must match the selected provider.' }
  $callerTenant = $Settings.applications[$access.callerComponentId].tenantId
  $resourceTenant = $Settings.resources[$access.resourceId].tenantId
  if (($access.boundary -eq 'SameTenant') -ne ($callerTenant -eq $resourceTenant)) { throw "Tenant inputs contradict the selected boundary for $($access.relationshipId)." }
  if ($access.apiComponentId -and $Settings.applications[$access.apiComponentId].tenantId -ne $resourceTenant) { throw "Resource tenant must match the custom API home tenant for $($access.resourceId)." }
  if (!$access.apiComponentId -and $access.category -ne 'AzureResource' -and @($Settings.access[$access.relationshipId].permissionValues).Count -eq 0) { throw 'Select at least one actual resource permission.' }
 }
 Import-Module Microsoft.Graph.Authentication -ErrorAction Stop
 Connect-MgGraph -TenantId $Tenant -Scopes $Scopes -ContextScope Process -NoWelcome
 if ((Get-MgContext).TenantId -ne $Tenant) { throw 'Graph connection tenant mismatch.' }
}
function Save-State { $State | ConvertTo-Json -Depth 50 | Set-Content -LiteralPath $StateFile -Encoding utf8 }
function Get-UniqueName([string]$ComponentId) {
 $bytes = [Text.Encoding]::UTF8.GetBytes($Settings.deploymentKey + ':' + $ComponentId)
 return 'advisor-' + [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}
function Get-GraphCollection([string]$Path, [string]$Filter='') {
 $uri = 'https://graph.microsoft.com/v1.0/' + $Path
 if ($Filter) { $uri += '?$filter=' + [uri]::EscapeDataString($Filter) }
 $items = @()
 do { $response = Invoke-MgGraphRequest -Method GET -Uri $uri; $items += @($response.value); $uri = $response['@odata.nextLink'] } while ($uri)
 return ,$items
}
function Resolve-Principal([string]$AppId, [bool]$Create=$false) {
 $id = ([guid]$AppId).ToString()
 for ($attempt=0; $attempt -lt 6; $attempt++) {
  $matches = Get-GraphCollection 'servicePrincipals' "appId eq '$id'"
  if ($matches.Count -eq 1) { return $matches[0] }
  if ($matches.Count -gt 1) { throw 'Ambiguous service principal.' }
  if ($Create -and $attempt -eq 0) { return Invoke-MgGraphRequest -Method POST -Uri 'https://graph.microsoft.com/v1.0/servicePrincipals' -Body (@{appId=$id} | ConvertTo-Json) -ContentType 'application/json' }
  if ($attempt -lt 5) { Start-Sleep -Seconds 3 }
 }
 throw "Service principal $id is not present in tenant $Tenant. Complete registration/onboarding there first."
}
function Get-ApplicationBody($App) {
 $input = $Settings.applications[$App.componentId]
 $scopes=@(); $roles=@()
 foreach ($p in @($Plan.permissions | Where-Object apiComponentId -EQ $App.componentId)) {
  $v = $Settings.permissions[$p.key]
  if ($p.mode -eq 'DelegatedScopes') {
   $scopes += @{ id=$p.id;value=$v.value;isEnabled=$true;type=$v.consentType;adminConsentDisplayName=$v.displayName;adminConsentDescription=$v.description;userConsentDisplayName=$v.displayName;userConsentDescription=$v.description }
  } else { $roles += @{id=$p.id;value=$v.value;displayName=$v.displayName;description=$v.description;isEnabled=$true;allowedMemberTypes=@('Application')} }
 }
 if (@($scopes | ForEach-Object {$_.value} | Select-Object -Unique).Count -ne $scopes.Count -or @($roles | ForEach-Object {$_.value} | Select-Object -Unique).Count -ne $roles.Count) { throw 'Duplicate permission values on an API.' }
 $keys=@()
 if ($App.credential -eq 'Certificate') {
  $cert = [Security.Cryptography.X509Certificates.X509Certificate2]::new([IO.File]::ReadAllBytes((Resolve-Path $input.certificatePath)))
  try {
   if ($cert.HasPrivateKey) { throw 'Supply a public .cer certificate, never a private-key file.' }
   if ($cert.NotAfter.ToUniversalTime() -le [datetime]::UtcNow) { throw 'Certificate is expired.' }
   $keys += @{type='AsymmetricX509Cert';usage='Verify';key=[Convert]::ToBase64String($cert.RawData);displayName='Advisor public certificate';startDateTime=$cert.NotBefore.ToUniversalTime().ToString('o');endDateTime=$cert.NotAfter.ToUniversalTime().ToString('o')}
  } finally { $cert.Dispose() }
 }
 return @{displayName=$input.displayName;signInAudience=$App.signInAudience;isFallbackPublicClient=[bool]$App.deviceCode;
  spa=@{redirectUris=@($(if($App.platform -eq 'Spa'){$input.redirectUris}))};
  web=@{redirectUris=@($(if($App.platform -eq 'Web'){$input.redirectUris}));implicitGrantSettings=@{enableAccessTokenIssuance=$false;enableIdTokenIssuance=$false}};
  publicClient=@{redirectUris=@($(if($App.platform -eq 'PublicClient'){$input.redirectUris}))};
  keyCredentials=$keys;api=@{requestedAccessTokenVersion=2;oauth2PermissionScopes=$scopes};appRoles=$roles}
}
function Get-ResourceAppId($Access) {
 if ($Access.apiComponentId) {
  if (!$State.applications.ContainsKey($Access.apiComponentId)) { throw 'Register every custom API before configuring callers.' }
  return $State.applications[$Access.apiComponentId].appId
 }
 return $Settings.resources[$Access.resourceId].appId
}
function Get-PermissionValues($Access) {
 if ($Access.apiComponentId) { return ,@($Settings.permissions[$Access.permissionKey].value) }
 return ,@($Settings.access[$Access.relationshipId].permissionValues)
}
function Resolve-Permissions($Access) {
 $appId = Get-ResourceAppId $Access
 $values = Get-PermissionValues $Access
 for ($attempt=0;$attempt -lt 6;$attempt++) {
  $principal = Resolve-Principal $appId
  $definitions = if ($Access.mode -eq 'DelegatedScopes') { @($principal.oauth2PermissionScopes) } else { @($principal.appRoles) }
  $resolved=@()
  foreach ($value in $values) {
   $matches=@($definitions | Where-Object { $_.value -eq $value -and $_.isEnabled -and ($Access.mode -eq 'DelegatedScopes' -or 'Application' -in $_.allowedMemberTypes) })
   if ($matches.Count -eq 1) { $resolved += @{id=$matches[0].id;type=$(if($Access.mode -eq 'DelegatedScopes'){'Scope'}else{'Role'});value=$value} }
  }
  if ($resolved.Count -eq $values.Count) { return @{principal=$principal;permissions=$resolved;appId=$appId} }
  if ($attempt -lt 5) { Start-Sleep -Seconds 3 }
 }
 throw "A requested permission is missing, disabled or not valid for this access mode on $appId. Review settings.json; no permission will be guessed."
}
function Get-RequiredAccess($App) {
 $resources=@{}
 foreach ($access in @($Plan.access | Where-Object callerComponentId -EQ $App.componentId)) {
  if ($access.category -eq 'AzureResource' -or $access.credential -eq 'ManagedIdentity') { continue }
  if ($access.apiComponentId) {
   # IDs are authored by this typed plan and provisioned in the API home tenant during Register.
   # Referencing them does not require creating a resource principal in the caller home tenant.
   $appId=Get-ResourceAppId $access
   $definition=@($Plan.permissions | Where-Object { $_.key -eq $access.permissionKey -and $_.apiComponentId -eq $access.apiComponentId -and $_.mode -eq $access.mode })
   if ($definition.Count -ne 1) { throw 'Missing or ambiguous typed custom API permission.' }
   $resolved=@{appId=$appId;permissions=@(@{id=$definition[0].id;type=$(if($access.mode -eq 'DelegatedScopes'){'Scope'}else{'Role'})})}
  } else { $resolved=Resolve-Permissions $access }
  if (!$resources.ContainsKey($resolved.appId)) { $resources[$resolved.appId]=@() }
  $resources[$resolved.appId] += @($resolved.permissions | ForEach-Object { @{id=$_.id;type=$_.type} })
 }
 return ,@($resources.Keys | ForEach-Object { @{resourceAppId=$_;resourceAccess=@($resources[$_] | Sort-Object { $_.id } -Unique)} })
}
function Get-TenantApplications {
 return ,@($Plan.applications | Where-Object { $_.createRegistration -and $Settings.applications[$_.componentId].tenantId -eq $Tenant })
}
function Assert-OwnedState($App) {
 if (!$State.applications.ContainsKey($App.componentId)) { throw "Register $($App.componentId) first." }
 $identity=$State.applications[$App.componentId]
 if ($identity.tenantId -ne $Tenant) { throw 'Saved application tenant mismatch.' }
 $current=Invoke-MgGraphRequest -Method GET -Uri ('https://graph.microsoft.com/v1.0/applications/' + ([guid]$identity.objectId).ToString())
 if ($current.uniqueName -ne (Get-UniqueName $App.componentId) -or $current.appId -ne $identity.appId) { throw 'Saved application does not match the owned uniqueName.' }
 return $identity
}
