# SwiftFind build script
# Supports both classic csc and modern dotnet SDK

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$proj = Join-Path $root "src\SwiftFind.App\SwiftFind.csproj"
$outDir = $root

Write-Host "Building SwiftFind..." -ForegroundColor Cyan

# Prefer modern SDK if available
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnet) {
    Write-Host "  Using: dotnet build"
    & dotnet build $proj -c Release -o $outDir --nologo
    if ($LASTEXITCODE -ne 0) { Write-Error "dotnet build failed" }
} else {
    Write-Host "  Using: classic csc"
    $srcMain = Join-Path $root "src\SwiftFind.App\SwiftFind.cs"
    $srcProgram = Join-Path $root "src\SwiftFind.App\Program.cs"
    $srcModels = Join-Path $root "src\SwiftFind.App\Models\FileEntry.cs"
    $srcUsn = Join-Path $root "src\SwiftFind.App\Indexing\UsnIndexer.cs"
    $srcRec = Join-Path $root "src\SwiftFind.App\Indexing\RecursiveIndexer.cs"
    $srcSearch = Join-Path $root "src\SwiftFind.App\Search\SearchEngine.cs"
    $out = Join-Path $root "SwiftFind.exe"

    $cscCandidates = @(
        "${env:WINDIR}\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
        "${env:WINDIR}\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    )
    $csc = $null
    foreach ($c in $cscCandidates) { if (Test-Path $c) { $csc = $c; break } }
    if (-not $csc) { Write-Error "csc.exe not found. Install .NET Framework 4.x Developer Pack or the .NET SDK." }

    & $csc /nologo /target:winexe /optimize+ /out:"$out" `
        /r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll `
        "$srcMain" "$srcProgram" "$srcModels" "$srcUsn" "$srcRec" "$srcSearch"

    if ($LASTEXITCODE -ne 0) { Write-Error "csc build failed" }
}

$exe = Join-Path $outDir "SwiftFind.exe"
if (Test-Path $exe) {
    $size = (Get-Item $exe).Length
    Write-Host ""
    Write-Host "Build succeeded." -ForegroundColor Green
    Write-Host "  $exe  ($([math]::Round($size/1KB,1)) KB)"
    Write-Host ""
    Write-Host "Run:  .\SwiftFind.exe" -ForegroundColor Yellow
} else {
    Write-Error "Output EXE not found."
}
