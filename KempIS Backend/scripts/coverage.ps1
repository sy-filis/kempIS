#requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

$resultsDir = Join-Path $repoRoot 'TestResults'
$reportDir  = Join-Path $resultsDir 'CoverageReport'

Write-Host "==> Cleaning $resultsDir" -ForegroundColor Cyan
if (Test-Path $resultsDir) {
    Remove-Item -Recurse -Force $resultsDir
}
New-Item -ItemType Directory -Path $resultsDir | Out-Null

Write-Host '==> Restoring local .NET tools' -ForegroundColor Cyan
dotnet tool restore

Write-Host '==> Running tests with coverage collection' -ForegroundColor Cyan
dotnet test KempISBackend.slnx `
    --collect:"XPlat Code Coverage" `
    --settings (Join-Path $repoRoot 'coverlet.runsettings') `
    --results-directory $resultsDir

Write-Host '==> Generating HTML report' -ForegroundColor Cyan
$reportsGlob = Join-Path $resultsDir '**/coverage.cobertura.xml'
dotnet reportgenerator `
    "-reports:$reportsGlob" `
    "-targetdir:$reportDir" `
    '-reporttypes:Html;TextSummary'

$summaryFile = Join-Path $reportDir 'Summary.txt'
if (Test-Path $summaryFile) {
    Write-Host ''
    Write-Host '===== Coverage summary =====' -ForegroundColor Green
    Get-Content $summaryFile | Write-Host
    Write-Host '============================' -ForegroundColor Green
}

$indexHtml = Join-Path $reportDir 'index.html'
Write-Host ''
Write-Host "HTML report: $indexHtml" -ForegroundColor Green
