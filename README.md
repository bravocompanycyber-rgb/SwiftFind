# SwiftFind

**Instant desktop search + lightweight file manager for Windows**

SwiftFind finds files and folders the moment you type — then lets you act on them like Explorer: open, rename, delete, copy, cut, paste, new folder, drag-out, and more.

Built to feel modern, stay fast, and run on everything from **Windows 8 / 8.1** through **Windows 11**.

---

## Why SwiftFind?

Most search tools stop at “find it”.  
SwiftFind continues: **find it → work with it**.

| Feature | SwiftFind | Typical search tools |
|---------|-----------|----------------------|
| Instant name + path search | Yes | Yes |
| Multi-select | Yes | Rare |
| Full Explorer-style context menu | Yes | Limited |
| Rename / Delete / New folder | Yes | No |
| Copy / Cut / Paste (with Explorer interop) | Yes | No |
| Drag files out to Explorer or other apps | Yes | Rare |
| Size / Date / Extension filters | Yes | Partial |
| Persistent local index | Yes | Yes |
| Works on Windows 8 → 11 | Yes | Varies |
| Zero installer needed (single EXE) | Yes | Varies |
| Dark modern UI | Yes | Mixed |

### Unique advantages
- **Search + manage in one place** — no need to jump back to Explorer for everyday actions.
- **Keyboard-first** — Ctrl+C / X / V, F2, Delete, Enter, Ctrl+A, Ctrl+F, Ctrl+R all work as expected.
- **Clean, distraction-free dark interface** designed for long sessions.
- **Lightweight** — pure .NET Framework WinForms, no Electron, no heavy runtime.

---

## Screenshots (conceptual)

- Large search bar at the top — results appear as you type.
- Filters for type, size and date sit just below the path bar.
- Results list shows Name · Location · Size · Modified · Type.
- Right-click any selection for the full action menu.
- Status bar always shows indexed count and current folder.

---

## Quick start

### Option A – Run the pre-built EXE
1. Download `SwiftFind.exe` from the repository.
2. Double-click. No installation required.
3. Click **Choose folder**, select a drive or directory, then **Build index**.
4. Start typing. Results appear instantly.

### Option B – Build from source
Requirements: Windows + .NET Framework 4.5 or later (included in Windows 8+).

```powershell
# From the repository root
.\scripts\Build.ps1
```

Or compile manually:

```powershell
csc /target:winexe /out:SwiftFind.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll src\SwiftFind.App\SwiftFind.cs
```

---

## Everyday workflow

1. **Index** once (or refresh when needed).
2. **Type** any part of the name or path — results update live.
3. **Filter** by extension, size or date if the list is large.
4. **Act**:
   - Double-click or Enter → open
   - F2 → rename
   - Delete → remove (with confirmation)
   - Ctrl+C / Ctrl+X → copy or cut
   - Ctrl+V → paste into the current/selected folder
   - Right-click → full menu (open location, new folder, properties, copy path…)
   - Drag selected items out to Explorer or other applications

Keyboard shortcuts:

| Shortcut | Action |
|----------|--------|
| `Ctrl+F` | Focus search box |
| `Ctrl+R` | Rebuild index |
| `Enter` | Open selected |
| `F2` | Rename |
| `Delete` | Delete |
| `Ctrl+C` | Copy |
| `Ctrl+X` | Cut |
| `Ctrl+V` | Paste |
| `Ctrl+A` | Select all visible |
| `Esc` | Clear search |

---

## Technical notes

- **Indexing**: recursive walk of the chosen root (skips reparse points / junctions). Progress is reported live. Index is cached under `%LocalAppData%\SwiftFind`.
- **Search**: case-insensitive AND matching across file name and full path. Results capped at 8 000 for responsiveness.
- **Compatibility**: targets .NET Framework 4.5+. Uses only APIs available since Windows 8. No Windows 10/11-only features required.
- **Permissions**: runs with the rights of the current user. Protected system folders may be skipped during indexing (normal behaviour).

---

## Roadmap ideas

- Optional USN Journal / MFT fast path for near-Everything speed on NTFS
- Content search (text inside files)
- Bookmarks / favorites
- Portable mode (settings next to the EXE)
- Light theme toggle

Contributions and ideas welcome.

---

## License

MIT — free for personal and commercial use.

---

**SwiftFind** — find anything, then do something with it.
