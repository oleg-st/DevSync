using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevSyncLib;
using DevSyncLib.Command;
using DevSyncLib.Logger;
using Task = System.Threading.Tasks.Task;

namespace DevSync
{
    public partial class Sender : IDisposable
    {
        private readonly ILogger _logger;
        private bool _needScan;
        private volatile bool _needQuit;
        private bool _gitIsBusy;
        private FileSystemWatcher _fileSystemWatcher;

        private readonly string _srcPath;

        private readonly FileMaskList _excludeList;

        private readonly Dictionary<string, FsChange> _changes;
        private long _changesSize;
        private readonly AgentStarter _agentStarter;

        // max items in chunk
        private const int CHANGES_MAX_COUNT = 2500;
        // max items body size in chunk (soft limit)
        private const int CHANGES_MAX_SIZE = 100 * 1024 * 1024;
        private const string GIT_INDEX_LOCK_FILENAME = ".git/index.lock";
        private readonly object _syncHasWork = new object();
        private readonly ApplyRequest _applyRequest;
        private readonly PathScanner _pathScanner;
        private readonly SentReporter _sentReporter;
        private bool _isReady;

        protected class SentReporter
        {
            private readonly Stopwatch _stopwatch;
            private int _totalCount;
            private long _totalSize;
            private readonly ILogger _logger;
            private string _lastChange;
            private const int REPORT_INTERVAL = 1000;

            public SentReporter(ILogger logger)
            {
                _logger = logger;
                _stopwatch = new Stopwatch();
            }

            public void Report(List<FsChange> changes, long size)
            {
                _totalCount += changes.Count;
                _totalSize += size;
                if (_totalCount == 1 && changes.Count == 1)
                {
                    _lastChange =  changes.First().ToString();
                }
                if (!_stopwatch.IsRunning || _stopwatch.ElapsedMilliseconds > REPORT_INTERVAL)
                {
                    Flush();
                }
            }

            public void Flush()
            {
                if (_totalCount <= 0)
                {
                    return;
                }

                _logger.Log(_totalCount == 1
                    ? _lastChange
                    : $"Sent {_totalCount} changes, {PrettySize(_totalSize)}");

                _lastChange = "";
                _totalCount = 0;
                _totalSize = 0;
                _stopwatch.Restart();
            }
        }

        public Sender(SyncOptions syncOptions, ILogger logger)
        {
            _logger = logger;
            _pathScanner = new PathScanner(this);
            _sentReporter = new SentReporter(_logger);

            if (!Directory.Exists(syncOptions.SourcePath))
            {
                throw new SyncException($"Invalid source path: {syncOptions.SourcePath}");
            }

            _srcPath = syncOptions.SourcePath;
            _excludeList = new FileMaskList(); 
            _excludeList.SetList(syncOptions.ExcludeList);

            _changes = new Dictionary<string, FsChange>();
            _changesSize = 0;
            _needScan = true;
            _agentStarter = AgentStarter.Create(syncOptions, _logger);
            _logger.Log($"Sync {syncOptions}");
            _applyRequest = new ApplyRequest(_logger)
            {
                BasePath = _srcPath,
                Changes = new List<FsChange>(CHANGES_MAX_COUNT)
            };
        }

