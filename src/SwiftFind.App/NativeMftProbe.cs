using System;
using System.IO;
using System.Runtime.InteropServices;
class NativeMftProbe
{
    const uint FSCTL_ENUM_USN_DATA = 0x000900B3;
    const uint GENERIC_READ = 0x80000000;
    const uint SHARE_ALL = 7;
    const uint OPEN_EXISTING = 3;
    [StructLayout(LayoutKind.Sequential)]
    struct MftEnumData
    {
        public ulong StartFileReferenceNumber;
        public long LowUsn;
        public long HighUsn;
    }
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode,
        SetLastError=true)]
    static extern IntPtr CreateFile(
        string name, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError=true)]
    static extern bool DeviceIoControl(
        IntPtr handle, uint code, ref MftEnumData input,
        int inputSize, byte[] output, int outputSize,
        out int returned, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError=true)]
    static extern bool CloseHandle(IntPtr handle);
    static int Main(string[] args)
    {
        string drive = args.Length > 0 ? args[0] : "C:";
        if (drive.Length != 2 || drive[1] != ':')
        {
            Console.WriteLine("Use a drive such as C: or D:");
            return 2;
        }
        IntPtr h = CreateFile(
            @"\\.\" + drive, GENERIC_READ, SHARE_ALL,
            IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (h == new IntPtr(-1))
        {
            Console.WriteLine("Cannot open volume. Win32 error: " +
                Marshal.GetLastWin32Error());
            return 1;
        }
        int total = 0;
        try
        {
            MftEnumData input = new MftEnumData {
                StartFileReferenceNumber = 0,
                LowUsn = 0,
                HighUsn = Int64.MaxValue
            };
            byte[] buffer = new byte[1024 * 1024];
            for (int batch = 1; batch <= 10; batch++)
            {
                int bytes;
                bool ok = DeviceIoControl(
                    h, FSCTL_ENUM_USN_DATA, ref input,
                    Marshal.SizeOf(typeof(MftEnumData)),
                    buffer, buffer.Length, out bytes, IntPtr.Zero);
                if (!ok)
                {
                    int error = Marshal.GetLastWin32Error();
                    Console.WriteLine("First/native API error: " + error);
                    if (error == 38 || error == 259)
                        break;
                    Console.WriteLine("Native enumeration error: " + error);
                    return total > 0 ? 0 : 1;
                }
                Console.WriteLine("Bytes returned: " + bytes);
                if (bytes < 8) break;
                ulong next = BitConverter.ToUInt64(buffer, 0);
                Console.WriteLine("Next file reference: " + next);
                int offset = 8;
                int batchCount = 0;
                while (offset + 60 <= bytes)
                {
                    uint recordLength = BitConverter.ToUInt32(buffer, offset);
                    if (recordLength < 60 || offset + recordLength > bytes)
                        break;
                    ushort nameLength = BitConverter.ToUInt16(buffer, offset + 56);
                    ushort nameOffset = BitConverter.ToUInt16(buffer, offset + 58);
                    if ((uint)nameOffset + nameLength <= recordLength)
                        batchCount++;
                    offset += (int)recordLength;
                }
                total += batchCount;
                Console.WriteLine("Batch {0}: {1:N0} records; total {2:N0}",
                    batch, batchCount, total);
                if (next <= input.StartFileReferenceNumber ||
                    batchCount == 0)
                    break;
                input.StartFileReferenceNumber = next;
            }
            Console.WriteLine("Probe complete. This was a bounded test, not a full index.");
            return 0;
        }
        finally
        {
            CloseHandle(h);
        }
    }
}


