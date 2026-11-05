<p align="center">
  <img src="assets/symbol_512.png" alt="SwiftFind" width="160">
</p>

<h1 align="center">SwiftFind</h1>
<p align="center"><b>Fast desktop search &amp; indexing for Windows</b></p>

SwiftFind indexes **everything on your PC** — every local drive (and USB drives, if you like) — and searches it all at once as you type. You never have to pick a folder; if you prefer, you can limit it to one folder from the *Search scope* button. On NTFS drives it reads the Master File Table directly (run as administrator), so even millions of files index in seconds. On other drives it falls back to a safe folder scan.

## Features

- **Search everywhere by default** — all drives indexed automatically on first run, kept fresh in the background
- **Instant search** while typing, on a background thread, so the window never freezes
- **NTFS fast index** (MFT / USN) with automatic fallback to a folder scan
- **Powerful query syntax** — wildcards, regex, size, date, extension, path, exclusions (see below)
- **File actions** — open, open with, show in folder, copy / cut / paste, rename, **move to Recycle Bin**, properties, drag files out to other apps
- **Favourites**, grouping (type, folder, date, size), colour coding, sortable columns
- **Export results to CSV**
- **Dark, modern UI** with brand icon, per-type shell icons, per-monitor high-DPI support
- **Portable mode** — create an empty file named `SwiftFind.portable` next to the exe to keep all data beside it
- **Global hotkey** `Ctrl+Alt+Space` brings SwiftFind to the front from anywhere; optional **close-to-tray** (Preferences)
- Launching SwiftFind a second time brings the running window forward instead of opening a duplicate
- Saved index loads on startup so search is available immediately

## Download

Grab `SwiftFind-portable-windows.zip` (or `SwiftFind.exe`) from the [Releases](../../releases) page. Requires Windows 7 or later with .NET Framework 4.8 (built into Windows 10 1903+ and Windows 11).

> **Tip:** use *File → Restart as administrator* once to unlock the NTFS fast index (an entire drive in seconds). Without it SwiftFind still works, but scanning a whole drive takes minutes instead.

## Search syntax

| You type | Meaning |
|---|---|
| `invoice 2026` | all words must match (name, or full path) |
| `"annual report"` | exact phrase |
| `report !draft` or `report -draft` | exclude matches |
| `*.pdf`, `img_00?.jpg` | wildcards |
| `ext:jpg;png` | one or more extensions |
| `size:>10mb`, `size:<=1kb`, `size:1mb..5mb` | size filters |
| `dm:today`, `dm:week`, `dm:2026-03-01`, `dm:>2026-01-01`, `dm:2026-01-01..2026-02-01` | modified date (`yesterday`, `month`, `year` also work) |
| `path:projects` | match the full path only |
| `file:` / `folder:` | only files / only folders |
| `regex:^img_\d+` | regular expression (or `regex:` alone to treat every word as regex) |
| `case:` | case-sensitive |

Combine freely: `budget ext:xlsx dm:month !old`

## Keyboard shortcuts

| Key | Action |
|---|---|
| `Ctrl+F` / `Ctrl+L` | focus search |
| `↓` / `Enter` in search | move into results / open first result |
| `Enter` / `Ctrl+Enter` | open / show in folder |
| `Ctrl+R` / `F5` | build or cancel index |
| `Ctrl+B` | favourites only |
| `Ctrl+D` | toggle favourite |
| `Ctrl+C` / `Ctrl+X` / `Ctrl+V` | copy / cut / paste files |
| `Ctrl+Shift+C` | copy full path |
| `F2` / `Del` | rename / move to Recycle Bin |
| `Alt+Enter` | properties |
| `Ctrl+E` | export results to CSV |
| `F1` | search syntax help |
| `Ctrl+Alt+Space` | global hotkey: bring SwiftFind to the front (can be turned off in Preferences) |

## Where data lives

| Mode | Location |
|---|---|
| Installed (default) | `%LOCALAPPDATA%\SwiftFind` |
| Portable | next to `SwiftFind.exe` (when `SwiftFind.portable` exists) |

Files: `settings.ini`, `index.cache`, `favorites.txt`, `crash.log`.

## Build from source

Requires the .NET SDK (8.x is fine) on Windows.

```powershell
pwsh scripts/Build.ps1        # runs tests, then builds Release
pwsh scripts/Build.ps1 -Run   # ...and launches it
```

Output: `src/SwiftFind.App/bin/Release/net48/SwiftFind.exe`

Releases are produced by GitHub Actions: push a tag such as `v3.0.0` and the workflow tests, builds and publishes the zip with a SHA-256 checksum.

## Repository layout

```
assets/                  logo + icon set (embedded into the exe)
src/SwiftFind.App/       the application
  Core/                  paths, logging, Win32 helpers, theme
  Controls/              flat button, search box
  Forms/                 preferences, dialogs
  Indexing/              USN (MFT) indexer, recursive indexer, details enricher
  Search/                query parser + matcher (UI-free, unit-tested)
  Services/              index cache, file operations, icon cache
tests/SwiftFind.Tests/   dependency-free unit tests
tools/NativeMftProbe/    stand-alone MFT diagnostic (not part of the app)
docs/ARCHITECTURE.md     how it fits together
```

## Troubleshooting

- **"NTFS fast index unavailable"** — run as administrator, and make sure the folder is on an NTFS drive.
- **Something crashed** — *Help → View error log* (or open `crash.log` in the data folder).
- **Index looks stale** — press `Ctrl+R`. Rename, delete and paste inside SwiftFind update the index automatically; changes made elsewhere need a rebuild.
