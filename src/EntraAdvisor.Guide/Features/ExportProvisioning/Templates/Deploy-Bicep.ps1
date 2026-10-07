#Requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$TenantId,[Parameter(Mandatory)][string]$ResourceGroupName,
 [Parameter(Mandatory)][ValidateSet('Register','Configure')][string]$Phase,
 [string]$SettingsPath=(Join-Path $PSScriptRoot 'settings.json'),[string]$StatePath=(Join-Path $PSScriptRoot 'state.json'))
. (Join-Path $PSScriptRoot 'Provisioning.Common.ps1')
Initialize-Setup $TenantId $SettingsPath $StatePath @('Application.ReadWrite.All')
Import-Module Az.Resources -ErrorAction Stop
if((Get-AzContext).Tenant.Id -ne $Tenant){throw 'Connect-AzAccount to the selected tenant/subscription first.'}
$applications=@()
foreach($app in (Get-TenantApplications)){
 $body=Get-ApplicationBody $app
 $template=@{componentId=$app.componentId;uniqueName=(Get-UniqueName $app.componentId);displayName=$body.displayName;signInAudience=$body.signInAudience;
  platform=$app.platform;redirectUris=$Settings.applications[$app.componentId].redirectUris;deviceCode=$body.isFallbackPublicClient;
  isApi=$app.isApi;keyCredentials=$body.keyCredentials;scopes=$body.api.oauth2PermissionScopes;roles=$body.appRoles}
 if($Phase -eq 'Configure'){
  $identity=Assert-OwnedState $app
  $template.appId=$identity.appId;$template.requiredResourceAccess=Get-RequiredAccess $app
 }
 $applications+=$template
}
if(!$applications.Count){Write-Output 'No app registrations to deploy in this tenant; see README for managed identity/resource steps.';return}
$file=if($Phase -eq 'Register'){'create.bicep'}else{'configure.bicep'}
$parameters=@{'$schema'='https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#';contentVersion='1.0.0.0';parameters=@{applications=@{value=$applications}}}
$parameterFile=Join-Path $PSScriptRoot ($Phase.ToLowerInvariant() + '.parameters.json')
$parameters | ConvertTo-Json -Depth 50 | Set-Content $parameterFile -Encoding utf8
$result=New-AzResourceGroupDeployment -Name ('advisor-' + $Phase.ToLowerInvariant()) -ResourceGroupName $ResourceGroupName -TemplateFile (Join-Path $PSScriptRoot $file) -TemplateParameterFile $parameterFile -ErrorAction Stop
foreach($identity in $result.Outputs.identities.Value){$State.applications[$identity.componentId]=@{appId=$identity.appId;objectId=$identity.objectId;principalId=$identity.principalId;tenantId=$Tenant}}
Save-State
Write-Output "Completed $Phase. IDs saved to $StateFile. Consent and runtime setup are separate."
