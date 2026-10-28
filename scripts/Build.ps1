# SwiftFind local build script
$ErrorActionPreference = 'Stop'
$proj = Join-Path $PSScriptRoot '..\src\SwiftFind.App\SwiftFind.csproj'
Write-Host "Building $proj ..."
msbuild $proj /p:Configuration=Release /p:Platform="Any CPU" /v:m
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
Write-Host "Build succeeded."
$exe = Get-ChildItem -Path (Join-Path $PSScriptRoot '..') -Recurse -Filter 'SwiftFind.exe' | Select-Object -First 1
if ($exe) { Write-Host "Output: $($exe.FullName)" }
