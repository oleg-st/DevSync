using DevSyncLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DevSync;

public partial class Sender
{
    public class PathScanner(Sender sender)
    {
        private volatile bool _needToQuit;
        private readonly ManualResetEvent _hasWorkEvent = new(false);
        private readonly HashSet<string> _pathsToScan = [];
        private readonly CancellationTokenSource _cancellationTokenSource = new();

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
                var scanDirectory = new ScanDirectory(sender._logger, sender._excludeList,
                    false, _cancellationTokenSource.Token);
                foreach (var srcEntry in scanDirectory.ScanPath(sender._srcPath, path))
                {
                    sender.AddChange(FsSenderChange.CreateChange(srcEntry.Path));
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void DoWork()
        {
            string? path;
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