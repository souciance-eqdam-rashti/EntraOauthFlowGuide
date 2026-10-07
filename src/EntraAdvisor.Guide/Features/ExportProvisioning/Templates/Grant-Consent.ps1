#Requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$TenantId, [switch]$GrantTenantWideConsent,
 [string]$SettingsPath=(Join-Path $PSScriptRoot 'settings.json'), [string]$StatePath=(Join-Path $PSScriptRoot 'state.json'))
if (!$GrantTenantWideConsent) { throw 'Review the selected permissions, then explicitly supply -GrantTenantWideConsent. This grants access for the tenant, not just one user.' }
. (Join-Path $PSScriptRoot 'Provisioning.Common.ps1')
Initialize-Setup $TenantId $SettingsPath $StatePath @('Application.ReadWrite.All','DelegatedPermissionGrant.ReadWrite.All','AppRoleAssignment.ReadWrite.All')
foreach($access in $Plan.access) {
 if($access.category -eq 'AzureResource' -or $Settings.resources[$access.resourceId].tenantId -ne $Tenant){continue}
 $resolved=Resolve-Permissions $access
 $caller=$Plan.applications | Where-Object componentId -EQ $access.callerComponentId
 if($access.credential -eq 'ManagedIdentity'){
  if($Settings.applications[$caller.componentId].tenantId -ne $Tenant){throw 'A managed identity cannot be onboarded as a cross-tenant app.'}
  $principal=Invoke-MgGraphRequest -Method GET -Uri ('https://graph.microsoft.com/v1.0/servicePrincipals/' + ([guid]$Settings.applications[$caller.componentId].managedIdentityPrincipalId).ToString())
  if($principal.servicePrincipalType -ne 'ManagedIdentity'){throw 'The supplied identity is not a managed identity.'}
 } else {
  if(!$State.applications.ContainsKey($caller.componentId)){throw 'Register the caller first.'}
  $principal=Resolve-Principal $State.applications[$caller.componentId].appId $true
 }
 $resource=$resolved.principal
 if($access.mode -eq 'DelegatedScopes'){
  $filter="clientId eq '$($principal.id)' and resourceId eq '$($resource.id)' and consentType eq 'AllPrincipals'"
  $existing=Get-GraphCollection 'oauth2PermissionGrants' $filter
  if($existing.Count -gt 1){throw 'Ambiguous delegated grant.'}
  $values=@($resolved.permissions.value)
  if($existing.Count -eq 1){
   $values+=@($existing[0].scope -split ' ' | Where-Object {$_})
   $scope=(@($values | Sort-Object -Unique) -join ' ')
   if($scope -ne $existing[0].scope){Invoke-MgGraphRequest -Method PATCH -Uri ('https://graph.microsoft.com/v1.0/oauth2PermissionGrants/' + $existing[0].id) -Body (@{scope=$scope}|ConvertTo-Json) -ContentType 'application/json' | Out-Null}
  } else { Invoke-MgGraphRequest -Method POST -Uri 'https://graph.microsoft.com/v1.0/oauth2PermissionGrants' -Body (@{clientId=$principal.id;resourceId=$resource.id;consentType='AllPrincipals';scope=($values -join ' ')}|ConvertTo-Json) -ContentType 'application/json' | Out-Null }
 } else {
  $existing=Get-GraphCollection ('servicePrincipals/' + $principal.id + '/appRoleAssignments')
  foreach($permission in $resolved.permissions){
   if(!@($existing | Where-Object {$_.resourceId -eq $resource.id -and $_.appRoleId -eq $permission.id}).Count){
    Invoke-MgGraphRequest -Method POST -Uri ('https://graph.microsoft.com/v1.0/servicePrincipals/' + $resource.id + '/appRoleAssignedTo') -Body (@{principalId=$principal.id;resourceId=$resource.id;appRoleId=$permission.id}|ConvertTo-Json) -ContentType 'application/json' | Out-Null
   }
  }
 }
}
Write-Output 'Consent stage finished for selected relationships in this tenant. Azure resource authorization, tenant policies and runtime validation remain separate.'
