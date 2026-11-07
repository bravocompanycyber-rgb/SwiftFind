using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SwiftFind.Core;
using SwiftFind.Indexing;

namespace SwiftFind.Services
{
    public sealed class DriveDetails
    {
        public string Root;          // "C:\\"
        public DriveType Type;
        public string Format;        // "NTFS", "exFAT" ...
        public string Label;
        public uint Serial;          // volume serial number (0 = unknown)
        public long TotalBytes;
        public DriveKind Kind;       // SSD / HDD / network ...
        public bool IsExternal;      // USB, FireWire, SD card or other removable bus

        public bool IsNtfs { get { return string.Equals(Format, "NTFS", StringComparison.OrdinalIgnoreCase); } }
        public string TypeText
        {
            get
            {
                switch (Type)
                {
                    case DriveType.Fixed: return "Local disk";
                    case DriveType.Removable: return "Removable";
                    case DriveType.Network: return "Network";
                    case DriveType.CDRom: return "Optical";
                    default: return Type.ToString();
                }
            }
        }
        public string KindText
        {
            get
            {
                string k = Kind == DriveKind.Ssd ? "SSD" : Kind == DriveKind.Hdd ? "HDD" : Kind == DriveKind.Network ? "network drive" : Kind == DriveKind.Optical ? "optical" : "drive";
                return IsExternal ? "external " + k : k;
            }
        }

        public string Display
        {
            get { return Root.TrimEnd('\\') + (string.IsNullOrEmpty(Label) ? "" : " " + Label); }
        }
    }

    /// <summary>
    /// Keeps an always-current list of connected drives. All probing happens on background threads with
    /// per-drive timeouts, so a dead network share or an empty card reader can never freeze the UI.
    /// </summary>
    public sealed class DriveMonitor : IDisposable
    {
        public static readonly DriveMonitor Shared = new DriveMonitor();

        volatile List<DriveDetails> current = new List<DriveDetails>();
        int refreshing;
        Timer poll;

        /// <summary>Raised on a thread-pool thread when drives were added, removed or replaced.</summary>
        public event Action Changed;

        public IList<DriveDetails> Drives { get { return current; } }

        public void Start(int pollMilliseconds = 8000)
        {
            if (poll == null) poll = new Timer(_ => Refresh(), null, pollMilliseconds, pollMilliseconds);
        }

        /// <summary>Blocking refresh. Call from a background thread only.</summary>
        public void RefreshNow() { Refresh(); }

        public void RequestRefresh() { ThreadPool.QueueUserWorkItem(_ => Refresh()); }

        /// <summary>After a plug/unplug event the volume may take a moment to mount or vanish, so check twice.</summary>
        public void NudgeSoon()
        {
            ThreadPool.QueueUserWorkItem(_ => { Thread.Sleep(700); Refresh(); Thread.Sleep(2500); Refresh(); });
        }

        void Refresh()
        {
            if (Interlocked.Exchange(ref refreshing, 1) == 1) return;
            try
            {
                List<DriveDetails> list = Enumerate();
                bool changed = !SameSet(list, current);
                current = list;
                if (changed)
                {
                    Action h = Changed;
                    if (h != null) h();
                }
            }
            catch (Exception ex) { Log.Error("Drive refresh", ex); }
            finally { Interlocked.Exchange(ref refreshing, 0); }
        }

        static bool SameSet(List<DriveDetails> a, List<DriveDetails> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var x in a)
                if (!b.Any(y => string.Equals(x.Root, y.Root, StringComparison.OrdinalIgnoreCase) && x.Serial == y.Serial && x.Format == y.Format))
                    return false;
            return true;
        }

        static List<DriveDetails> Enumerate()
        {
            var result = new List<DriveDetails>();
            DriveInfo[] all;
            try { all = DriveInfo.GetDrives(); } catch { return result; }

            foreach (DriveInfo d in all)
            {
                DriveType type;
                try { type = d.DriveType; } catch { continue; }
                if (type == DriveType.NoRootDirectory || type == DriveType.Unknown) continue;   // CD-ROM and RAM disks are tracked too, so a folder on one isn't mistaken for 'disconnected'

                DriveDetails details = null;
                var probe = Task.Factory.StartNew(() =>
                {
                    try
                    {
                        if (!d.IsReady) return (DriveDetails)null;
                        string root = d.RootDirectory.FullName;
                        var dd = new DriveDetails
                        {
                            Root = root,
                            Type = type,
                            Format = d.DriveFormat,
                            Label = d.VolumeLabel,
                            TotalBytes = d.TotalSize,
                            Serial = Native.GetVolumeSerial(root)
                        };
                        if (type == DriveType.Network) dd.Kind = DriveKind.Network;
                        else if (type == DriveType.CDRom) dd.Kind = DriveKind.Optical;
                        else if (type != DriveType.Ram)
                        {
                            int seek = Native.QuerySeekPenalty(root);      // spinning disk or flash?
                            int bus = Native.QueryBusType(root);           // 7 = USB, 17 = NVMe ...
                            dd.Kind = seek == 1 ? DriveKind.Hdd : (seek == 0 || bus == 17 ? DriveKind.Ssd : DriveKind.Unknown);
                            dd.IsExternal = type == DriveType.Removable || bus == 4 || bus == 7 || bus == 12 || bus == 13;
                        }
                        return dd;
                    }
                    catch { return null; }
                });
                // A hung drive is simply treated as "not available" instead of blocking everything.
                if (probe.Wait(3000)) details = probe.Result;
                if (details != null) result.Add(details);
            }
            return result;
        }

        public void Dispose() { if (poll != null) poll.Dispose(); }
    }
}
