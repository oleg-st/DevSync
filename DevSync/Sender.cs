using DevSyncLib;
using DevSyncLib.Command;
using DevSyncLib.Logger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DevSync
{
    public partial class Sender : IDisposable
    {
        private const int ShutdownTimeout = 5000;

        private readonly ILogger _logger;
        private volatile bool _needToScan, _needToQuit, _gitIsBusy;
        private FileSystemWatcher _fileSystemWatcher;

        private readonly string _srcPath;

        private readonly FileMaskList _excludeList;

        private readonly Dictionary<string, FsSenderChange> _changes;
        private readonly AgentStarter _agentStarter;

        private const string GitIndexLockFilename = ".git/index.lock";

        private readonly ManualResetEvent _hasWorkEvent = new ManualResetEvent(false);
        private readonly ManualResetEvent _gitIsReadyEvent = new ManualResetEvent(true);

        private readonly ApplyRequest _applyRequest;
        private readonly PathScanner _pathScanner;
        private readonly SentReporter _sentReporter;
        private bool _isSending;
        private readonly CancellationTokenSource _cancellationTokenSource;

        protected class SentReporter
        {
            private SlimStopwatch _stopwatch;
            private int _totalCount;
            private long _totalSize;
            private readonly ILogger _logger;
            private string _lastChange;
            private const int ReportInterval = 1000;
            private TimeSpan _timeSpan;

            public SentReporter(ILogger logger)
            {
                _logger = logger;
                _stopwatch = SlimStopwatch.Create();
                _timeSpan = TimeSpan.Zero;
            }

            public void Report(List<FsSenderChange> changes, long size, TimeSpan timeSpan)
            {
                _totalCount += changes.Count;
                _totalSize += size;
                _timeSpan += timeSpan;
                if (_totalCount == 1 && changes.Count == 1)
                {
                    _lastChange = changes.First().ToString();
                }
                if (!_stopwatch.IsRunning || _stopwatch.ElapsedMilliseconds > ReportInterval)
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
                    ? $"Sent {_lastChange} in {(int)_timeSpan.TotalMilliseconds} ms"
                    : $"Sent {_totalCount} changes, {PrettySize(_totalSize)} in {(int)_timeSpan.TotalMilliseconds} ms");

                _lastChange = "";
                _totalCount = 0;
                _totalSize = 0;
                _timeSpan = TimeSpan.Zero;
                _stopwatch.Start();
            }
        }

        public Sender(SyncOptions syncOptions, ILogger logger)
        {
            _logger = logger;
            _pathScanner = new PathScanner(this);
            _sentReporter = new SentReporter(_logger);
            _srcPath = Path.GetFullPath(syncOptions.SourcePath);
            _cancellationTokenSource = new CancellationTokenSource();

            if (!Directory.Exists(_srcPath))
            {
                throw new SyncException($"Invalid source path: {_srcPath}");
            }

            _excludeList = new FileMaskList();
            _excludeList.SetList(syncOptions.ExcludeList);

            _changes = new Dictionary<string, FsSenderChange>();
            _needToScan = true;
            _agentStarter = AgentStarter.Create(syncOptions, _logger);
            _logger.Log($"Sync {syncOptions}");
            _applyRequest = new ApplyRequest(_logger)
            {
                BasePath = _srcPath,
            };
            UpdateHasWork();
        }

        private void UpdateHasWork()
        {
            lock (_changes)
            {
                if (HasWork)
                {
                    _hasWorkEvent.Set();
                }
                else
                {
                    _hasWorkEvent.Reset();
                }

                if (_gitIsBusy)
                {
                    _gitIsReadyEvent.Reset();
                }
                else
                {
                    _gitIsReadyEvent.Set();
                }

            }
        }

        private void Scan()
        {
            var sw = SlimStopwatch.StartNew();
            List<FsEntry> srcList = null;
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
            using (var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token))
            {
                var cancellationToken = tokenSource.Token;
                cancellationToken.ThrowIfCancellationRequested();

                _agentStarter.Start();

                // scan source
                var task = Task.Run(() =>
                {
                    try
                    {
                        var swScanSource = SlimStopwatch.StartNew();
                        var scanDirectory =
                            new ScanDirectory(_logger, _excludeList, cancellationToken: cancellationToken);
                        srcList = scanDirectory.ScanPath(_srcPath).ToList();
                        cancellationToken.ThrowIfCancellationRequested();
                        _logger.Log($"Scanned source {srcList.Count} items in {swScanSource.ElapsedMilliseconds} ms");
                    }
                    catch (OperationCanceledException)
                    {
                        srcList = null;
                    }
                }, cancellationToken);

                try
                {
                    var swScanDestination = SlimStopwatch.StartNew();
                    // scan destination
                    var response = _agentStarter.SendCommand<ScanResponse>(new ScanRequest(_logger));
                    destList = response.FileList.ToDictionary(x => x.Path, y => y);
                    _logger.Log(
                        $"Scanned destination {destList.Count} items in {swScanDestination.ElapsedMilliseconds} ms");
                    task.Wait(cancellationToken);
                }
                catch (Exception)
                {
                    tokenSource.Cancel();
                    throw;
                }
            }

            // During scan, changes could come from file system events or from PathScanner, we should not overwrite them.
            var itemsCount = 0;
            long changesSize = 0;
            lock (_changes)
            {
                foreach (var srcEntry in srcList)
                {
                    if (!destList.TryGetValue(srcEntry.Path, out var destEntry))
                    {
                        destEntry = FsEntry.Empty;
                    }

                    // Skip changed srcEntry
                    if (!_changes.ContainsKey(srcEntry.Path))
                    {
                        // add to changes (no replace)
                        if (!srcEntry.Equals(destEntry))
                        {
                            itemsCount++;
                            if (!srcEntry.IsDirectory)
                            {
                                changesSize += srcEntry.Length;
                            }
                            AddChange(FsSenderChange.CreateChange(srcEntry), false);
                        }
                    }

                    if (!destEntry.IsEmpty)
                    {
                        destList.Remove(destEntry.Path);
                    }
                }

                // add deletes
                foreach (var destEntry in destList.Values)
                {
                    // Skip changed destEntry
                    if (!_changes.ContainsKey(destEntry.Path))
                    {
                        itemsCount++;
                        AddChange(FsSenderChange.CreateRemove(destEntry.Path), false);
                    }
                }
            }

            _needToScan = false;
            _logger.Log(
                $"Scanned in {sw.ElapsedMilliseconds} ms, {itemsCount} items, {PrettySize(changesSize)} to send");
            UpdateHasWork();
        }

        private void AddChange(FsSenderChange fsSenderChange, bool notifyHasWork = true, bool withSubdirectories = false)
        {
            // ignore empty path
            if (string.IsNullOrEmpty(fsSenderChange.Path))
            {
                return;
            }

            lock (_changes)
            {
                if (_changes.TryGetValue(fsSenderChange.Path, out var oldFsChange))
                {
                    oldFsChange.Expired = true;
                }

                if (fsSenderChange.ChangeType == FsChangeType.Rename &&
                    _changes.TryGetValue(fsSenderChange.OldPath, out var oldPathFsChange))
                {
                    // rename -> delete old, create new
                    oldPathFsChange.Expired = true;
                    _changes[fsSenderChange.OldPath] = FsSenderChange.CreateRemove(fsSenderChange.OldPath);
                    fsSenderChange = FsSenderChange.CreateChange(fsSenderChange.Path);
                }

                _changes[fsSenderChange.Path] = fsSenderChange;
            }

            if (withSubdirectories)
            {
                _pathScanner.Add(fsSenderChange.Path);
            }

            if (notifyHasWork)
            {
                UpdateHasWork();
            }
        }

        private string GetPath(string fullPath)
        {
            return FsEntry.NormalizePath(Path.GetRelativePath(_srcPath, fullPath));
        }

        private void OnWatcherChanged(object source, FileSystemEventArgs e)
        {
            var path = GetPath(e.FullPath);
            // ignore event for srcPath (don't know why it occurs rarely)
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (!_gitIsBusy && e.ChangeType == WatcherChangeTypes.Created && path == GitIndexLockFilename)
            {
                SetGitIsBusy(true);
            }

            if (_excludeList.IsMatch(path))
            {
                return;
            }
            AddChange(FsSenderChange.CreateChange(path), withSubdirectories: e.ChangeType == WatcherChangeTypes.Created);
        }

        private void OnWatcherDeleted(object source, FileSystemEventArgs e)
        {
            var path = GetPath(e.FullPath);
            // ignore event for srcPath (don't know why it occurs rarely)
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (_gitIsBusy && path == GitIndexLockFilename)
            {
                SetGitIsBusy(false);
            }

            if (_excludeList.IsMatch(path))
            {
                return;
            }

            AddChange(FsSenderChange.CreateRemove(path));
        }

        private void OnWatcherRenamed(object source, RenamedEventArgs e)
        {
            var path = GetPath(e.FullPath);
            var oldPath = GetPath(e.OldFullPath);
            // ignore event for srcPath (don't know why it occurs rarely)
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(oldPath))
            {
                return;
            }

            if (_gitIsBusy && oldPath == GitIndexLockFilename)
            {
                SetGitIsBusy(false);
            }

            // is new file excluded?
            if (_excludeList.IsMatch(path))
            {
                // old file is not excluded -> delete it
                if (!_excludeList.IsMatch(oldPath))
                {
                    AddChange(FsSenderChange.CreateRemove(oldPath));
                }

                // both files are excluded -> do nothing
            }
            else // new file is not excluded
            {
                // old file is excluded -> send change with withSubdirectories
                if (_excludeList.IsMatch(oldPath))
                {
                    AddChange(FsSenderChange.CreateChange(path), withSubdirectories: true);
                }
                else
                {
                    // both files are not excluded -> send rename
                    AddChange(FsSenderChange.CreateRename(path, oldPath));
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

        private bool HasWork => _needToQuit || _needToScan || HasChanges;
        private void SetGitIsBusy(bool value)
        {
            _gitIsBusy = value;
            UpdateHasWork();
        }

        private void WaitForWork()
        {
            const int readyTimeout = 300;
            var waitForReady = true;
            var sw = SlimStopwatch.StartNew();
            while (!HasWork)
            {
                var timeout = Timeout.Infinite;
                var waitForGit = false;
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
                            _isSending = false;
                        }
                        else
                        {
                            waitForGit = true;
                        }
                    }
                    else
                    {
                        timeout = readyTimeout - (int)elapsed;
                    }
                }

                if (waitForGit)
                {
                    WaitHandle.WaitAny(new WaitHandle[] { _hasWorkEvent, _gitIsReadyEvent }, timeout);
                }
                else
                {
                    _hasWorkEvent.WaitOne(timeout);
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
            lock (_changes)
            {
                if (_changes.Count == 0)
                {
                    return true;
                }

                // filter not ready changes
                _applyRequest.SetChanges(_changes.Values.Where(x => x.IsReady));
            }

            // nothing to send -> has not ready changes
            if (!_applyRequest.HasChanges)
            {
                // waiting for change is getting ready or we get new ready changes
                Thread.Sleep(FsSenderChange.WaitForReadyTimeoutMs);
                return true;
            }

            if (!_isSending)
            {
                _logger.Log("Sending");
                _isSending = true;
            }

            var sw = SlimStopwatch.StartNew();
            var response = _agentStarter.SendCommand<ApplyResponse>(_applyRequest);
            var responseResult = response.Result.ToDictionary(x => x.Key, y => y);

            bool hasErrors = false;
            // process sent changes
            lock (_changes)
            {
                foreach (var fsChange in _applyRequest.SentChanges)
                {
                    if (!fsChange.Expired)
                    {
                        _changes.Remove(fsChange.Path);
                        if (responseResult.TryGetValue(fsChange.Path, out var fsChangeResult))
                        {
                            if (fsChangeResult.ResultCode != FsChangeResultCode.Ok)
                            {
                                var withSubdirectories = false;
                                // ignore sender errors: just resend
                                if (fsChangeResult.ResultCode != FsChangeResultCode.SenderError)
                                {
                                    // if rename failed -> send change with withSubdirectories
                                    if (fsChange.ChangeType == FsChangeType.Rename)
                                    {
                                        withSubdirectories = true;
                                    }
                                    else
                                    {
                                        hasErrors = true;
                                        _logger.Log(
                                            $"Change apply error {fsChange.ChangeType} {fsChange.Path}: {fsChangeResult.ErrorMessage ?? "-"}", LogLevel.Error);
                                    }
                                }
                                AddChange(FsSenderChange.CreateChange(fsChange.Path), false, withSubdirectories);
                            }
                        }
                    }
                    else
                    {
                        // remove expired
                        if (_changes.TryGetValue(fsChange.Path, out var oldFsChange) && oldFsChange.Expired)
                        {
                            _changes.Remove(fsChange.Path);
                        }
                    }
                }
            }

            _sentReporter.Report(_applyRequest.SentChanges, _applyRequest.SentChangesSize, sw.Elapsed);
            _applyRequest.ClearChanges();
            UpdateHasWork();
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

            _pathScanner.Start();

            while (!_needToQuit)
            {
                var needToWait = false;
                try
                {

                    // scan
                    if (_needToScan)
                    {
                        Scan();
                    }

                    if (!SendChanges())
                    {
                        needToWait = true;
                    }
                }
                catch (SyncException ex)
                {
                    if (_needToQuit)
                    {
                        break;
                    }
                    _logger.Log(ex.Message, LogLevel.Error);
                    if (!ex.Recoverable)
                    {
                        break;
                    }
                    needToWait = ex.NeedToWait;
                }
                catch (SyncInterruptException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (_needToQuit)
                    {
                        break;
                    }

                    _logger.Log(ex.Message, LogLevel.Error);
                    needToWait = true;
                }

                if (_needToQuit)
                {
                    break;
                }

                if (needToWait)
                {
                    const int intervalMs = 1000;
                    _logger.Log($"Waiting for {intervalMs} ms");
                    // TODO: dynamic interval?
                    _cancellationTokenSource.Token.WaitHandle.WaitOne(intervalMs);
                }

                WaitForWork();
            }
        }

        private void FileSystemWatcherOnError(object sender, ErrorEventArgs e)
        {
            _logger.Log($"File system watcher error: {e.GetException()}", LogLevel.Error);
            lock (_changes)
            {
                _needToScan = true;
                _pathScanner.Clear();
                SetGitIsBusy(false);
                _changes.Clear();
            }

            UpdateHasWork();
        }

        private async Task WaitForShutdown()
        {
            await Task.Delay(ShutdownTimeout).ConfigureAwait(false);
            _logger.Log("Shutdown timed out", LogLevel.Warning);
            Environment.Exit(-1);
        }

        private void Stop()
        {
            if (_cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            Task.Factory.StartNew(WaitForShutdown, TaskCreationOptions.LongRunning);
            _needToQuit = true;
            _cancellationTokenSource.Cancel();
            _agentStarter.Stop();
            _pathScanner.Stop();
            UpdateHasWork();
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
