using System;
using System.IO;

namespace DevSyncLib
{
    public struct FsEntry : IEquatable<FsEntry>
    {
        public bool IsDirectory => Length == -1;
        public string Path;
        public long Length;
        public DateTime LastWriteTime;
        public static readonly FsEntry Empty = new FsEntry { Path = "" };
        public bool IsEmpty => string.IsNullOrEmpty(Path);

        public static string NormalizePath(string path)
        {
            return NormalizePath(path.AsSpan());
        }

        public static string NormalizePath(ReadOnlySpan<char> span)
        {
            var index = 0;
            var length = span.Length;

            // skip .[/\\]*
            while (index < length && span[index] == '.' &&
                   (index + 1 >= length || span[index] == '/' || span[index] == '\\'))
            {
                index++;
                // skip [/\\]+
                while (index < length && (span[index] == '/' || span[index] == '\\'))
                {
                    index++;
                }
            }

            var slicedSpan = span[index..];
            return new string(slicedSpan).Replace('\\', '/');
        }

        private bool CompareDates(DateTime dt1, DateTime dt2)
        {
            return Math.Abs(dt1.Subtract(dt2).TotalSeconds) < 1;
        }

        public static FsEntry FromFsInfo(string normalizedPath, FileSystemInfo fsInfo, bool withInfo)
        {
            return new FsEntry
            {
                LastWriteTime = withInfo ? fsInfo.LastWriteTime : DateTime.MinValue,
                Path = normalizedPath,
                Length = withInfo ? (fsInfo as FileInfo)?.Length ?? -1 :
                    fsInfo is FileInfo ? 0 : -1
            };
        }

        public bool Equals(FsEntry other)
        {
            // TODO: we don't compare dates for directories
            return Path == other.Path && Length == other.Length && (IsDirectory || CompareDates(LastWriteTime, other.LastWriteTime));
        }
    }
}