        private void Scan()
        {
            var sw = Stopwatch.StartNew();
            Dictionary<string, FsEntry> srcList = null;
            Dictionary<string, FsEntry> destList;
            /*
             * Start agent before scan source
             *
             * Old timeline: [Main thread]       Start ... Initialize ... Scan destination ... Finish
             *               [Secondary thread]  Scan source ................................. Finish
             *
             * New timeline: [Main thread]       Start ... Initialize ... Scan destination ... Finish
             *               [Secondary thread]            Scan source ....................... Finish
             *
             * A failed start could cause unnecessary scanning source in old timeline.
             * No need to scan source before start in most cases because it is about as fast as the scan destination.
             */
            _agentStarter.Start();

            using (var tokenSource = new CancellationTokenSource())
            {
                var cancellationToken = tokenSource.Token;

                // scan source
                var task = Task.Run(() =>
                {
                    try
                    {
                        var scanDirectory =
                            new ScanDirectory(_logger, _excludeList, cancellationToken: cancellationToken);
                        srcList = scanDirectory.ScanPath(_srcPath).ToDictionary(x => x.Path, y => y);
                        cancellationToken.ThrowIfCancellationRequested();
                        _logger.Log($"Scanned source {srcList.Count} items in {sw.ElapsedMilliseconds} ms");
                    }
                    catch (OperationCanceledException)
                    {
                        srcList = null;
                    }
                }, cancellationToken);

                try
                {
                    // scan destination
                    var response = _agentStarter.SendCommand<ScanResponse>(new ScanRequest(_logger));
                    destList = response.FileList.ToDictionary(x => x.Path, y => y);
                    _logger.Log($"Scanned destination {destList.Count} items in {sw.ElapsedMilliseconds} ms");
                    task.Wait(cancellationToken);
                }
                catch (Exception)
                {
                    tokenSource.Cancel();
                    throw;
                }
            }

            int itemsCount = 0;
            foreach (var srcEntry in srcList.Values)
            {
                if (!destList.TryGetValue(srcEntry.Path, out var destEntry))
                {
                    destEntry = FsEntry.Empty;
                }

                // add to changes
                if (!srcEntry.Compare(destEntry))
                {
                    var fsChange = new FsChange {ChangeType = FsChangeType.Change, FsEntry = srcEntry};
                    itemsCount++;
                    AddChange(fsChange, false);
                }

                // remove from dest list
                if (!destEntry.IsEmpty)
                {
                    destList.Remove(destEntry.Path);
                }
            }

            // delete
            foreach (var destEntry in destList.Values)
            {
                itemsCount++;
                AddChange(new FsChange {ChangeType = FsChangeType.Remove, FsEntry = destEntry}, false);
            }

            _needScan = false;
            NotifyHasWork();
            _logger.Log(
                $"Scanned in {sw.ElapsedMilliseconds} ms, {itemsCount} items, {PrettySize(_changesSize)} to send");
        }

        private void AddChange(FsChange fsChange, bool notifyHasWork = true)
        {
            lock (_changes)
            {
                if (_changes.TryGetValue(fsChange.Key, out var oldFsChange))
                {
                    oldFsChange.Expired = true;
                    _changesSize -= oldFsChange.BodySize;
                }

                _changes[fsChange.Key] = fsChange;
                _changesSize += fsChange.BodySize;
            }

            if (notifyHasWork)
            {
                NotifyHasWork();
            }
        }

        private string GetPath(string fullPath)
        {
            return FsEntry.NormalizePath(Path.GetRelativePath(_srcPath, fullPath));
        }
        
        private void OnWatcherChanged(object source, FileSystemEventArgs e)
        {
            var path = GetPath(e.FullPath);
            if (!_gitIsBusy && e.ChangeType == WatcherChangeTypes.Created && path == GIT_INDEX_LOCK_FILENAME)
            {
                SetGitIsBusy(true);
            }

            if (_excludeList.IsMatch(path))
            {
                return;
            }

            var fsChange = FsChange.FromFilename(e.FullPath, path);
            AddChange(fsChange);
            // scan new directory
            if (e.ChangeType == WatcherChangeTypes.Created && fsChange.FsEntry.IsDirectory)
            {
                _pathScanner.Add(fsChange.FsEntry.Path);
            }
        }

        private void OnWatcherDeleted(object source, FileSystemEventArgs e)
        {
            var path = GetPath(e.FullPath);
            if (_gitIsBusy && path == GIT_INDEX_LOCK_FILENAME)
            {
                SetGitIsBusy(false);
            }

            if (_excludeList.IsMatch(path))
            {
                return;
            }

            AddChange(FsChange.FromFilename(e.FullPath, path));
        }

