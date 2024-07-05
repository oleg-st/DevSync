using System;
using System.IO;

namespace DevSyncLib;

public struct FsEntry : IEquatable<FsEntry>
{
    public bool IsDirectory => Length == -1;
    public string Path;
    public long Length;
    public DateTime LastWriteTime;
    public static readonly FsEntry Empty = new() { Path = "" };
    public bool IsEmpty => string.IsNullOrEmpty(Path);

    public static string NormalizePath(string path) => NormalizeSlash(NormalizeStart(path));

    public static ReadOnlySpan<char> NormalizeStart(ReadOnlySpan<char> span)
    {
        var index = 0;
        var length = span.Length;

        // skip .[/\\]*
        while (index < length && span[index] == '.' &&
               (index + 1 >= length || span[index + 1] == '/' || span[index + 1] == '\\'))
        {
            index++;
            // skip [/\\]+
            while (index < length && (span[index] == '/' || span[index] == '\\'))
            {
                index++;
            }
        }

        return span[index..];
    }

    public static string NormalizeSlash(ReadOnlySpan<char> span)
    {
        if (System.IO.Path.DirectorySeparatorChar == '\\' && span.Contains('\\'))
        {
            var len = span.Length;
            Span<char> destSpan = stackalloc char[len];

#if NET8_0_OR_GREATER
                span.Replace(destSpan, '\\', '/');
#else
            for (int i = 0; i < len; i++)
            {
                if (span[i] == '\\')
                    destSpan[i] = '/';
                else
                    destSpan[i] = span[i];
            }
#endif
            return new string(destSpan);
        }

        return new string(span);
    }

    private static bool CompareDates(DateTime dt1, DateTime dt2) => Math.Abs(dt1.Subtract(dt2).TotalSeconds) < 1;

    public static FsEntry FromFsInfo(string normalizedPath, FileSystemInfo fsInfo, bool withInfo) =>
        new()
        {
            LastWriteTime = withInfo ? fsInfo.LastWriteTime : DateTime.MinValue,
            Path = normalizedPath,
            Length = withInfo ? (fsInfo as FileInfo)?.Length ?? -1 :
                fsInfo is FileInfo ? 0 : -1
        };

    public bool Equals(FsEntry other) =>
        // TODO: we don't compare dates for directories
        Path == other.Path && Length == other.Length && (IsDirectory || CompareDates(LastWriteTime, other.LastWriteTime));
}