using System.IO;

namespace DevSyncLib
{
    public class FsSenderChange : FsChange
    {
        public bool NeedToResolve { get; private set; }
        // change is expired -> ignore it
        public bool Expired;
        // change is vanished
        public bool Vanished;
        // change is opened -> cannot vanish
        public bool Opened;
        private Timer _readyTimer;
        // change is ready after this timeout
        public const int ReadyTimeoutMs = 100;
        // wait for this timeout if change is not ready
        public const int WaitForReadyTimeoutMs = ReadyTimeoutMs / 5;
        // vanish is expired after this timeout
        public const int VanishExpireTimeoutMs = 1500;

        public override string ToString()
        {
            return $"{ChangeType} {(IsRename ? $"{OldPath} -> " : "")}{Path}{(IsChange && Length >= 0 ? $", {Length}" : "")}";
        }

        private FsSenderChange(FsChangeType changeType, string path, bool isReady = true) : base(changeType, path)
        {
            // start timer if not ready
            _readyTimer = Timer.Create(!isReady, ReadyTimeoutMs);
        }

        public bool IsReady
        {
            get
            {
                if (!_readyTimer.IsRunning)
                {
                    return true;
                }

                // not ready yet
                if (!_readyTimer.IsFired)
                {
                    return false;
                }

                // fired, stop stopwatch
                _readyTimer.Stop();
                return true;
            }
        }

        public static FsSenderChange CreateWithPath(FsSenderChange fsChange, string path)
        {
            switch (fsChange.ChangeType)
            {
                case FsChangeType.Remove:
                    return CreateRemove(path);
                case FsChangeType.Change:
                    return CreateChange(path);
                case FsChangeType.Rename:
                    return CreateRename(path, fsChange.OldPath);
                case FsChangeType.ChangeAndRename:
                    return CreateChangeAndRename(path, fsChange.OldPath);
                default:
                    // not reachable
                    return null;
            }
        }

        public static FsSenderChange CreateRemove(string path)
        {
            return new FsSenderChange(FsChangeType.Remove, path);
        }

        public static FsSenderChange CreateChange(string path)
        {
            // Delay sending change to combine multiple modifications of a same file
            return new FsSenderChange(FsChangeType.Change, path, false)
            {
                NeedToResolve = true
            };
        }

        public static FsSenderChange CreateChangeAndRename(string path, string oldPath)
        {
            return new FsSenderChange(FsChangeType.ChangeAndRename, path, false)
            {
                NeedToResolve = true,
                OldPath = oldPath,
            };
        }

        public static FsSenderChange CreateRename(string path, string oldPath)
        {
            return new FsSenderChange(FsChangeType.Rename, path)
            {
                OldPath = oldPath,
            };
        }

        public static FsSenderChange CreateChange(FsEntry fsEntry)
        {
            return new FsSenderChange(FsChangeType.Change, fsEntry.Path)
            {
                LastWriteTime = fsEntry.LastWriteTime,
                IsDirectory = fsEntry.IsDirectory,
            };
        }

        public void Resolve(string path)
        {
            var fileInfo = new FileInfo(path);
            var attributes = fileInfo.Attributes;
            if (attributes != (FileAttributes)(-1))
            {
                LastWriteTime = fileInfo.LastWriteTime;
                IsDirectory = (attributes & FileAttributes.Directory) != 0;
                NeedToResolve = false;
            }
            else
            {
                // item vanished
                Vanished = true;
            }
        }

        public void DelayVanished()
        {
            // wait and expire
            _readyTimer.Start(VanishExpireTimeoutMs);
            Expired = true;
        }

        public void Combine(FsSenderChange other)
        {
            // Change + Rename -> ChangeAndRename
            if ((IsChange && other.IsRename) || (IsRename && other.IsChange))
            {
                ChangeType = FsChangeType.ChangeAndRename;
                if (other.IsChange)
                {
                    NeedToResolve = other.NeedToResolve;
                    LastWriteTime = other.LastWriteTime;
                    IsDirectory = other.IsDirectory;
                }

                if (other.IsRename)
                {
                    OldPath = other.OldPath;
                }
            }
        }
    }
}
