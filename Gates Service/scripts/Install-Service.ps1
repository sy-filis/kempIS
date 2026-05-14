#Requires -Version 5.1
#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Registers KempISGatesService as a Windows service configured to start
    automatically at boot and restart on unexpected failure.

.DESCRIPTION
    Wraps the underlying sc.exe commands into a single idempotent
    operation. If a service with the same name already exists, it is
    stopped and removed before the new registration is created.

    Must be run from an elevated PowerShell prompt.

.PARAMETER BinPath
    Absolute path to the published KempISGatesService.exe.

.PARAMETER ServiceName
    Windows service identifier. Defaults to 'KempISGatesService'.

.PARAMETER DisplayName
    Human-readable name shown in services.msc. Defaults to
    'Kemp IS Gates Service'.

.PARAMETER Description
    Service description shown in services.msc.

.EXAMPLE
    .\Install-Service.ps1 -BinPath "C:\KempISGatesService\KempISGatesService.exe"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BinPath,

    [Parameter()]
    [string]$ServiceName = 'KempISGatesService',

    [Parameter()]
    [string]$DisplayName = 'Kemp IS Gates Service',

    [Parameter()]
    [string]$Description = 'HTTP API that upserts and deletes cards in the legacy Jet 4.0 (Access) users/events databases used by the Kemp IS gate system.'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Sc {
    param([string[]]$Arguments)

    $output = & sc.exe @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "sc.exe $($Arguments -join ' ') failed (exit $LASTEXITCODE):`n$output"
    }
    return $output
}

# ---- Validate input -------------------------------------------------------

if (-not (Test-Path -LiteralPath $BinPath -PathType Leaf)) {
    throw "BinPath does not exist or is not a file: $BinPath"
}

$resolved = (Resolve-Path -LiteralPath $BinPath).ProviderPath
if ([System.IO.Path]::GetExtension($resolved) -ne '.exe') {
    throw "BinPath must point to a .exe file: $resolved"
}

Write-Host "Installing service '$ServiceName'"
Write-Host "  Binary : $resolved"

# ---- Ensure the Windows event source exists -------------------------------

# Serilog's EventLog sink runs with manageEventSource=false at runtime, so the
# source must already be registered when the service first starts. Creating
# the source requires admin rights — done here, once, by the installer.
$eventLogName = 'Application'
if (-not [System.Diagnostics.EventLog]::SourceExists($ServiceName)) {
    Write-Host "Registering event source '$ServiceName' in '$eventLogName' log"
    [System.Diagnostics.EventLog]::CreateEventSource($ServiceName, $eventLogName)
}
else {
    Write-Host "Event source '$ServiceName' already exists"
}

# ---- Remove any existing service with the same name -----------------------

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -ne $existing) {
    Write-Host "Existing service found. Stopping and removing..."
    if ($existing.Status -ne 'Stopped') {
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        $existing.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(10))
    }
    Invoke-Sc @('delete', $ServiceName) | Out-Null

    # Allow SCM a moment to release the name before we recreate.
    Start-Sleep -Milliseconds 500
}

# ---- Create, describe, configure recovery, start --------------------------

Write-Host "Creating service with start= auto"
Invoke-Sc @(
    'create', $ServiceName,
    'binPath=', $resolved,
    'start=',   'auto',
    'DisplayName=', $DisplayName
) | Out-Null

Write-Host "Setting description"
Invoke-Sc @('description', $ServiceName, $Description) | Out-Null

# reset=  86400   — forget prior failures after 24 h
# actions= restart/60000/restart/60000/restart/60000
#   → SCM restarts the service three times with a 60 s delay each.
Write-Host "Configuring recovery (restart three times, 60 s delay)"
Invoke-Sc @(
    'failure', $ServiceName,
    'reset=',   '86400',
    'actions=', 'restart/60000/restart/60000/restart/60000'
) | Out-Null

Write-Host "Starting service"
Invoke-Sc @('start', $ServiceName) | Out-Null

# ---- Wait for Running -----------------------------------------------------

$svc = Get-Service -Name $ServiceName
try {
    $svc.WaitForStatus('Running', [TimeSpan]::FromSeconds(10))
}
catch [System.ServiceProcess.TimeoutException] {
    $svc.Refresh()
    throw "Service did not reach Running state within 10 s (current: $($svc.Status)). Check the Application event log for source '$ServiceName'."
}

Write-Host ""
Write-Host "Service '$ServiceName' is running." -ForegroundColor Green
Write-Host "Check Event Viewer → Windows Logs → Application (source '$ServiceName') for startup messages."
Write-Host "The listen URL is controlled by the 'Urls' key in appsettings.json next to the binary."
