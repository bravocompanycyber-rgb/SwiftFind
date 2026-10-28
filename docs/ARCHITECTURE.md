# SwiftFind Architecture

## Overview

SwiftFind is a single-process WinForms desktop application targeting .NET Framework 4.8.
It combines high-performance filename search with in-app file management.

## Layers

### UI (SwiftFind.cs – MainForm)
- Search box with debounced live filtering
- Path bar + action buttons
- Filter combos (type / size / date)
- Results ListView with multi-select, column sorting, drag-out
- Context menu + keyboard shortcuts
- Status bar with index method and counts

### Search (Search/SearchEngine.cs)
- Query parser supporting:
  - Free-text terms (AND)
  - Wildcards (`*`, `?`)
  - Operators: `ext:`, `size:>`, `size:<`, `dm:`, `regex:`
- Execution pipeline with early filtering and result capping

### Indexing
- **UsnIndexer** – NTFS USN Journal (`FSCTL_ENUM_USN_DATA`). Extremely fast on large volumes when the process has sufficient rights. Returns null on failure so the caller can fall back.
- **RecursiveIndexer** – Classic stack-based directory walk. Skips reparse points. Always available.

### Models
- `FileEntry` – lightweight record (Name, FullPath, Directory, Size, Modified, IsDirectory)

### Persistence
- Index cache (binary-ish text under `%LocalAppData%\SwiftFind` or portable folder)
- Favorites list
- Last root + index method settings

## Design decisions

1. **WinForms + .NET Framework 4.8** – maximum compatibility (Windows 8 through 11) without requiring the modern .NET runtime.
2. **USN first, recursive fallback** – users get Everything-like speed when possible; everyone still gets a working index.
3. **In-app file operations** – the main competitive gap versus pure search tools.
4. **No external dependencies** – single EXE, no installers required for basic use.

## Future extensions

- FileSystemWatcher incremental updates for the recursive path
- Optional content search (text files / simple binary headers)
- Multiple simultaneous roots
- Light theme
