[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
& dotnet run --project (Join-Path $repositoryRoot 'src/EntraAdvisor.Web') --no-build --no-restore --launch-profile http
exit $LASTEXITCODE
