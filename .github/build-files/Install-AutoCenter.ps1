<#
    Auto Center sideload installer.
    Imports the bundled signing certificate into LocalMachine\Root,
    installs the .msixbundle, and launches the app.

    Run via AutoCenter_Installer.bat (which self-elevates).
#>

$ErrorActionPreference = 'Stop'

$payload = Join-Path $PSScriptRoot 'SystemFiles'
if (-not (Test-Path $payload)) {
    Write-Host "ERROR: SystemFiles folder not found next to this script." -ForegroundColor Red
    exit 1
}

$cer = Get-ChildItem -Path $payload -Filter *.cer -File | Select-Object -First 1
$bundle = Get-ChildItem -Path $payload -Filter *.msixbundle -File | Select-Object -First 1

if (-not $cer)    { Write-Host 'ERROR: No .cer file found.' -ForegroundColor Red; exit 1 }
if (-not $bundle) { Write-Host 'ERROR: No .msixbundle file found.' -ForegroundColor Red; exit 1 }

Write-Host ''
Write-Host "Installing certificate: $($cer.Name)"
Import-Certificate -FilePath $cer.FullName -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
Write-Host '  Certificate trusted.' -ForegroundColor Green

$existing = Get-AppxPackage -Name 'JihedKdiss.AutoCenter' -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host ''
    Write-Host "Removing existing $($existing.PackageFullName)..."
    try {
        Remove-AppxPackage -Package $existing.PackageFullName -ErrorAction Stop
        Write-Host '  Existing package removed.' -ForegroundColor Green
    }
    catch {
        Write-Host "  Remove-AppxPackage failed: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

Write-Host ''
Write-Host "Installing package: $($bundle.Name)"
try {
    Add-AppxPackage -Path $bundle.FullName -ForceApplicationShutdown -ForceUpdateFromAnyVersion
    Write-Host '  Package installed.' -ForegroundColor Green
}
catch {
    Write-Host "  Add-AppxPackage failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ''
Write-Host 'Launching Auto Center...'
$pkg = Get-AppxPackage -Name 'JihedKdiss.AutoCenter' | Select-Object -First 1
if ($pkg) {
    Start-Process "shell:AppsFolder\$($pkg.PackageFamilyName)!App"
} else {
    Write-Host '  (Could not locate installed package; launch it from the Start menu.)' -ForegroundColor Yellow
}

Write-Host ''
Write-Host 'Auto Center is now installed.' -ForegroundColor Green
exit 0
