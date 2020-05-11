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

        [DllImport("libc", EntryPoint = "chmod")]
        internal static extern int Chmod(string pathname, int mode);

        [DllImport("libc", EntryPoint = "umask")]
        private static extern uint umask(uint mask);

        private static volatile bool _noChmod;

        public static bool SetupUserMask()
        {
            try
            {
                // 0022
                umask(0b0_010_010);
                return true;
            }
            catch (Exception) // no libc or no umask
            {
                return false;
            }
        }

        public static int ChangeMode(string filename, int mode)
        {
            if (_noChmod)
            {
                return -1;
            }

            try
            {
                return Chmod(filename, mode);
            }
            catch (DllNotFoundException) // no libc
            {
                _noChmod = true;
                return -1;
            }
            catch (EntryPointNotFoundException) // no chmod in libc
            {
                _noChmod = true;
                return -1;
            }
        }

        public static int FChangeMode(this FileStream fileStream, int mode)
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
