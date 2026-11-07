using System;

namespace SwiftFind.Indexing
{
    public enum DriveKind { Unknown, Ssd, Hdd, Network, Optical }

    /// <summary>
    /// Chooses how many threads to scan with, from the drive type, the PC's CPU/RAM and the user's preference.
    /// Pure logic (no Windows calls) so it is unit-tested.
    ///
    /// The key insight: SSDs love parallel requests, spinning disks hate them (the head thrashes), and a USB
    /// bridge or network share sits in between. Using the same thread count everywhere is slow on one or the other.
    /// </summary>
    public static class IndexTuning
    {
        const long GB = 1024L * 1024 * 1024;

        public static string Normalize(string priority)
        {
            if (priority == "Gentle" || priority == "Fast") return priority;
            return "Auto";   // also upgrades the older "Balanced" value
        }

        public static int ScanThreads(DriveKind kind, bool external, string priority, int cores, long ramBytes)
        {
            int c = Math.Max(1, cores);
            priority = Normalize(priority);

            // What this kind of storage can usefully absorb.
            int cap;
            switch (kind)
            {
                case DriveKind.Ssd: cap = Math.Min(c, 16); break;
                case DriveKind.Hdd: cap = 2; break;
                case DriveKind.Network: cap = 4; break;
                case DriveKind.Optical: cap = 1; break;
                default: cap = external ? 3 : Math.Min(c, 4); break;   // unknown: stay conservative
            }

            int t = cap;
            if (priority == "Gentle") t = Math.Max(1, Math.Min(cap, c / 4));
            else if (priority == "Fast")
                t = kind == DriveKind.Hdd ? 3 : (kind == DriveKind.Optical ? 1 : Math.Min(Math.Max(cap, c), 24));

            // Low-memory PCs: fewer threads means fewer simultaneous directory buffers and less pressure.
            if (ramBytes > 0)
            {
                if (ramBytes < 2 * GB) t = 1;
                else if (ramBytes < 4 * GB) t = Math.Min(t, 2);
            }
            return Math.Max(1, t);
        }

        /// <summary>Threads for the follow-up size/date pass: limited by the slowest drive involved.</summary>
        public static int EnrichThreads(DriveKind slowest, bool external, string priority, int cores, long ramBytes)
        {
            return ScanThreads(slowest, external, priority, cores, ramBytes);
        }

        /// <summary>The "slowest" of several kinds, for choosing one setting that suits all drives.</summary>
        public static DriveKind Slowest(System.Collections.Generic.IEnumerable<DriveKind> kinds)
        {
            DriveKind worst = DriveKind.Ssd;
            bool any = false;
            foreach (DriveKind k in kinds)
            {
                any = true;
                if (Rank(k) > Rank(worst)) worst = k;
            }
            return any ? worst : DriveKind.Unknown;
        }

        static int Rank(DriveKind k)
        {
            switch (k)
            {
                case DriveKind.Ssd: return 0;
                case DriveKind.Unknown: return 1;
                case DriveKind.Network: return 2;
                case DriveKind.Hdd: return 3;
                default: return 4;
            }
        }
    }
}
