# Changelog

## 3.0.0

A ground-up quality pass: new design, new brand assets, many correctness fixes.

### Design
- New dark UI built on a single theme: rounded search field, flat buttons with hover states, themed menus, dialogs, drop-downs, dark title bar and scrollbars (Windows 10/11)
- Brand logo and full icon set (16-256 px) embedded in the exe; header logo and About dialog
- Real per-file-type shell icons in the results list; roomier rows
- Helpful empty states ("No index yet", "No results", invalid regex message)
- Window size, position, maximised state and sort order are remembered
- Per-monitor high-DPI support (manifest + DPI-aware layout)

### Everyday use
- Global hotkey `Ctrl+Alt+Space` (falls back gracefully if another program already owns it) and optional close-to-tray with a tray menu
- Starting SwiftFind again focuses the running instance instead of showing an error

### Search
- Quoted phrases, exclusions (`!word` / `-word`), `path:`, `file:`, `folder:`, `case:`, `regex:pattern`, size ranges (`1mb..5mb`, `>=`, `<=`), date ranges and `dm:yesterday/month/year`
- Search runs on a background thread and is cancelled when you keep typing — no more UI stalls on large indexes
- Patterns are compiled once instead of per file; no more double sorting
- Invalid regular expressions now show a message instead of silently falling back
- Result cap is reported honestly ("first 5,000 of 123,456 matches")

### Indexing
- **Fixed:** NTFS fast index resolved paths while enumerating, but the MFT is not parents-first, so many paths came out wrong. Paths are now resolved in a second pass
- **Fixed:** fast-index entries had size 0 and no date, breaking size/date filters. Details are now read in parallel in the background
- **Fixed:** root-folder filter treated `C:\Users2` as inside `C:\Users`
- **Fixed:** exclusion matching had the same prefix bug
- Cancel button during indexing; USN failure reason is shown (e.g. "administrator rights required")
- Folder scan is faster (one directory listing per folder instead of one call per file) and no longer keeps a huge duplicate-tracking set
- Binary index cache (much faster to load), loaded in the background at startup, written atomically

### File operations
- **Delete now goes to the Recycle Bin** (previously permanent)
- **Fixed:** cut/paste copied then deleted; it is now a real move on the same volume
- **Fixed:** pasting a folder into itself recursed; it is now blocked
- Rename validates the name, detects collisions, supports case-only renames
- Rename / delete / paste keep the index consistent, including folder contents
- Favourites can be removed again, and favourites view is a filter that works with search
- Drag files out of the results into other apps; export results to CSV

### Reliability & housekeeping
- Single settings/data path implementation; atomic settings writes; invalid values no longer reset limits to 0
- Global crash handler with `crash.log` instead of silent termination; single-instance guard
- **Fixed:** garbled characters (`â€¦`, `â˜…`) in menus and buttons
- **Fixed:** About box showed a stale version; it now reads the assembly version
- **Removed from git:** build output (`obj/`), compiled binaries and the `backups/` folder (still in history); added `.gitignore`
- Builds now go to `bin/Release/net48` instead of the repository root
- CI: restore-safe `dotnet build`, unit tests, version from tag, SHA-256 checksum. The icon is no longer regenerated (and overwritten) on every build
- Unit tests for the search engine (44 cases)

## 2.7.0 and earlier
See git history.
