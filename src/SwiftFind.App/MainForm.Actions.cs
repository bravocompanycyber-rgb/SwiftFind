using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SwiftFind.Core;
using SwiftFind.Forms;
using SwiftFind.Models;
using SwiftFind.Services;

namespace SwiftFind
{
    public partial class MainForm
    {
        const int DropEffectCopy = 1, DropEffectMove = 2;

        List<FileEntry> SelectedEntries()
        {
            return resultsView.SelectedItems.Cast<ListViewItem>().Select(i => (FileEntry)i.Tag).ToList();
        }

        // ================================================================ context menu

        void BuildContextMenu()
        {
            rowMenu = new ContextMenuStrip { Renderer = new DarkRenderer(), BackColor = Theme.Panel, ForeColor = Theme.Text, Font = Theme.Ui(9.5f) };
            miOpenWith = Item("Open with\u2026", Keys.None, (s, e) => OpenWith());
            miFavorite = Item("Add to favourites", Keys.Control | Keys.D, (s, e) => ToggleFavoriteSelected());
            miRename = Item("Rename", Keys.F2, (s, e) => RenameSelected());
            miPaste = Item("Paste here", Keys.Control | Keys.V, (s, e) => PasteHere());

            rowMenu.Items.AddRange(new ToolStripItem[]
            {
                Item("Open", Keys.Enter, (s, e) => OpenSelected()),
                miOpenWith,
                Item("Show in folder", Keys.Control | Keys.Enter, (s, e) => RevealSelected()),
                new ToolStripSeparator(),
                Item("Copy", Keys.Control | Keys.C, (s, e) => ClipboardFiles(false)),
                Item("Cut", Keys.Control | Keys.X, (s, e) => ClipboardFiles(true)),
                miPaste,
                Item("Copy full path", Keys.Control | Keys.Shift | Keys.C, (s, e) => CopyPathOfSelected()),
                new ToolStripSeparator(),
                miFavorite,
                miRename,
                Item("Move to Recycle Bin", Keys.Delete, (s, e) => DeleteSelected()),
                new ToolStripSeparator(),
                Item("Properties", Keys.Alt | Keys.Enter, (s, e) => ShowProperties())
            });
            // Shortcuts are handled by the list's KeyDown; the menu only displays them.
            foreach (var mi in rowMenu.Items.OfType<ToolStripMenuItem>()) mi.ShortcutKeyDisplayString = ShortcutText(mi.ShortcutKeys);
            foreach (var mi in rowMenu.Items.OfType<ToolStripMenuItem>()) mi.ShortcutKeys = Keys.None;

            rowMenu.Opening += (s, e) =>
            {
                var sel = SelectedEntries();
                if (sel.Count == 0) { e.Cancel = true; return; }
                miOpenWith.Enabled = sel.Count == 1 && !sel[0].IsDirectory;
                miRename.Enabled = sel.Count == 1;
                miPaste.Enabled = Clipboard.ContainsFileDropList();
                miFavorite.Text = sel.All(f => favorites.Contains(f.FullPath)) ? "Remove from favourites" : "Add to favourites";
            };
            resultsView.ContextMenuStrip = rowMenu;
        }

        static string ShortcutText(Keys k)
        {
            if (k == Keys.None) return "";
            var parts = new List<string>();
            if ((k & Keys.Control) != 0) parts.Add("Ctrl");
            if ((k & Keys.Shift) != 0) parts.Add("Shift");
            if ((k & Keys.Alt) != 0) parts.Add("Alt");
            parts.Add((k & Keys.KeyCode).ToString());
            return string.Join("+", parts);
        }

        // ================================================================ open / reveal

        void OpenSelected()
        {
            var sel = SelectedEntries();
            if (sel.Count == 0) return;
            foreach (var f in sel.Take(10)) OpenEntry(f);
        }

        bool EnsureExists(FileEntry f)
        {
            if (File.Exists(f.FullPath) || Directory.Exists(f.FullPath)) return true;
            var r = MessageBox.Show(this, "This item no longer exists:\n" + f.FullPath + "\n\nRemove it from the index?",
                "SwiftFind", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r == DialogResult.Yes) { RemoveFromIndex(f.FullPath); ApplySearch(); }
            return false;
        }

