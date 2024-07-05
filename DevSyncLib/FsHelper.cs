using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace DevSyncLib;

public static class FsHelper
{
    public static bool TryDeleteFile(string filename) => TryDeleteFile(filename, out _);

    public static bool TryDeleteFile(string filename, [MaybeNullWhen(true)] out Exception exception)
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
}