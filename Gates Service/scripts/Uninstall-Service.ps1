#Requires -Version 5.1
#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Stops and unregisters the KempISGatesService Windows service.

.DESCRIPTION
    Idempotent: exits successfully if the service does not exist.
    Must be run from an elevated PowerShell prompt.

.PARAMETER ServiceName
    Windows service identifier. Defaults to 'KempISGatesService'.

.EXAMPLE
    .\Uninstall-Service.ps1
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$ServiceName = 'KempISGatesService'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -eq $svc) {
    Write-Host "Service '$ServiceName' is not installed. Nothing to do."
    exit 0
}

if ($svc.Status -ne 'Stopped') {
    Write-Host "Stopping service '$ServiceName'..."
    Stop-Service -Name $ServiceName -Force
    $svc.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(10))
}

Write-Host "Removing service '$ServiceName'..."
$output = & sc.exe delete $ServiceName 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "sc.exe delete failed (exit $LASTEXITCODE):`n$output"
}

# SCM marks the service for deletion; it disappears from Get-Service once
# all open handles release. Poll briefly so the script's exit reflects a
# clean removal.
$deadline = (Get-Date).AddSeconds(10)
while ((Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) -and ((Get-Date) -lt $deadline)) {
    Start-Sleep -Milliseconds 500
}

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Write-Host "Service '$ServiceName' is marked for deletion but still visible. It will disappear after all open handles close (services.msc open? event viewer?)." -ForegroundColor Yellow
}
else {
    Write-Host "Service '$ServiceName' removed." -ForegroundColor Green
}
