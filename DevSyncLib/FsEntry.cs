using System;
using System.IO;

namespace DevSyncLib
{
    public struct FsEntry
    {
        public bool IsDirectory => Length == -1;
        public string Path;
        public long Length;
        public DateTime LastWriteTime;
        public static readonly FsEntry Empty = new FsEntry { Path = "" };
        public bool IsEmpty => string.IsNullOrEmpty(Path);


        public static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private bool CompareDates(DateTime dt1, DateTime dt2)
        {
            return Math.Abs(dt1.Subtract(dt2).TotalSeconds) < 1;
        }

        public bool Compare(FsEntry other)
        {
            // TODO: we don't compare dates for directories
            return Path == other.Path && Length == other.Length && (IsDirectory || CompareDates(LastWriteTime, other.LastWriteTime));
        }

        public static FsEntry FromFsInfo(string path, FileSystemInfo fsInfo, bool withInfo)
        {
            return new FsEntry
            {
                LastWriteTime = withInfo ? fsInfo.LastWriteTime : DateTime.MinValue,
                Path = NormalizePath(path),
                Length = withInfo ? (fsInfo as FileInfo)?.Length ?? -1 : 
                    fsInfo is FileInfo ? 0 : -1
            };
        }
    }
}
