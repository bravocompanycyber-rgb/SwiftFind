# SwiftFind

**Instant desktop search + Explorer-like file manager for Windows**

Find anything in milliseconds, then act on it without leaving the app: open, rename, delete, copy, cut, paste, new folder, drag-out, favorites.

Runs on **Windows 8 / 8.1 → Windows 11**. Optional **USN Journal fast path** on NTFS for near-Everything speed.

---

## Why SwiftFind beats the others

| Capability | SwiftFind | Everything | Listary | Agent Ransack |
|------------|-----------|------------|---------|---------------|
| Instant name/path search | Yes | Excellent | Good | Good |
| Full file management (rename/delete/copy/cut/paste/new folder) | **Yes** | Limited | Limited | No |
| Multi-select + drag-out to Explorer | **Yes** | Limited | Partial | No |
| Favorites / bookmarks | **Yes** | No | Partial | No |
| Advanced query syntax (`ext:`, `size:>`, `dm:`, wildcards, regex) | **Yes** | Yes | Partial | Yes |
| USN / MFT fast indexing | **Yes** (with recursive fallback) | Yes | No | No |
| Column sorting | **Yes** | Yes | Limited | Yes |
| Portable mode | **Yes** | Yes | No | Yes |
| Windows 8 → 11 | **Yes** | Yes | Yes | Yes |
| Dark modern UI | **Yes** | Basic | Good | Basic |
| Zero heavy runtime | **Yes** (.NET Framework) | Yes | Yes | Yes |

**Gap we close:** most search tools stop at “find it”. SwiftFind continues — **find it → work with it** — with a full Explorer-style action surface inside the same window.

---

## Features

### Search
- Instant as-you-type results
- Name + full path matching
- Wildcards (`*report*`, `file?.txt`)
- Operators: `ext:pdf`, `size:>10MB`, `size:<1KB`, `dm:today`, `dm:week`, `dm:month`, `dm:year`, `regex:`
- UI filters for type / size / modified date
- Click any column header to sort

### File management (the differentiator)
- Open / Open containing folder
- Rename (F2)
- Delete (with confirmation)
- Copy / Cut / Paste (interop with Explorer clipboard)
- New folder
- Drag selected items out to Explorer or other apps
- Multi-select + Select All
- Copy full path(s)
- Properties summary

### Other
- Favorites (★ / Ctrl+D)
- Persistent index cache
- USN Journal fast path on NTFS (falls back to recursive walk automatically)
- Portable mode (create empty `SwiftFind.portable` next to the EXE)
- Clean dark UI, keyboard-first workflow

---

## Keyboard shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+F` | Focus search |
| `Ctrl+R` | Rebuild index |
| `Ctrl+D` | Add to Favorites |
| `Enter` | Open |
| `F2` | Rename |
| `Delete` | Delete |
| `Ctrl+C / X / V` | Copy / Cut / Paste |
| `Ctrl+A` | Select all visible |
| `Esc` | Clear search |
| `↓` from search box | Jump to results |

---

## Quick start

### Build with Visual Studio / modern tooling
```bash
cd src/SwiftFind.App
dotnet build -c Release
```
(or open `SwiftFind.csproj` in Visual Studio)

### Classic build script
```powershell
.\scripts\Build.ps1
```

### Run
```
SwiftFind.exe
```
1. Choose a folder or drive
2. Click **Build index** (USN is tried first on NTFS)
3. Type to search — results appear instantly
4. Right-click or use shortcuts to manage files

---

## Advanced query examples

```
report                          # name or path contains "report"
*2024*.pdf                      # wildcards
ext:pdf size:>5MB               # PDFs larger than 5 MB
dm:week invoice                 # modified in last 7 days + "invoice"
regex:^Draft.*\.docx$           # regular expression
```

---

## Technical notes

- **Target**: .NET Framework 4.8 (works on Windows 8+ with Framework installed; 4.5+ APIs only)
- **Indexing**: USN Journal (`FSCTL_ENUM_USN_DATA`) when available and permitted → otherwise recursive walk that skips reparse points
- **Cache**: `%LocalAppData%\SwiftFind` (or next to EXE in portable mode)
- **Permissions**: runs as the current user. System volumes may need elevation for full USN access; the app falls back gracefully.

---

## Project structure

```
src/SwiftFind.App/
  SwiftFind.csproj
  Program.cs
  SwiftFind.cs              ← MainForm (UI + orchestration)
  Models/FileEntry.cs
  Indexing/
    UsnIndexer.cs           ← Fast NTFS path
    RecursiveIndexer.cs     ← Reliable fallback
  Search/SearchEngine.cs    ← Query parser + execution
```

---

## License

MIT — free for personal and commercial use.

**SwiftFind** — find anything, then do something with it.
