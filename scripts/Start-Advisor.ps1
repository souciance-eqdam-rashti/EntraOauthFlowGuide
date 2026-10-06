[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$env:ASPIRE_ALLOW_UNSECURED_TRANSPORT = 'true'
# Aspire's local control-plane and OTLP traffic must stay on loopback.
$env:NO_PROXY = (@($env:NO_PROXY, 'localhost', '127.0.0.1', '::1') | Where-Object { $_ }) -join ','

& dotnet run --project (Join-Path $repositoryRoot 'src/EntraAdvisor.AppHost') --no-build --no-restore --launch-profile http
exit $LASTEXITCODE
