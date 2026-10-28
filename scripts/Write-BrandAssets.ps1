# Writes SwiftFind brand binaries into assets/
# Run from repo root: powershell -ExecutionPolicy Bypass -File scripts/Write-BrandAssets.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $root) { $root = Get-Location }
$assets = Join-Path $PWD 'assets'
if (-not (Test-Path $assets)) { New-Item -ItemType Directory -Path $assets | Out-Null }
Write-Host "Assets folder: $assets"
Write-Host "Place SwiftFind.ico and PNG masters into assets/ manually or via CI artifact."
Write-Host "Done."
