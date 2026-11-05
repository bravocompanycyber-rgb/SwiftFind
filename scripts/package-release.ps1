
$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root "src\SwiftFind.App\SwiftFind.csproj"
$Build = Join-Path $Root "src\SwiftFind.App\bin\Release\net48"
$Dist = Join-Path $Root "dist"
$Stage = Join-Path $Dist "SwiftFind-3.0.0-Portable"
$Zip = Join-Path $Dist "SwiftFind-3.0.0-Portable.zip"

Write-Host "`n====================================" -ForegroundColor Cyan
Write-Host " SwiftFind 3.0.0 Release Packaging" -ForegroundColor Cyan
Write-Host "====================================`n" -ForegroundColor Cyan

# 1. Build Release
Write-Host "[1/6] Building SwiftFind..." -ForegroundColor Yellow

dotnet build $Project -c Release

if ($LASTEXITCODE -ne 0) {
    throw "Release build failed."
}

# 2. Verify build output
$Exe = Join-Path $Build "SwiftFind.exe"
$Config = Join-Path $Build "SwiftFind.exe.config"

if (!(Test-Path $Exe)) {
    throw "SwiftFind.exe was not generated."
}

if (!(Test-Path $Config)) {
    throw "SwiftFind.exe.config is missing."
}

# 3. Prepare distribution directory
Write-Host "[2/6] Preparing distribution..." -ForegroundColor Yellow

New-Item -ItemType Directory -Force -Path $Dist | Out-Null

if (Test-Path $Stage) {
    Remove-Item $Stage -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $Stage | Out-Null

# 4. Copy application files
Write-Host "[3/6] Copying application files..." -ForegroundColor Yellow

Copy-Item $Exe $Stage
Copy-Item $Config $Stage
Copy-Item (Join-Path $Root "README.md") $Stage
Copy-Item (Join-Path $Root "CHANGELOG.md") $Stage

# Include license if the repository has one
$License = Join-Path $Root "LICENSE"
if (Test-Path $License) {
    Copy-Item $License $Stage
}

# Create distribution instructions
@"
SWIFTFIND 3.0.0
==============================

Thank you for downloading SwiftFind.

INSTALLATION:
1. Extract this ZIP into a folder.
2. Open the extracted folder.
3. Double-click SwiftFind.exe.

REQUIREMENTS:
- Windows 10 or Windows 11.
- Microsoft .NET Framework 4.8 or later.

IMPORTANT:
- Do not delete SwiftFind.exe.config.
- Your indexed data and settings are stored separately
  from the application installation directory.
- Run SwiftFind with appropriate Windows permissions
  for the folders and drives you want to index.

SUPPORT:
Consult README.md and CHANGELOG.md.

Version: 3.0.0
Product: SwiftFind
Publisher: Bravo Company Cyber
"@ | Set-Content (Join-Path $Stage "READ-ME-FIRST.txt") -Encoding UTF8

# 5. Create portable ZIP
Write-Host "[4/6] Creating portable ZIP..." -ForegroundColor Yellow

if (Test-Path $Zip) {
    Remove-Item $Zip -Force
}

Compress-Archive -Path (Join-Path $Stage "*") -DestinationPath $Zip -CompressionLevel Optimal

# 6. Generate checksum
Write-Host "[5/6] Generating SHA-256 checksum..." -ForegroundColor Yellow

$Hash = Get-FileHash $Zip -Algorithm SHA256

$HashLine = "$($Hash.Hash)  $([System.IO.Path]::GetFileName($Zip))"
$HashLine | Set-Content (Join-Path $Dist "SHA256SUMS.txt") -Encoding ASCII

Write-Host "[6/6] Release package created." -ForegroundColor Green

Write-Host "`n====================================" -ForegroundColor Green
Write-Host " RELEASE PACKAGE READY" -ForegroundColor Green
Write-Host "====================================" -ForegroundColor Green

Write-Host "`nPortable ZIP:"
Write-Host $Zip -ForegroundColor Cyan

Write-Host "`nSHA-256:"
Write-Host $Hash.Hash -ForegroundColor Cyan

Write-Host "`nDistribution folder:"
Write-Host $Dist -ForegroundColor Cyan
