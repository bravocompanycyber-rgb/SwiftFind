$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$b64Path = Join-Path $root "assets\SwiftFind.icon.b64"
$icoPath = Join-Path $root "assets\SwiftFind.ico"
if (-not (Test-Path $b64Path)) {
  Write-Host "No SwiftFind.icon.b64; skipping icon."
  exit 0
}
Add-Type -AssemblyName System.Drawing
$raw = ((Get-Content -Path $b64Path -Raw) -replace '\s','')
$bytes = [Convert]::FromBase64String($raw)
$ms = New-Object System.IO.MemoryStream(,$bytes)
$src = [System.Drawing.Image]::FromStream($ms)

$sizes = @(16,24,32,48,64,96)
$pngs = New-Object System.Collections.Generic.List[byte[]]
foreach ($s in $sizes) {
  $bmp = New-Object System.Drawing.Bitmap([int]$s, [int]$s)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $g.Clear([System.Drawing.Color]::Transparent)
  $g.DrawImage($src, 0, 0, [int]$s, [int]$s)
  $g.Dispose()
  $out = New-Object System.IO.MemoryStream
  $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
  $pngs.Add($out.ToArray())
  $out.Dispose()
  $bmp.Dispose()
}
$src.Dispose()
$ms.Dispose()

$buf = New-Object System.Collections.Generic.List[byte]
function Add-U16([System.Collections.Generic.List[byte]]$list, [int]$v) {
  $list.Add([byte]($v -band 255))
  $list.Add([byte](($v -shr 8) -band 255))
}
function Add-U32([System.Collections.Generic.List[byte]]$list, [int]$v) {
  $list.Add([byte]($v -band 255))
  $list.Add([byte](($v -shr 8) -band 255))
  $list.Add([byte](($v -shr 16) -band 255))
  $list.Add([byte](($v -shr 24) -band 255))
}
Add-U16 $buf 0
Add-U16 $buf 1
Add-U16 $buf $pngs.Count
$offset = 6 + (16 * $pngs.Count)
for ($i = 0; $i -lt $pngs.Count; $i++) {
  $s = [int]$sizes[$i]
  $dim = 0
  if ($s -lt 256) { $dim = $s }
  $buf.Add([byte]$dim)
  $buf.Add([byte]$dim)
  $buf.Add([byte]0)
  $buf.Add([byte]0)
  Add-U16 $buf 1
  Add-U16 $buf 32
  Add-U32 $buf $pngs[$i].Length
  Add-U32 $buf $offset
  $offset += $pngs[$i].Length
}
foreach ($p in $pngs) {
  foreach ($by in $p) { $buf.Add([byte]$by) }
}
[System.IO.File]::WriteAllBytes($icoPath, $buf.ToArray())
Write-Host "Wrote $icoPath ($($buf.Count) bytes)"
