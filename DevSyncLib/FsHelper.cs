using System;
using System.IO;

namespace DevSyncLib
{
    public static class FsHelper
    {
        public static bool TryDeleteFile(string filename)
        {
            return TryDeleteFile(filename, out _);
        }

        public static bool TryDeleteDirectory(string filename)
        {
            return TryDeleteDirectory(filename, out _);
        }

        public static bool TryDeleteFile(string filename, out Exception exception)
        {
            if (File.Exists(filename))
            {
                try
                {
                    File.Delete(filename);
                }
                catch (Exception ex)
                {
                    exception = ex;
                    return false;
                }
            }

            exception = null;
            return true;
        }

        public static bool TryDeleteDirectory(string filename, out Exception exception)
        {
            if (Directory.Exists(filename))
            {
                try
                {
                    Directory.Delete(filename, true);
                }
                catch (Exception ex)
                {
                    exception = ex;
                    return false;
                }
            }

            exception = null;
            return true;
        }
    }
}
