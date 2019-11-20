﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using DevSyncLib;
using DevSyncLib.Command;
using DevSyncLib.Logger;
using Task = System.Threading.Tasks.Task;

namespace DevSync
{
    public partial class Sender : IDisposable
    {
        private readonly ILogger _logger;
        private bool _needToScan;
        private volatile bool _needToQuit;
        private bool _gitIsBusy;
        private FileSystemWatcher _fileSystemWatcher;

        private readonly string _srcPath;

        private readonly FileMaskList _excludeList;

        private readonly Dictionary<string, FsChange> _changes;
        private readonly AgentStarter _agentStarter;

        private const string GIT_INDEX_LOCK_FILENAME = ".git/index.lock";

        private readonly ConditionVariable _hasWorkConditionVariable;

        private readonly ApplyRequest _applyRequest;
        private readonly PathScanner _pathScanner;
        private readonly SentReporter _sentReporter;
        private bool _isSending;

        protected class SentReporter
        {
            private readonly Stopwatch _stopwatch;
            private int _totalCount;
            private long _totalSize;
            private readonly ILogger _logger;
            private string _lastChange;
            private const int REPORT_INTERVAL = 1000;
            private TimeSpan _timeSpan;

            public SentReporter(ILogger logger)
            {
                _logger = logger;
                _stopwatch = new Stopwatch();
                _timeSpan = TimeSpan.Zero;
            }

            public void Report(List<FsChange> changes, long size, TimeSpan timeSpan)
            {
                _totalCount += changes.Count;
                _totalSize += size;
                _timeSpan += timeSpan;
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
                    : $"Sent {_totalCount} changes, {PrettySize(_totalSize)} in {(int)_timeSpan.TotalMilliseconds} ms");

                _lastChange = "";
                _totalCount = 0;
                _totalSize = 0;
                _timeSpan = TimeSpan.Zero;
                _stopwatch.Restart();
            }
        }

        public Sender(SyncOptions syncOptions, ILogger logger)
        {
            _logger = logger;
            _pathScanner = new PathScanner(this);
            _sentReporter = new SentReporter(_logger);
            _hasWorkConditionVariable = new ConditionVariable();
            _srcPath = Path.GetFullPath(syncOptions.SourcePath);

            if (!Directory.Exists(_srcPath))
            {
                throw new SyncException($"Invalid source path: {_srcPath}");
            }

            _excludeList = new FileMaskList(); 
            _excludeList.SetList(syncOptions.ExcludeList);

            _changes = new Dictionary<string, FsChange>();
            _needToScan = true;
            _agentStarter = AgentStarter.Create(syncOptions, _logger);
            _logger.Log($"Sync {syncOptions}");
            _applyRequest = new ApplyRequest(_logger)
            {
                BasePath = _srcPath,
            };
        }

