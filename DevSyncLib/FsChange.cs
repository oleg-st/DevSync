using System;
using System.Diagnostics.CodeAnalysis;

namespace DevSyncLib;

public class FsChange(FsChangeType changeType, string? path) : IEquatable<FsChange>
{
    public FsChangeType ChangeType = changeType;
    public string? Path = path;
    // change
    public long Length;
    public bool IsDirectory;
    public DateTime LastWriteTime;
    // rename
    public string? OldPath;
    public long BodySize => HasBody ? Length : 0;
    public bool HasBody => IsChange && !IsDirectory;

    public static readonly FsChange Empty = new(FsChangeType.EmptyMarker, null);

    public bool IsEmpty => ChangeType == FsChangeType.EmptyMarker;

    // need body
    [MemberNotNullWhen(true, nameof(Path))]
    public bool IsChange => ChangeType is FsChangeType.Change or FsChangeType.ChangeAndRename;
    // has old path
    [MemberNotNullWhen(true, nameof(Path))]
    [MemberNotNullWhen(true, nameof(OldPath))]
    public bool IsRename => ChangeType is FsChangeType.Rename or FsChangeType.ChangeAndRename;

    public bool IsRemove => ChangeType == FsChangeType.Remove;

    public override string ToString() => 
        $"{ChangeType} {(IsRename ? $"{OldPath} -> " : "")}{Path}{(ChangeType == FsChangeType.Change && Length >= 0 ? $", {Length}" : "")}";

    public bool Equals(FsChange? other)
    {
        return other != null && ChangeType == other.ChangeType && ChangeType switch
        {
            FsChangeType.Change => Path == other.Path &&
                                   Length == other.Length &&
                                   LastWriteTime.Equals(other.LastWriteTime),
            FsChangeType.Remove => Path == other.Path,
            FsChangeType.Rename => Path == other.Path && OldPath == other.OldPath,
            FsChangeType.ChangeAndRename => Path == other.Path &&
                                            OldPath == other.OldPath &&
                                            Length == other.Length &&
                                            LastWriteTime.Equals(other.LastWriteTime),
            _ => true
        };
    }
}