# SwiftFind

**Professional instant desktop search + Explorer-like file manager for Windows**

Find anything in milliseconds. Then act on it — open, rename, delete, copy, cut, paste, new folder, drag-out, favorites — without leaving the window.

Built for **Windows 8 / 8.1 through Windows 11**. Optional **USN Journal fast path** on NTFS. Fully configurable exclusions, grouping, color coding and preferences.

---

## Why teams and power users choose SwiftFind

Most search tools stop at “find it”.  
SwiftFind continues: **find it → work with it**.

| Capability | SwiftFind | Typical alternatives |
|------------|-----------|----------------------|
| Instant name + path search | Yes | Yes |
| Full in-app file management | **Yes** | Rare / limited |
| Multi-select, drag-out, clipboard interop | **Yes** | Partial |
| User-defined folder & extension exclusions | **Yes** | Limited |
| Grouping (Extension / Folder / Date / Size) | **Yes** | Rare |
| Color coding (folders, favorites, large, recent) | **Yes** | Rare |
| Preferences dialog + persistent settings | **Yes** | Varies |
| USN fast path + recursive fallback | **Yes** | Specialized tools only |
| Professional menu system (File / Edit / View / Tools) | **Yes** | Basic or none |
| Portable mode | **Yes** | Some |
| Windows 8 → 11, single EXE, no heavy runtime | **Yes** | Varies |

---

## Highlights

### Search that stays out of your way
- Live results as you type
- Advanced operators: `ext:pdf`, `size:>10MB`, `dm:week`, wildcards, `regex:`
- Click any column to sort
- Group results by extension, folder, modified date or size range

### Real file management
- Open / Open location
- Rename (F2), Delete, New folder
- Copy / Cut / Paste with Explorer interop
- Drag selected items straight into Explorer or other apps
- Multi-select and Select All

### Control & polish
- **Preferences** (Ctrl+,) — exclusions, grouping defaults, color coding, max results, auto-index
- Exclude any folders and file types from the index
- Favorites (★ / Ctrl+D)
- Color coding: folders, favorites, large files (>100 MB), recent files
- Dark professional UI with proper menu bar
- Crash-hardened paths and background indexing

---

## Quick start

```powershell
# Modern
cd src\SwiftFind.App
dotnet build -c Release

# Or classic script
.\scripts\Build.ps1

.\SwiftFind.exe
```

1. **Choose folder** (or whole drive)
2. Open **Tools → Preferences** and set exclusions (highly recommended)
3. **Build index** (USN is tried first on NTFS)
4. Type to search. Right-click or use shortcuts to manage files.

---

## Keyboard shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+F` | Focus search |
| `Ctrl+R` | Rebuild index |
| `Ctrl+,` | Preferences |
| `Ctrl+D` | Add to Favorites |
| `Enter` | Open |
| `F2` | Rename |
| `Delete` | Delete |
| `Ctrl+C / X / V` | Copy / Cut / Paste |
| `Ctrl+A` | Select all visible |
| `Esc` | Clear search |

---

## Query examples

```
report
*2024*.pdf
ext:pdf size:>5MB
dm:week invoice
regex:^Draft.*\.docx$
```

---

## Technical

- **Target**: .NET Framework 4.8 (Windows 8+ compatible APIs)
- **Indexing**: USN Journal when available → recursive walk with exclusions otherwise
- **Settings & cache**: `%LocalAppData%\SwiftFind` or next to EXE in portable mode
- **No external dependencies**, no installer required for basic use

---

## License

MIT — free for personal and commercial use.

**SwiftFind** — professional search that lets you finish the job.
