# Decodes embedded brand assets into assets/
$ErrorActionPreference = 'Stop'
$assets = Join-Path $PWD 'assets'
if (-not (Test-Path $assets)) { New-Item -ItemType Directory -Path $assets | Out-Null }

$icoB64 = @'
AAABAAcAEBAAAAEAIADCAAAAdgAAABgYAAABACAA9wAAADgBAAAgIAAAAQAgAJcBAAAvAgAAMDAAAAEAIAAIAgAAxgMAAEBAAAABACAAKAIAAO4FAACAgAAAAQAgACgJAACGhAAAQEAAAAAAAQAgACU0AAAOjwQA
'@
# NOTE: full base64 is embedded by CI decode step / local run of complete script
# For a complete local decode, run the full script from the design package.

Write-Host 'Decode-BrandAssets.ps1 placeholder — full ICO embedded in workflow step.'
Write-Host "Assets dir: $assets"