        void OpenEntry(FileEntry f)
        {
            if (!EnsureExists(f)) return;
            try { Process.Start(new ProcessStartInfo(f.FullPath) { UseShellExecute = true }); }
            catch (Exception ex) { Log.Error("Open " + f.FullPath, ex); MessageBox.Show(this, ex.Message, "Cannot open", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        void OpenWith()
        {
            var sel = SelectedEntries();
            if (sel.Count != 1 || !EnsureExists(sel[0])) return;
            try { Process.Start(new ProcessStartInfo(sel[0].FullPath) { UseShellExecute = true, Verb = "openas" }); }
            catch (Exception ex) { Log.Error("Open with", ex); }
        }

        void RevealSelected()
        {
            var sel = SelectedEntries();
            if (sel.Count == 0 || !EnsureExists(sel[0])) return;
            Reveal(sel[0].FullPath, false);
        }

        void Reveal(string path, bool openFolderItself)
        {
            try
            {
                if (openFolderItself) Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
                else Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true });
            }
            catch (Exception ex) { Log.Error("Reveal", ex); }
        }

        void ShowProperties()
        {
            var sel = SelectedEntries();
            if (sel.Count != 1 || !EnsureExists(sel[0])) return;
            try { Process.Start(new ProcessStartInfo(sel[0].FullPath) { UseShellExecute = true, Verb = "properties" }); }
            catch (Exception ex) { Log.Error("Properties", ex); }
        }

        // ================================================================ clipboard

        void CopyPathOfSelected()
        {
            var sel = SelectedEntries();
            if (sel.Count == 0) return;
            try
            {
                Clipboard.SetText(string.Join(Environment.NewLine, sel.Select(f => f.FullPath)));
                SetStatus(sel.Count == 1 ? "Path copied." : sel.Count + " paths copied.");
            }
            catch (Exception ex) { Log.Error("Copy path", ex); }
        }

        void ClipboardFiles(bool cut)
        {
            var sel = SelectedEntries().Where(f => File.Exists(f.FullPath) || Directory.Exists(f.FullPath)).ToList();
            if (sel.Count == 0) return;
            try
            {
                var list = new StringCollection();
                foreach (var f in sel) list.Add(f.FullPath);
                var data = new DataObject();
                data.SetFileDropList(list);
                data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(cut ? DropEffectMove : DropEffectCopy)));
                Clipboard.SetDataObject(data, true);
                SetStatus((cut ? "Cut " : "Copied ") + sel.Count + (sel.Count == 1 ? " item." : " items."));
            }
            catch (Exception ex) { Log.Error("Clipboard", ex); MessageBox.Show(this, ex.Message, "Clipboard", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        void PasteHere()
        {
            if (!Clipboard.ContainsFileDropList()) return;
            var sel = SelectedEntries();
            string destDir = settings.RootPath;
            if (sel.Count > 0) destDir = sel[0].IsDirectory ? sel[0].FullPath : sel[0].Directory;

            bool move = false;
            try
            {
                var data = Clipboard.GetDataObject();
                var ms = data != null ? data.GetData("Preferred DropEffect") as MemoryStream : null;
                if (ms != null) { var b = new byte[4]; ms.Read(b, 0, 4); move = (BitConverter.ToInt32(b, 0) & DropEffectMove) != 0; }
            }
            catch { }

            var errors = new List<string>();
            int ok = 0;
            foreach (string src in Clipboard.GetFileDropList().Cast<string>().ToList())
            {
                try
                {
                    bool isDir = Directory.Exists(src);
                    if (!isDir && !File.Exists(src)) { errors.Add(Path.GetFileName(src) + ": no longer exists"); continue; }
                    string name = Path.GetFileName(src.TrimEnd('\\'));
                    string dest = Path.Combine(destDir, name);

                    if (move)
                    {
                        if (string.Equals(dest, src, StringComparison.OrdinalIgnoreCase)) continue; // already there
                        if (File.Exists(dest) || Directory.Exists(dest)) dest = FileOps.UniquePath(destDir, name);
                        FileOps.MoveItem(src, dest, isDir);
                        RemoveFromIndex(src);
                    }
                    else
                    {
                        if (File.Exists(dest) || Directory.Exists(dest)) dest = FileOps.UniquePath(destDir, name);
                        FileOps.CopyItem(src, dest, isDir);
                    }
                    AddToIndex(dest);
                    ok++;
                }
                catch (Exception ex)
                {
                    Log.Error("Paste " + src, ex);
                    errors.Add(Path.GetFileName(src) + ": " + ex.Message);
                }
            }
            if (move && ok > 0) { try { Clipboard.Clear(); } catch { } }
            ApplySearch();
            SetStatus(ok + (ok == 1 ? " item pasted." : " items pasted.") + (errors.Count > 0 ? "  " + errors.Count + " failed." : ""));
            if (errors.Count > 0) MessageBox.Show(this, string.Join("\n", errors.Take(8)), "Some items could not be pasted", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // ================================================================ rename / delete

        void RenameSelected()
        {
            var sel = SelectedEntries();
            if (sel.Count != 1) return;
            FileEntry f = sel[0];
            if (!EnsureExists(f)) return;

            string parent = f.Directory;
            string newName = Dialogs.Prompt(this, "Rename", "New name for \"" + f.Name + "\"", f.Name, text =>
            {
                string problem = FileOps.ValidateName(text);
                if (problem != null) return problem;
                string target = Path.Combine(parent, text);
                bool caseOnly = string.Equals(text, f.Name, StringComparison.OrdinalIgnoreCase);
                if (!caseOnly && (File.Exists(target) || Directory.Exists(target))) return "An item with that name already exists here.";
                return null;
            });
            if (newName == null || newName == f.Name) return;

            string dest = Path.Combine(parent, newName);
            try
            {
                if (f.IsDirectory) Directory.Move(f.FullPath, dest); else File.Move(f.FullPath, dest);
                if (favorites.Remove(f.FullPath)) { favorites.Add(dest); SaveFavorites(); }
                RemoveFromIndex(f.FullPath);
                AddToIndex(dest);
                ApplySearch();
                SetStatus("Renamed to " + newName);
            }
            catch (Exception ex)
            {
                Log.Error("Rename", ex);
                MessageBox.Show(this, ex.Message, "Rename failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void DeleteSelected()
        {
            var sel = SelectedEntries();
            if (sel.Count == 0) return;
            string what = sel.Count == 1 ? "\"" + sel[0].Name + "\"" : sel.Count + " items";
            var ans = MessageBox.Show(this, "Move " + what + " to the Recycle Bin?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ans != DialogResult.Yes) return;

            int removed = 0; var errors = new List<string>();
            foreach (var f in sel)
            {
                try
                {
                    if (File.Exists(f.FullPath) || Directory.Exists(f.FullPath)) FileOps.Recycle(f.FullPath, f.IsDirectory);
                    if (!File.Exists(f.FullPath) && !Directory.Exists(f.FullPath))
                    {
                        RemoveFromIndex(f.FullPath);
                        if (favorites.Remove(f.FullPath)) SaveFavorites();
                        removed++;
                    }
                }
                catch (Exception ex) { Log.Error("Delete " + f.FullPath, ex); errors.Add(f.Name + ": " + ex.Message); }
            }
            ApplySearch();
            SetStatus(removed + (removed == 1 ? " item moved to the Recycle Bin." : " items moved to the Recycle Bin."));
            if (errors.Count > 0) MessageBox.Show(this, string.Join("\n", errors.Take(8)), "Some items could not be deleted", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // ================================================================ drag out

        void OnItemDrag(object sender, ItemDragEventArgs e)
        {
            var paths = SelectedEntries().Where(f => File.Exists(f.FullPath) || Directory.Exists(f.FullPath)).Select(f => f.FullPath).ToArray();
            if (paths.Length == 0) return;
            try { resultsView.DoDragDrop(new DataObject(DataFormats.FileDrop, paths), DragDropEffects.Copy | DragDropEffects.Move); }
            catch (Exception ex) { Log.Error("Drag", ex); }
        }
    }
}
