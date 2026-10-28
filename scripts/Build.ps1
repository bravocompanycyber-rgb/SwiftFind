# SwiftFind build script
# Compatible with Windows PowerShell 3+ and PowerShell 7+
# Requires .NET Framework 4.5+ (present on Windows 8 and later)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$src  = Join-Path $root "src\SwiftFind.App\SwiftFind.cs"
$out  = Join-Path $root "SwiftFind.exe"

if (-not (Test-Path $src)) {
    Write-Error "Source not found: $src"
}

Write-Host "Building SwiftFind..." -ForegroundColor Cyan
Write-Host "  Source : $src"
Write-Host "  Output : $out"

# Prefer the highest available csc
$cscCandidates = @(
    "${env:WINDIR}\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "${env:WINDIR}\Microsoft.NET\Framework\v4.0.30319\csc.exe",
    "${env:WINDIR}\Microsoft.NET\Framework64\v3.5\csc.exe",
    "${env:WINDIR}\Microsoft.NET\Framework\v3.5\csc.exe"
)

$csc = $null
foreach ($c in $cscCandidates) {
    if (Test-Path $c) { $csc = $c; break }
}

if (-not $csc) {
    Write-Error "csc.exe not found. Install .NET Framework 4.5 or later / Windows SDK."
}

Write-Host "  Compiler: $csc"

& $csc /nologo /target:winexe /optimize+ /out:"$out" `
    /r:System.dll `
    /r:System.Windows.Forms.dll `
    /r:System.Drawing.dll `
    /r:System.Core.dll `
    "$src"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
}

if (Test-Path $out) {
    $size = (Get-Item $out).Length
    Write-Host ""
    Write-Host "Build succeeded." -ForegroundColor Green
    Write-Host "  $out  ($([math]::Round($size/1KB,1)) KB)"
    Write-Host ""
    Write-Host "Run it with:  .\SwiftFind.exe" -ForegroundColor Yellow
} else {
    Write-Error "Output EXE was not created."
}
