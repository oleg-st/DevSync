using System;
using System.IO;

namespace DevSyncLib
{
    public class FsChange
    {
        public FsChangeType ChangeType;
        public string Path;
        // change
        public long Length;
        public bool NeedToResolve { get; private set; }
        public bool IsDirectory;
        public DateTime LastWriteTime;
        // rename
        public string OldPath;
        public long BodySize => HasBody ? Length : 0;
        public bool HasBody => ChangeType == FsChangeType.Change && !IsDirectory;
        // change is expired -> ignore it
        public bool Expired;

        public bool IsEmpty => ChangeType == FsChangeType.EmptyMarker;
        public static readonly FsChange Empty = new FsChange(FsChangeType.EmptyMarker, null);

        public override string ToString()
        {
            return $"{ChangeType} {(ChangeType == FsChangeType.Rename ? $"{OldPath} -> " : "")}{Path}{(ChangeType == FsChangeType.Change && Length >= 0 ? $", {Length}" : "")}";
        }

        public FsChange(FsChangeType changeType, string path)
        {
            ChangeType = changeType;
            Path = path;
        }

        public static FsChange CreateRemove(string path)
        {
            return new FsChange(FsChangeType.Remove, path);
        }

        public static FsChange CreateChange(string path)
        {
            return new FsChange(FsChangeType.Change, path)
            {
                NeedToResolve = true
            };
        }

        public static FsChange CreateRename(string path, string oldPath)
        {
            return new FsChange(FsChangeType.Rename, path)
            {
                OldPath = oldPath,
            };
        }

        public static FsChange CreateChange(FsEntry fsEntry)
        {
            return new FsChange(FsChangeType.Change, fsEntry.Path)
            {
                LastWriteTime = fsEntry.LastWriteTime,
                IsDirectory = fsEntry.IsDirectory,
            };
        }

        public void Resolve(string basePath)
        {
            var fileInfo = new FileInfo(System.IO.Path.Combine(basePath, Path));
            var attributes = fileInfo.Attributes;
            if (attributes != (FileAttributes)(-1))
            {
                LastWriteTime = fileInfo.LastWriteTime;
                IsDirectory = (attributes & FileAttributes.Directory) != 0;
            }
            else
            {
                ChangeType = FsChangeType.Remove;
            }
            NeedToResolve = false;
        }
    }
}
