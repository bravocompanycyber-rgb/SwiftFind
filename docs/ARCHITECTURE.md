# SwiftFind architecture

## Data flow

```
 settings.ini / index.cache / favorites.txt          (Core.AppPaths)
            |
            v
   MainForm  --(Build index)-->  UsnIndexer (NTFS + admin)  --fails--> RecursiveIndexer
      |                                |
      |                                v
      |                        DetailsEnricher  (parallel size/date fill-in)
      |                                |
      |        FileEntry[]  (immutable snapshot, swapped atomically)
      |                                |
      +--(each keystroke, debounced 90 ms)--> SearchEngine.Parse -> Execute (background, cancellable)
                                       |
                                       v
                          ListView (cap = MaxResults)  +  IconCache
```

## Key decisions

* **Index is a snapshot array.** Searches run on a background thread against `FileEntry[]`. Edits (rename, delete, paste) build a new array and swap the reference, so a running search never sees a half-edited list.
* **Search engine is UI-free** (`Search/SearchEngine.cs`) and unit-tested in `tests/SwiftFind.Tests`.
* **Fast index is two-pass.** The MFT is not enumerated parents-first, so pass 1 collects every record (file reference -> parent, name, is-directory); pass 2 resolves directory paths with memoisation. File references are masked to 48 bits (the top 16 bits are a sequence number).
* **USN records carry no size or timestamp.** Entries start with `Size = -1` (unknown) and `DetailsEnricher` fills them in parallel after the index is already searchable. The cache is rewritten when it finishes.
* **Large result sets are capped** at `MaxResults` (default 5,000) for list rendering speed. Above 250,000 matches only the returned page is sorted.
* **Deletes are recoverable** (Recycle Bin). Cut/paste uses real moves.

## Partial classes of `MainForm`

| File | Responsibility |
|---|---|
| `MainForm.cs` | menu, layout, window state |
| `MainForm.Search.cs` | query building, async search, list population, sorting, keyboard, help, CSV export |
| `MainForm.Index.cs` | indexing, enrichment, cache, favourites, preferences |
| `MainForm.Actions.cs` | context menu, open/reveal, clipboard, rename, delete, drag-out |

## Not covered by automated tests
Win32-specific code (USN enumeration, shell icons, Recycle Bin, DWM dark title bar) can only run on Windows. The search engine, which carries most of the logic, is covered by tests.
