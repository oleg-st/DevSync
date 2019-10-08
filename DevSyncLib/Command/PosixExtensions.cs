using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DevSyncLib.Command
{
    public static class PosixExtensions
    {
        [DllImport("libc", EntryPoint = "fchmod")]
        internal static extern int FChmod(SafeFileHandle fd, int mode);

        public static int StreamChmod(this FileStream fileStream, int mode)
        {
            return FChmod(fileStream.SafeFileHandle, mode);
        }
    }
}
