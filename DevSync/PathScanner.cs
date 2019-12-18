using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevSyncLib;

namespace DevSync
{
    public partial class Sender
    {
        public class PathScanner
        {
            private volatile bool _needToQuit;
            private readonly ManualResetEvent _hasWorkEvent = new ManualResetEvent(false);
            private readonly HashSet<string> _pathsToScan = new HashSet<string>();
            private readonly Sender _sender; 
            private readonly CancellationTokenSource _cancellationTokenSource;

            public PathScanner(Sender sender)
            {
                _sender = sender;
                _cancellationTokenSource = new CancellationTokenSource();
            }

            private void UpdateHasWork()
            {
                lock (this)
                {
                    if (_needToQuit || _pathsToScan.Count > 0)
                    {
                        _hasWorkEvent.Set();
                    }
                    else
                    {
                        _hasWorkEvent.Reset();
                    }
                }
            }

            private void AddDirectoryContents(string path)
            {
                try
                {
                    var scanDirectory = new ScanDirectory(_sender._logger, _sender._excludeList,
                        false, _cancellationTokenSource.Token);
                    foreach (var srcEntry in scanDirectory.ScanPath(_sender._srcPath, path))
                    {
                        _sender.AddChange(FsSenderChange.CreateChange(srcEntry.Path));
                    }
                }
                catch (OperationCanceledException)
                {
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
                UpdateHasWork();
                AddDirectoryContents(path);
            }

            protected void Run()
            {
                while (!_needToQuit)
                {
                    _hasWorkEvent.WaitOne();
                    DoWork();
                }
            }

            public void Start()
            {
                Task.Factory.StartNew(Run, TaskCreationOptions.LongRunning);
            }

            public void Stop()
            {
                _cancellationTokenSource.Cancel();
                lock (_pathsToScan)
                {
                    _pathsToScan.Clear();
                    _needToQuit = true;
                }
                UpdateHasWork();
            }

            public void Add(string path)
            {
                lock (_pathsToScan)
                {
                    _pathsToScan.Add(path);
                }
                UpdateHasWork();
            }

            public void Clear()
            {
                lock (_pathsToScan)
                {
                    _pathsToScan.Clear();
                }
                UpdateHasWork();
            }
        }
    }
}
