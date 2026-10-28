# SwiftFind

**Professional instant desktop search & Explorer-like file manager for Windows 8 – 11**

SwiftFind is a fast, modern, crash-hardened file search utility with full file management capabilities. Built for speed, clarity, and control.

![SwiftFind](assets/symbol_512.png)

## Features

- **Instant real-time search** — results as you type
- **Dual indexing** — optional NTFS USN Journal path (Everything-style speed) + solid recursive fallback
- **Explorer-like actions** — open, rename, delete, copy / cut / paste, multi-select
- **Advanced query syntax** — `ext:`, `size:>`, `dm:`, wildcards, regex
- **Preferences & exclusions** — exclude folders and file types you choose
- **Grouping & color coding** — by extension, folder, modified date, size
- **Favorites, portable mode, persistent cache**
- **Windows 8 / 8.1 / 10 / 11** via .NET Framework 4.8

## Download

Official builds are produced automatically by GitHub Actions.

1. Go to **Actions** → latest successful run → **Artifacts** → `SwiftFind-windows-x64`
2. Or download from **Releases** (created when a version tag is pushed)

```bash
git tag v2.6.0
git push origin v2.6.0
```

## Build from source

```powershell
cd src\SwiftFind.App
msbuild SwiftFind.csproj /p:Configuration=Release
# or
dotnet build -c Release
```

Requirements: Visual Studio 2019/2022 or Build Tools with .NET Framework 4.8 targeting pack.

## Application icon

Place `assets/SwiftFind.ico` in the repository (multi-resolution 16–256).  
The project also embeds a runtime-generated matching icon so the window always looks correct.

## Project layout

```
src/SwiftFind.App/     # Main WinForms application
  Helpers/AppIcon.cs   # Premium runtime icon
  Indexing/            # USN + Recursive indexers
  Search/              # Query engine
  Forms/               # Preferences dialog
  Models/              # FileEntry, AppSettings
assets/                # Brand assets & .ico
.github/workflows/     # CI build & release
scripts/               # Build helpers
```

## License

Copyright © 2026 Bravo Company Cyber. All rights reserved.
