$ErrorActionPreference = 'Stop'

$inno = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
$iss = Join-Path $PSScriptRoot 'installer\ClawJump-Avalonia.iss'

if (!(Test-Path $inno)) {
    Write-Host 'Inno Setup not found:' -ForegroundColor Red
    Write-Host $inno
    Write-Host 'Please install Inno Setup 6 first.'
    exit 1
}

Write-Host 'Using Inno Setup:' -ForegroundColor Green
Write-Host $inno

Write-Host 'Stopping running ClawJump process...' -ForegroundColor Cyan
Get-Process ClawJump -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

Write-Host 'Step 1: publish Avalonia release...' -ForegroundColor Cyan
& (Join-Path $PSScriptRoot 'build-avalonia-release.ps1')

Write-Host ''
Write-Host 'Step 2: build installer...' -ForegroundColor Cyan
& $inno $iss

Write-Host ''
Write-Host 'Installer completed:' -ForegroundColor Green
Write-Host (Join-Path $PSScriptRoot 'installer-output\ClawJump-Avalonia-Setup-0.2.0.exe')