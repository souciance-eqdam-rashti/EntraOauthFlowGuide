#Requires -Version 7.0
param([Parameter(Mandatory)][string]$BundleDirectory)
$ErrorActionPreference='Stop'
$sample=Join-Path ([IO.Path]::GetTempPath()) ('entra-provisioning-check-' + [guid]::NewGuid())
New-Item -ItemType Directory -Path $sample | Out-Null
Copy-Item -Path (Join-Path $BundleDirectory '*') -Destination $sample
# Tests invoke exported scripts with mocked Graph functions; no authentication or network writes.

$global:Apps=@{};$global:Principals=@{};$global:Grants=@{};$global:Connections=0;$global:Writes=0
$tenant='11111111-2222-3333-4444-555555555555'
function Import-Module {param($Name,$ErrorAction)}
function Connect-MgGraph {param($TenantId,$Scopes,$ContextScope,[switch]$NoWelcome) $global:Connections++}
function Get-MgContext {return @{TenantId=$tenant}}
function Invoke-MgGraphRequest {
 param($Method,$Uri,$Body,$ContentType)
 $uriObject=[uri]$Uri;$path=$uriObject.AbsolutePath.Replace('/v1.0/','');$query=[uri]::UnescapeDataString($uriObject.Query)
 $payload=if($Body){$Body|ConvertFrom-Json -AsHashtable}else{@{}}
 if($Method -eq 'GET'){
  if($path -eq 'applications'){
   $key=([regex]::Match($query,"uniqueName eq '([^']+)'")).Groups[1].Value
   return @{value=@($global:Apps.Values | Where-Object uniqueName -EQ $key)}
  }
  if($path -like 'applications/*'){return $global:Apps[$path.Split('/')[1]]}
  if($path -eq 'servicePrincipals'){
   $appId=([regex]::Match($query,"appId eq '([^']+)'")).Groups[1].Value
   return @{value=@($global:Principals.Values | Where-Object appId -EQ $appId)}
  }
  if($path -eq 'oauth2PermissionGrants'){return @{value=@($global:Grants.Values)}}
 }
 $global:Writes++
 if($Method -eq 'POST' -and $path -eq 'applications'){
  $payload.id=[guid]::NewGuid().ToString();$payload.appId=[guid]::NewGuid().ToString();$global:Apps[$payload.id]=$payload;return $payload
 }
 if($Method -eq 'POST' -and $path -eq 'servicePrincipals'){
  $app=@($global:Apps.Values | Where-Object appId -EQ $payload.appId)[0]
  $payload.id=[guid]::NewGuid().ToString();$payload.oauth2PermissionScopes=$app.api.oauth2PermissionScopes;$payload.appRoles=$app.appRoles
  $global:Principals[$payload.id]=$payload;return $payload
 }
 if($Method -eq 'PATCH' -and $path -like 'applications/*'){
  $app=$global:Apps[$path.Split('/')[1]];foreach($key in $payload.Keys){$app[$key]=$payload[$key]};return
 }
 if($Method -eq 'POST' -and $path -eq 'oauth2PermissionGrants'){
  $payload.id=[guid]::NewGuid().ToString();$global:Grants[$payload.id]=$payload;return $payload
 }
 throw "Unexpected mocked request $Method $Uri"
}
$settingsFile=Join-Path $sample 'mock-settings.json';$stateFile=Join-Path $sample 'mock-state.json'
$unfilled=Join-Path $sample 'settings.json'
try { & (Join-Path $sample 'Setup-Entra.ps1') -TenantId $tenant -Phase Register -SettingsPath $unfilled -StatePath $stateFile;throw 'Expected placeholder rejection.' }catch{if($_.Exception.Message -notmatch 'placeholder'){throw}}
if($global:Connections -ne 0 -or $global:Writes -ne 0){throw 'Placeholder preflight must precede authentication and writes.'}
$s=Get-Content -Raw $unfilled|ConvertFrom-Json -AsHashtable
$s.deploymentKey='test-deployment'
foreach($id in $s.applications.Keys){$s.applications[$id].displayName=$id;$s.applications[$id].tenantId=$tenant;if($s.applications[$id].redirectUris.Count){$s.applications[$id].redirectUris=@('https://localhost/auth/callback')}}
foreach($p in $s.permissions.Values){$p.value='Orders.Read';$p.displayName='Read orders';$p.description='Read allowed orders';$p.consentType='User'}
foreach($r in $s.resources.Values){$r.tenantId=$tenant}
$s|ConvertTo-Json -Depth 50|Set-Content $settingsFile
& (Join-Path $sample 'Setup-Entra.ps1') -TenantId $tenant -Phase Register -SettingsPath $settingsFile -StatePath $stateFile
& (Join-Path $sample 'Setup-Entra.ps1') -TenantId $tenant -Phase Register -SettingsPath $settingsFile -StatePath $stateFile
if($global:Apps.Count -ne 2 -or $global:Principals.Count -ne 2){throw 'Registration rerun duplicated identities.'}
& (Join-Path $sample 'Setup-Entra.ps1') -TenantId $tenant -Phase Configure -SettingsPath $settingsFile -StatePath $stateFile
$browser=@($global:Apps.Values|Where-Object displayName -EQ 'browser')[0]
if($browser.requiredResourceAccess.Count -ne 1 -or $browser.requiredResourceAccess[0].resourceAccess[0].type -ne 'Scope'){throw 'Caller delegated permission wiring missing.'}
if($global:Grants.Count){throw 'Setup must not grant consent.'}
& (Join-Path $sample 'Grant-Consent.ps1') -TenantId $tenant -GrantTenantWideConsent -SettingsPath $settingsFile -StatePath $stateFile
& (Join-Path $sample 'Grant-Consent.ps1') -TenantId $tenant -GrantTenantWideConsent -SettingsPath $settingsFile -StatePath $stateFile
if($global:Grants.Count -ne 1){throw 'Consent rerun duplicated grants.'}
Write-Output 'Mock checks passed: placeholders blocked before connection; reruns reuse identities; scope IDs wired; consent is opt-in and idempotent.'

# Custom cross-tenant wiring uses authored IDs without requiring a local resource principal.
. (Join-Path $sample 'Provisioning.Common.ps1')
Initialize-Setup $tenant $settingsFile $stateFile @('Application.ReadWrite.All')
$caller=@($Plan.applications | Where-Object componentId -EQ 'browser')[0]
$before=$global:Writes
$Plan.access[0].boundary='CrossTenant'
$apiPrincipalId=$State.applications['api-a'].principalId
$global:Principals.Remove($apiPrincipalId)
$access=Get-RequiredAccess $caller
if($access[0].resourceAccess[0].id -ne $Plan.permissions[0].id -or $global:Writes -ne $before){throw 'Cross-tenant custom permission wiring should use authored IDs without Graph writes.'}
Write-Output 'Cross-tenant custom permission ID check passed.'
