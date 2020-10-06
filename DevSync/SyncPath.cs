using DevSyncLib;
using System;
using System.Text.RegularExpressions;

namespace DevSync
{
    public class SyncPath
    {
        public string Host;
        public string UserName;
        public string Path;
        public static SyncPath Parse(string path)
        {
            SyncPath syncPath;
            // replace \ to /
            path = FsEntry.NormalizePath(path);

            // [UserName@]Host:Path (host 2+ symbols)
            var userHostPathRegex = new Regex("^([^@:/]+@)?([^:/]{2,}:)?(/.*)$");
            var userHostPathMatch = userHostPathRegex.Match(path);
            if (userHostPathMatch.Success)
            {
                syncPath = new SyncPath
                {
                    UserName = userHostPathMatch.Groups[1].Value.TrimEnd('@'),
                    Host = userHostPathMatch.Groups[2].Value.TrimEnd(':'),
                    Path = userHostPathMatch.Groups[3].Value
                };
            }
            else
            {
                // [drive:]/path (drive 1 symbol)
                var windowsPathRegex = new Regex("^([^:/]:)?(/.*)$");
                var windowsPathMatch = windowsPathRegex.Match(path);
                if (windowsPathMatch.Success)
                {
                    syncPath = new SyncPath
                    {
                        UserName = "",
                        Host = "",
                        Path = windowsPathMatch.Value
                    };
                }
                else
                {
                    return null;
                }
            }


            if (!string.IsNullOrEmpty(syncPath.Host) && string.IsNullOrEmpty(syncPath.UserName))
            {
                syncPath.UserName = Environment.UserName;
            }

            return syncPath;
        }
    }
}
