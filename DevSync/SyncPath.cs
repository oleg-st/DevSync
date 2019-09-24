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
            // [USER@]HOST: SRC... [DEST]
            var regex = new Regex("^([^@:/]+@)?([^:/]+:)?(/.*)$", RegexOptions.Compiled);
            var match = regex.Match(path);
            if (!match.Success)
            {
                return null;
            }

            var syncPath = new SyncPath
            {
                UserName = match.Groups[1].Value.TrimEnd('@'),
                Host = match.Groups[2].Value.TrimEnd(':'),
                Path = match.Groups[3].Value
            };

            if (string.IsNullOrEmpty(syncPath.UserName))
            {
                syncPath.UserName = Environment.UserName;
            }

            return syncPath;
        }
    }
}
