#Requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$TenantId, [Parameter(Mandatory)][ValidateSet('Register','Configure')][string]$Phase,
 [string]$SettingsPath=(Join-Path $PSScriptRoot 'settings.json'), [string]$StatePath=(Join-Path $PSScriptRoot 'state.json'))
. (Join-Path $PSScriptRoot 'Provisioning.Common.ps1')
Initialize-Setup $TenantId $SettingsPath $StatePath @('Application.ReadWrite.All')
$apps=Get-TenantApplications
# Preflight all selected applications (including certificate inputs) before writes.
$bodies=@{}; foreach($app in $apps) { $bodies[$app.componentId]=Get-ApplicationBody $app }
if ($Phase -eq 'Register') {
 foreach($app in $apps) {
  $key=Get-UniqueName $app.componentId
  $existing=Get-GraphCollection 'applications' "uniqueName eq '$key'"
  if ($existing.Count -gt 1) { throw 'Ambiguous application identity.' }
  if ($existing.Count -eq 1) { $registered=$existing[0] } else {
   $body=$bodies[$app.componentId];$body.uniqueName=$key
   $registered=Invoke-MgGraphRequest -Method POST -Uri 'https://graph.microsoft.com/v1.0/applications' -Body ($body | ConvertTo-Json -Depth 50) -ContentType 'application/json'
  }
  # Save the application first so a partial service-principal failure can be resumed.
  $State.applications[$app.componentId]=@{appId=$registered.appId;objectId=$registered.id;tenantId=$Tenant}
  Save-State
  $principal=Resolve-Principal $registered.appId $true
  $State.applications[$app.componentId].principalId=$principal.id;Save-State
 }
} else {
 $updates=@()
 foreach($app in $apps) {
  $identity=Assert-OwnedState $app
  $body=$bodies[$app.componentId]
  if($app.isApi){$body.identifierUris=@('api://' + $identity.appId)}
  $body.requiredResourceAccess=Get-RequiredAccess $app
  $updates+=@{identity=$identity;body=$body}
 }
 foreach($update in $updates) { Invoke-MgGraphRequest -Method PATCH -Uri ('https://graph.microsoft.com/v1.0/applications/' + $update.identity.objectId) -Body ($update.body | ConvertTo-Json -Depth 50) -ContentType 'application/json' | Out-Null }
}
Write-Output "Completed $Phase for tenant $Tenant. IDs are in $StateFile. Consent and runtime setup are separate."