        private void OnWatcherRenamed(object source, RenamedEventArgs e)
        {
            var path = GetPath(e.FullPath);
            var oldPath = GetPath(e.OldFullPath);
            if (_gitIsBusy && oldPath == GIT_INDEX_LOCK_FILENAME)
            {
                SetGitIsBusy(false);
            }

            // is new file excluded?
            if (_excludeList.IsMatch(path))
            {
                // old file is not excluded -> delete it
                if (!_excludeList.IsMatch(oldPath))
                {
                    AddChange(FsChange.FromFilename(e.OldFullPath, oldPath));
                }

                // both files are excluded -> do nothing
            }
            else // new file is not excluded
            {
                // old file is excluded -> send new file
                if (_excludeList.IsMatch(oldPath))
                {                    
                    AddChange(FsChange.FromFilename(e.FullPath, path));
                }
                else
                {
                    // both files are excluded -> send rename
                    var fsChange = FsChange.FromFilename(e.FullPath, path);
                    var oldFsChange = FsChange.FromFilename(e.OldFullPath, oldPath);
                    fsChange.ChangeType = FsChangeType.Rename;
                    fsChange.OldFsEntry = oldFsChange.FsEntry;
                    AddChange(fsChange);
                }
            }
        }

        private bool HasChanges
        {
            get
            {
                lock (_changes)
                {
                    return _changes.Count > 0;
                }
            }
        }

        private bool HasWork => _needQuit || _needScan || HasChanges;
        private void SetGitIsBusy(bool value)
        {
            _gitIsBusy = value;
            NotifyHasWork();
        }

        private void NotifyHasWork()
        {
            lock (_syncHasWork)
            {
                Monitor.Pulse(_syncHasWork);
            }
        }

        private void WaitForWork()
        {
            const int readyTimeout = 300;
            var waitForReady = true;
            var sw = Stopwatch.StartNew();
            while (!HasWork)
            {
                var timeout = Timeout.Infinite;
                if (waitForReady)
                {
                    var elapsed = sw.ElapsedMilliseconds;
                    // no work for some time and git is not busy
                    if (elapsed >= readyTimeout)
                    {
                        if (!_gitIsBusy)
                        {
                            _sentReporter.Flush();
                            _logger.Log("Ready");
                            waitForReady = false;
                            _isReady = true;
                        }
                    }
                    else
                    {
                        timeout = readyTimeout - (int)elapsed;
                    }
                }

                lock (_syncHasWork)
                {
                    Monitor.Wait(_syncHasWork, timeout);
                }
            }

            if (_isReady)
            {
                _isReady = false;
                if (HasChanges)
                {
                    _logger.Log("Sending");
                }
            }
        }

        private static string PrettySize(long size)
        {
            string[] sizes = { "bytes", "KB", "MB", "GB", "TB" };
            double doubleSize = size;
            var order = 0;
            while (doubleSize >= 1024 && order < sizes.Length - 1)
            {
                order++;
                doubleSize /= 1024;
            }
            return $"{doubleSize:0.##} {sizes[order]}";
        }


