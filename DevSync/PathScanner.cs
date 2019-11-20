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
            private readonly ConditionVariable _hasWorkConditionVariable;
            private readonly HashSet<string> _pathsToScan = new HashSet<string>();
            private readonly Sender _sender; 
            private readonly CancellationTokenSource _cancellationTokenSource;

            public PathScanner(Sender sender)
            {
                _sender = sender;
                _cancellationTokenSource = new CancellationTokenSource();
                _hasWorkConditionVariable = new ConditionVariable();
            }

            private void AddDirectoryContents(string path)
            {
                try
                {
                    var scanDirectory = new ScanDirectory(_sender._logger, _sender._excludeList,
                        false, _cancellationTokenSource.Token);
                    foreach (var srcEntry in scanDirectory.ScanPath(_sender._srcPath, path))
                    {
                        _sender.AddChange(FsChange.CreateChange(srcEntry.Path));
                    }
                }
                catch (OperationCanceledException)
                {
                }
            }
            
            private void DoWork()
            {
                string path;
                lock (_hasWorkConditionVariable)
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

            protected void Run()
            {
                while (!_needToQuit)
                {
                    _hasWorkConditionVariable.WaitForCondition(() => _needToQuit || _pathsToScan.Count > 0);
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
                lock (_hasWorkConditionVariable)
                {
                    _pathsToScan.Clear();
                    _needToQuit = true;
                }
                _hasWorkConditionVariable.Notify();
            }

            public void Add(string path)
            {
                lock (_hasWorkConditionVariable)
                {
                    _pathsToScan.Add(path);
                }
                _hasWorkConditionVariable.Notify();
            }

            public void Clear()
            {
                lock (_hasWorkConditionVariable)
                {
                    _pathsToScan.Clear();
                }
            }
        }
    }
}