        private void Scan()
        {
            var sw = Stopwatch.StartNew();
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
            _agentStarter.Start();

            using (var tokenSource = new CancellationTokenSource())
            {
                var cancellationToken = tokenSource.Token;

                // scan source
                var task = Task.Run(() =>
                {
                    try
                    {
                        var swScanSource = Stopwatch.StartNew();
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
                    var swScanDestination = Stopwatch.StartNew();
                    // scan destination
                    var response = _agentStarter.SendCommand<ScanResponse>(new ScanRequest(_logger));
                    destList = response.FileList.ToDictionary(x => x.Path, y => y);
                    _logger.Log($"Scanned destination {destList.Count} items in {swScanDestination.ElapsedMilliseconds} ms");
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
            lock (_hasWorkConditionVariable)
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
                        if (!srcEntry.Compare(destEntry))
                        {
                            itemsCount++;
                            if (!srcEntry.IsDirectory)
                            {
                                changesSize += srcEntry.Length;
                            }
                            AddChange(FsChange.CreateChange(srcEntry), false);
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
                        AddChange(FsChange.CreateRemove(destEntry.Path), false);
                    }
                }
            }

            _needToScan = false;
            _logger.Log(
                $"Scanned in {sw.ElapsedMilliseconds} ms, {itemsCount} items, {PrettySize(changesSize)} to send");
        }
        
        private void AddChange(FsChange fsChange, bool notifyHasWork = true, bool withSubdirectories = false)
        {
            lock (_hasWorkConditionVariable)
            {
                if (_changes.TryGetValue(fsChange.Path, out var oldFsChange))
                {
                    oldFsChange.Expired = true;
                }
                _changes[fsChange.Path] = fsChange;
            }

            if (withSubdirectories)
            {
                _pathScanner.Add(fsChange.Path);
            }

            if (notifyHasWork)
            {
                _hasWorkConditionVariable.Notify();
            }
        }

        private string GetPath(string fullPath)
        {
            return FsEntry.NormalizePath(Path.GetRelativePath(_srcPath, fullPath));
        }
        
        private void OnWatcherChanged(object source, FileSystemEventArgs e)
        {
            // ignore event for srcPath (don't know why it occurs rarely)
            if (e.FullPath == _srcPath)
            {
                return;
            }

            var path = GetPath(e.FullPath);
            if (!_gitIsBusy && e.ChangeType == WatcherChangeTypes.Created && path == GIT_INDEX_LOCK_FILENAME)
            {
                SetGitIsBusy(true);
            }

            if (_excludeList.IsMatch(path))
            {
                return;
            }
            AddChange(FsChange.CreateChange(path), withSubdirectories: e.ChangeType == WatcherChangeTypes.Created);
        }

        private void OnWatcherDeleted(object source, FileSystemEventArgs e)
        {
            // ignore event for srcPath (don't know why it occurs rarely)
            if (e.FullPath == _srcPath)
            {
                return;
            }

            var path = GetPath(e.FullPath);
            if (_gitIsBusy && path == GIT_INDEX_LOCK_FILENAME)
            {
                SetGitIsBusy(false);
            }

            if (_excludeList.IsMatch(path))
            {
                return;
            }

            AddChange(FsChange.CreateRemove(path));
        }

        private void OnWatcherRenamed(object source, RenamedEventArgs e)
        {
            // ignore event for srcPath (don't know why it occurs rarely)
            if (e.FullPath == _srcPath || e.OldFullPath == _srcPath)
            {
                return;
            }

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
                    AddChange(FsChange.CreateRemove(oldPath));
                }

                // both files are excluded -> do nothing
            }
            else // new file is not excluded
            {
                // old file is excluded -> send change with withSubdirectories
                if (_excludeList.IsMatch(oldPath))
                {                    
                    AddChange(FsChange.CreateChange(path), withSubdirectories: true);
                }
                else
                {
                    // both files are not excluded -> send rename
                    AddChange(FsChange.CreateRename(path, oldPath));
                }
            }
        }

        private bool HasChanges
        {
            get
            {
                lock (_hasWorkConditionVariable)
                {
                    return _changes.Count > 0;
                }
            }
        }

        private bool HasWork => _needToQuit || _needToScan || HasChanges;
        private void SetGitIsBusy(bool value)
        {
            _gitIsBusy = value;
            _hasWorkConditionVariable.Notify();
        }

        private void WaitForWork()
        {
            const int readyTimeout = 300;
            var waitForReady = true;
            var sw = Stopwatch.StartNew();
            while (!HasWork)
            {
                lock (_hasWorkConditionVariable)
                {
                    if (!HasWork)
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
                                    _isSending = false;
                                }
                            }
                            else
                            {
                                timeout = readyTimeout - (int) elapsed;
                            }
                        }

                        _hasWorkConditionVariable.Wait(timeout);
                    }
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
            lock (_hasWorkConditionVariable)
            {
                if (_changes.Count == 0)
                {
                    return true;
                }
                _applyRequest.SetChanges(_changes.Values);
            }

            if (!_isSending)
            {
                _logger.Log("Sending");
                _isSending = true;
            }

            var sw = Stopwatch.StartNew();
            var response = _agentStarter.SendCommand<ApplyResponse>(_applyRequest);
            var responseResult = response.Result.ToDictionary(x => x.Key, y => y);

            bool hasErrors = false;
            // process sent changes
            lock (_hasWorkConditionVariable)
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
                                AddChange(FsChange.CreateChange(fsChange.Path), false, withSubdirectories);
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
                    Thread.Sleep(intervalMs);
                }

                WaitForWork();
            }
        }

        private void FileSystemWatcherOnError(object sender, ErrorEventArgs e)
        {
            _logger.Log($"File system watcher error: {e.GetException()}", LogLevel.Error);
            lock (_hasWorkConditionVariable)
            {
                _needToScan = true;
                _pathScanner.Clear();
                _gitIsBusy = false;
                _changes.Clear();
            }
            _hasWorkConditionVariable.Notify();
        }

        private void Stop()
        {
            lock (_hasWorkConditionVariable)
            {
                _needToQuit = true;
            }
            _agentStarter.Stop();
            _pathScanner.Stop();
            _hasWorkConditionVariable.Notify();
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
