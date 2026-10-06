[CmdletBinding()]
param(
    [string]$BasePath = '/',
    [string]$OutputPath = ''
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ($BasePath -notmatch '^/(?:[A-Za-z0-9_.-]+/)*$' -or $BasePath.Contains('..')) {
    throw 'BasePath must be / or a URL directory such as /EntraOauthFlowGuide/.'
}
if (-not $OutputPath) { $OutputPath = Join-Path $repositoryRoot 'artifacts/static-site' }
& dotnet publish (Join-Path $repositoryRoot 'src/EntraAdvisor.Web') -c Release -o $OutputPath
if ($LASTEXITCODE -ne 0) { throw 'Static publish failed.' }
$webRoot = Join-Path $OutputPath 'wwwroot'
$indexPath = Join-Path $webRoot 'index.html'
$index = [IO.File]::ReadAllText($indexPath).Replace('<base href="/" />', ('<base href="' + $BasePath + '" />'))
[IO.File]::WriteAllText($indexPath, $index)
# GitHub Pages uses 404.html for direct client routes; assets keep the configured base.
[IO.File]::WriteAllText((Join-Path $webRoot '404.html'), $index)
[IO.File]::WriteAllText((Join-Path $webRoot '.nojekyll'), '')
Write-Output "Deploy the contents of $webRoot"
