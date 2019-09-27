using System;
using System.IO;

namespace DevSyncLib
{
    public class FsEntry
    {
        public bool IsDirectory => Length == -1;
        public string Path;
        public long Length;
        public DateTime LastWriteTime;

        public static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        protected bool CompareDates(DateTime dt1, DateTime dt2)
        {
            return Math.Abs(dt1.Subtract(dt2).TotalSeconds) < 1;
        }

        public bool Compare(FsEntry other)
        {
            // TODO: we don't compare dates for directories
            return other != null && Path == other.Path && Length == other.Length && (IsDirectory || CompareDates(LastWriteTime, other.LastWriteTime));
        }

        public static FsEntry FromFileInfo(string path, FileInfo file)
        {
            return new FsEntry
            {
                LastWriteTime = file.LastWriteTime,
                Path = NormalizePath(path),
                Length = file.Length
            };
        }

        public static FsEntry FromDirectoryInfo(string path, DirectoryInfo dir)
        {
            return new FsEntry
            {
                LastWriteTime = dir.LastWriteTime,
                Path = NormalizePath(path),
                Length = -1
            };
        }

        public static FsEntry FromFilename(string fullname, string path)
        {
            var file = new FileInfo(fullname);
            return new FsEntry
            {
                LastWriteTime = file.Exists ? file.LastWriteTime : DateTime.UnixEpoch,
                Path = path,
                Length = !file.Exists || (file.Attributes & FileAttributes.Directory) != 0 ? -1 : file.Length
            };
        }
    }
}
