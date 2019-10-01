using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using DevSyncLib;

namespace DevSync
{
    public partial class Sender
    {
        public class PathScanner
        {
            private bool _needQuit;

            private readonly object _syncHasWork = new object();

            private readonly HashSet<string> _pathsToScan = new HashSet<string>();

            private readonly Sender _sender;

            public PathScanner(Sender sender)
            {
                _sender = sender;
            }

            private bool HasWork
            {
                get
                {
                    lock (_pathsToScan)
                    {
                        return _needQuit || _pathsToScan.Count > 0;
                    }
                }
            }

            private void NotifyHasWork()
            {
                lock (_syncHasWork)
                {
                    Monitor.Pulse(_syncHasWork);
                }
            }

            private void AddDirectoryContents(string path)
            {
                var scanDirectory = new ScanDirectory(_sender._logger, _sender._excludeList);
                var srcList = scanDirectory.ScanPath(_sender._srcPath, path).ToDictionary(x => x.Path, y => y);
                foreach (var srcEntry in srcList.Values)
                {
                    var fsChange = new FsChange {ChangeType = FsChangeType.Change, FsEntry = srcEntry};
                    _sender.AddChange(fsChange);
                }
            }

            private void WaitForWork()
            {
                while (!HasWork)
                {
                    lock (_syncHasWork)
                    {
                        Monitor.Wait(_syncHasWork);
                    }
                }
            }

            private void DoWork()
            {
                string path;
                lock (_pathsToScan)
                {
                    path = _pathsToScan.FirstOrDefault();
                    if (path == null)
                    {
                        return;
                    }

                    _pathsToScan.Remove(path);
                }

                AddDirectoryContents(path);
            }

            public void Run()
            {
                while (!_needQuit)
                {
                    DoWork();
                    WaitForWork();
                }
            }

            public void Stop()
            {
                _needQuit = true;
                NotifyHasWork();
            }

            public void Add(string path)
            {
                lock (_pathsToScan)
                {
                    _pathsToScan.Add(path);
                }

                NotifyHasWork();
            }

            public void Clear()
            {
                lock (_pathsToScan)
                {
                    _pathsToScan.Clear();
                }
                NotifyHasWork();
            }
        }
    }
}