        private bool SendChanges()
        {
            // fetch changes
            long totalSize = 0;
            lock (_changes)
            {
                if (_changes.Count == 0)
                {
                    return true;
                }

                _applyRequest.Changes.Clear();
                int itemsCount = 0;
                foreach (var fsChange in _changes.Values)
                {
                    if (itemsCount >= CHANGES_MAX_COUNT || totalSize >= CHANGES_MAX_SIZE)
                    {
                        break;
                    }

                    _applyRequest.Changes.Add(fsChange);
                    itemsCount++;
                    totalSize += fsChange.BodySize;
                }
            }

            var response = _agentStarter.SendCommand<ApplyResponse>(_applyRequest);
            var responseResult = response.Result.ToDictionary(x => x.Key, y => y);

            bool hasErrors = false;
            // process sent changes
            lock (_changes)
            {
                foreach (var fsChange in _applyRequest.Changes)
                {
                    if (!fsChange.Expired)
                    {
                        _changes.Remove(fsChange.Key);
                        _changesSize -= fsChange.BodySize;
                        if (responseResult.TryGetValue(fsChange.Key, out var fsChangeResult))
                        {
                            if (fsChangeResult.ResultCode != FsChangeResultCode.Ok)
                            {
                                // ignore sender errors: just resend
                                if (fsChangeResult.ResultCode != FsChangeResultCode.SenderError)
                                {
                                    // if rename failed -> send entire file
                                    if (fsChange.ChangeType == FsChangeType.Rename)
                                    {
                                        fsChange.ChangeType = FsChangeType.Change;
                                        // scan if directory
                                        if (fsChange.FsEntry.IsDirectory)
                                        {
                                            _pathScanner.Add(fsChange.FsEntry.Path);
                                        }
                                    }
                                    else
                                    {
                                        hasErrors = true;
                                        _logger.Log(
                                            $"Change apply error {fsChange.ChangeType} {fsChange.FsEntry.Path}: {fsChangeResult.ErrorMessage ?? "-"}", LogLevel.Error);
                                    }
                                }
                                // resend
                                _changes.Add(fsChange.Key, fsChange);
                                _changesSize += fsChange.BodySize;
                            }
                        }
                    }
                    else
                    {
                        // remove expired
                        if (_changes.TryGetValue(fsChange.Key, out var oldFsChange) && oldFsChange.Expired)
                        {
                            _changes.Remove(fsChange.Key);
                            _changesSize -= fsChange.BodySize;
                        }
                    }
                }
            }

            NotifyHasWork();
            _sentReporter.Report(_applyRequest.Changes, totalSize);

            return !hasErrors;
        }

        public void Run()
        {
            Console.CancelKeyPress += ConsoleOnCancelKeyPress;

            _fileSystemWatcher = new FileSystemWatcher(_srcPath);
            _fileSystemWatcher.Changed += OnWatcherChanged;
            _fileSystemWatcher.Created += OnWatcherChanged;
            _fileSystemWatcher.Deleted += OnWatcherDeleted;
            _fileSystemWatcher.Renamed += OnWatcherRenamed;
            _fileSystemWatcher.Error += FileSystemWatcherOnError;
            _fileSystemWatcher.InternalBufferSize = 64 * 1024;
            _fileSystemWatcher.IncludeSubdirectories = true;
            _fileSystemWatcher.EnableRaisingEvents = true;

            Task.Factory.StartNew(_pathScanner.Run, TaskCreationOptions.LongRunning);

            while (!_needQuit)
            {
                var hasErrors = false;
                try
                {

                    // scan
                    if (_needScan)
                    {
                        Scan();
                    }

                    if (!SendChanges())
                    {
                        hasErrors = true;
                    }
                }
                catch (SyncException ex)
                {
                    if (_needQuit)
                    {
                        break;
                    }
                    _logger.Log(ex.Message, LogLevel.Error);
                    if (!ex.Recoverable)
                    {
                        break;
                    }
                    hasErrors = true;
                }
                catch (Exception ex)
                {
                    if (_needQuit)
                    {
                        break;
                    }

                    _logger.Log(ex.Message, LogLevel.Error);
                    hasErrors = true;
                }

                if (_needQuit)
                {
                    break;
                }

                if (hasErrors)
                {
                    _logger.Log("Waiting");
                    // TODO: dynamic delay?
                    Thread.Sleep(1000);
                }

                WaitForWork();
            }
        }

        private void FileSystemWatcherOnError(object sender, ErrorEventArgs e)
        {
            _logger.Log($"FileSystemWatcherOnError {e.GetException()}", LogLevel.Error);
            _needScan = true;
            _pathScanner.Clear();
            lock (_changes)
            {
                _changes.Clear();
                _changesSize = 0;
            }
            NotifyHasWork();
        }

        private void Stop()
        {
            _needQuit = true;
            _agentStarter.Stop();
            _pathScanner.Stop();
            NotifyHasWork();
        }

        private void ConsoleOnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Stop();
            e.Cancel = true;
        }

        public void Dispose()
        {
            _agentStarter.Dispose();
            _fileSystemWatcher?.Dispose();
        }
    }
}
