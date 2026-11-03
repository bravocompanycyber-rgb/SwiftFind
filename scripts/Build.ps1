<#
 Local build + test helper.
   pwsh scripts/Build.ps1            # test, then build Release
   pwsh scripts/Build.ps1 -Run       # ...and launch the result
#>
param([switch]$Run)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

Write-Host "== Tests ==" -ForegroundColor Cyan
dotnet run --project "$root/tests/SwiftFind.Tests/SwiftFind.Tests.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw "Tests failed" }

Write-Host "== Build ==" -ForegroundColor Cyan
dotnet build "$root/src/SwiftFind.App/SwiftFind.csproj" -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$exe = "$root/src/SwiftFind.App/bin/Release/net48/SwiftFind.exe"
Write-Host "Built: $exe" -ForegroundColor Green
if ($Run) { Start-Process $exe }
