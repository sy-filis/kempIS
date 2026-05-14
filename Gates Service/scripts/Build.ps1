#Requires -Version 5.1

<#
.SYNOPSIS
    Publishes KempISGatesService into <repo-root>/bin as a self-contained,
    single-file executable.

.DESCRIPTION
    Wraps `dotnet publish` with the project's standard configuration
    (Release, single-file, self-contained, win-x86, embedded PDB).
    The destination is cleaned first so the output never contains stale
    files from a previous publish.

.PARAMETER OutputPath
    Where to place the published artifacts. Defaults to <repo-root>/bin.
    Must be inside the repo root for the clean step to run.

.PARAMETER Configuration
    MSBuild configuration. Defaults to Release.

.EXAMPLE
    .\Publish-Service.ps1
#>
[CmdletBinding()]
param(
    [Parameter()]
    [string]$OutputPath,

    [Parameter()]
    [string]$Configuration = 'Release'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).ProviderPath
$projectPath = Join-Path $repoRoot 'KempISGatesService\KempISGatesService.csproj'

if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "Project not found: $projectPath"
}

if (-not $OutputPath) {
    $OutputPath = Join-Path $repoRoot 'bin'
}
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)

# Refuse to wipe anything outside the repo.
if (-not $resolvedOutput.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputPath '$resolvedOutput' is outside the repo root '$repoRoot'."
}

if (Test-Path -LiteralPath $resolvedOutput) {
    Write-Host "Cleaning $resolvedOutput"
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}

Write-Host "Publishing $projectPath"
Write-Host "  Configuration : $Configuration"
Write-Host "  Output        : $resolvedOutput"
Write-Host ""

& dotnet publish $projectPath -c $Configuration -o $resolvedOutput --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$exePath = Join-Path $resolvedOutput 'KempISGatesService.exe'
if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) {
    throw "Publish completed but '$exePath' was not produced."
}

Write-Host ""
Write-Host "Published to: $resolvedOutput" -ForegroundColor Green
Get-ChildItem -LiteralPath $resolvedOutput | Format-Table Name, Length, LastWriteTime
