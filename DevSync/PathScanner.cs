using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DevSyncLib;

namespace DevSync
{
    public partial class Sender
    {
        public class PathScanner
        {
            private bool _needToQuit;

            private readonly object _syncHasWork = new object();

            private readonly HashSet<string> _pathsToScan = new HashSet<string>();

            private readonly Sender _sender;

            private readonly CancellationTokenSource _cancellationTokenSource;

            public PathScanner(Sender sender)
            {
                _sender = sender;
                _cancellationTokenSource = new CancellationTokenSource();
            }

            private bool HasWork
            {
                get
                {
                    lock (_pathsToScan)
                    {
                        return _needToQuit || _pathsToScan.Count > 0;
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
                try
                {
                    var scanDirectory = new ScanDirectory(_sender._logger, _sender._excludeList,
                        cancellationToken: _cancellationTokenSource.Token);
                    foreach (var srcEntry in scanDirectory.ScanPath(_sender._srcPath, path))
                    {
                        _sender.AddChange(new FsChange {ChangeType = FsChangeType.Change, FsEntry = srcEntry}, false);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                _sender.NotifyHasWork();
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
                while (!_needToQuit)
                {
                    DoWork();
                    WaitForWork();
                }
            }

            public void Stop()
            {
                _cancellationTokenSource.Cancel();
                _needToQuit = true;
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
