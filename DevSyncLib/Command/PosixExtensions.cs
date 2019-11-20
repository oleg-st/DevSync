using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DevSyncLib.Command
{
    public static class PosixExtensions
    {
        [DllImport("libc", EntryPoint = "fchmod")]
        internal static extern int FChmod(SafeFileHandle fd, int mode);

        private static volatile bool _noChmod;

        public static int ChangeMode(this FileStream fileStream, int mode)
        {
            if (_noChmod)
            {
                return -1;
            }

            try
            {
                return FChmod(fileStream.SafeFileHandle, mode);
            }
            catch (DllNotFoundException) // no libc
            {
                _noChmod = true;
                return -1;
            }
            catch (EntryPointNotFoundException) // no fchmod in libc
            {
                _noChmod = true;
                return -1;
            }
        }
    }
}
