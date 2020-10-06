using System.IO;

namespace DevSyncLib
{
    public class FsSenderChange : FsChange
    {
        public bool NeedToResolve { get; private set; }
        // change is expired -> ignore it
        public bool Expired;
        private SlimStopwatch _readyStopwatch;
        // change is ready after this timeout
        public const int ReadyTimeoutMs = 100;
        // wait for this timeout if change is not ready
        public const int WaitForReadyTimeoutMs = ReadyTimeoutMs / 5;

        public override string ToString()
        {
            return $"{ChangeType} {(ChangeType == FsChangeType.Rename ? $"{OldPath} -> " : "")}{Path}{(ChangeType == FsChangeType.Change && Length >= 0 ? $", {Length}" : "")}";
        }

        private FsSenderChange(FsChangeType changeType, string path, bool isReady = true) : base(changeType, path)
        {
            // start stopwatch if not ready
            _readyStopwatch = SlimStopwatch.Create(!isReady);
        }

        public bool IsReady
        {
            get
            {
                if (!_readyStopwatch.IsRunning)
                {
                    return true;
                }

                // not ready yet
                if (_readyStopwatch.ElapsedMilliseconds < ReadyTimeoutMs)
                {
                    return false;
                }

                // elapsed, stop stopwatch
                _readyStopwatch.Stop();
                return true;
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
