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
    public class Sender : IDisposable
    {
        private readonly ILogger _logger;
        private bool _needScan;
        private bool _needQuit;
        private FileSystemWatcher _fileSystemWatcher;

        private readonly string _srcPath;

        private readonly ExcludeList _excludeList;

        private readonly Dictionary<string, FsChange> _changes;
        private readonly AutoResetEvent _autoResetEvent;

        private readonly AgentStarter _agentStarter;

        // max items in chunk
        private const int CHANGES_MAX_COUNT = 10000;
        // max items body size in chunk (soft limit)
        private const int CHANGES_MAX_SIZE = 100 * 1024 * 1024;

        public Sender(SyncOptions syncOptions, ILogger logger)
        {
            _logger = logger;

            if (!Directory.Exists(syncOptions.SourcePath))
            {
                throw new SyncException($"Invalid source path: {syncOptions.SourcePath}");
            }

            _srcPath = syncOptions.SourcePath;
            _excludeList = new ExcludeList(); 
            _excludeList.SetList(syncOptions.ExcludeList);

            _changes = new Dictionary<string, FsChange>();
            _needScan = true;
            _autoResetEvent = new AutoResetEvent(false);
            _agentStarter = AgentStarter.Create(syncOptions, _logger);
            _logger.Log($"Sync {syncOptions}");
        }

        private void Scan()
        {
            var sw = Stopwatch.StartNew();
            Dictionary<string, FsEntry> srcList = null;
            var task = Task.Run(() =>
            {
                var scanDirectory = new ScanDirectory(_logger);
                srcList = scanDirectory.Run(_srcPath, _excludeList);
                _logger.Log($"Scanned local {srcList.Count} items in {sw.ElapsedMilliseconds} ms");
            });
            var response = _agentStarter.SendCommand<ScanResponse>(new ScanRequest());
            _logger.Log($"Scanned remote {response.FileList.Count} items in {sw.ElapsedMilliseconds} ms");
            task.Wait();

            var destList = response.FileList;

            long totalSize = 0;
            int itemsCount = 0;
            foreach (var srcEntry in srcList.Values)
            {
                destList.TryGetValue(srcEntry.Path, out var destEntry);
                // add to changes
                if (!srcEntry.Compare(destEntry))
                {
                    var fsChange = new FsChange {ChangeType = FsChangeType.Change, FsEntry = srcEntry};
                    itemsCount++;
                    totalSize += fsChange.BodySize;
                    AddChange(fsChange);
                }

                // remove from dest list
                if (destEntry != null)
                {
                    destList.Remove(destEntry.Path);
                }
            }

            // delete
            foreach (var destEntry in destList.Values)
            {
                itemsCount++;
                AddChange(new FsChange { ChangeType = FsChangeType.Remove, FsEntry = destEntry });
            }

            _needScan = false;
            lock (_changes)
            {
                UpdateHasWork();
            }

            _logger.Log($"Scanned {itemsCount} items, {PrettySize(totalSize)} to send in {sw.ElapsedMilliseconds} ms");
        }

        private void AddChange(FsChange fsChange)
        {
            lock (_changes)
            {
                if (_changes.TryGetValue(fsChange.Key, out var oldFsChange))
                {
                    oldFsChange.Expired = true;
                }

                _changes[fsChange.Key] = fsChange;
            }
            UpdateHasWork();
        }

        private string GetPath(string fullPath)
        {
            return FsEntry.NormalizePath(Path.GetRelativePath(_srcPath, fullPath));
        }

        private void OnWatcherChanged(object source, FileSystemEventArgs e)
        {
            var path = GetPath(e.FullPath);
            if (_excludeList.IsExcluded(path))
            {
                return;
            }

            var fsEntry = FsEntry.FromFilename(e.FullPath, path);
            AddChange(new FsChange { ChangeType = FsChangeType.Change, FsEntry = fsEntry });
        }

        private void OnWatcherDeleted(object source, FileSystemEventArgs e)
        {
            var path = GetPath(e.FullPath);
            if (_excludeList.IsExcluded(path))
            {
                return;
            }

            var fsEntry = FsEntry.FromFilename(e.FullPath, path);
            AddChange(new FsChange { ChangeType = FsChangeType.Remove, FsEntry = fsEntry });
        }

        private void OnWatcherRenamed(object source, RenamedEventArgs e)
        {
            var path = GetPath(e.FullPath);
            var oldPath = GetPath(e.OldFullPath);

            // is new file excluded?
            if (_excludeList.IsExcluded(path))
            {
                // old file is not excluded -> delete it
                if (!_excludeList.IsExcluded(oldPath))
                {
                    var oldFsEntry = FsEntry.FromFilename(e.OldFullPath, oldPath);
                    AddChange(new FsChange {ChangeType = FsChangeType.Remove, FsEntry = oldFsEntry});
                }

                // both files are excluded -> do nothing
            }
            else // new file is not excluded
            {
                // old file is excluded -> send new file
                if (_excludeList.IsExcluded(oldPath))
                {                    
                    var fsEntry = FsEntry.FromFilename(e.FullPath, path);
                    AddChange(new FsChange { ChangeType = FsChangeType.Change, FsEntry = fsEntry });
                }
                else
                {
                    // both files are excluded -> send rename
                    var oldFsEntry = FsEntry.FromFilename(e.OldFullPath, oldPath);
                    var fsEntry = FsEntry.FromFilename(e.FullPath, path);
                    AddChange(new FsChange { ChangeType = FsChangeType.Rename, FsEntry = fsEntry, OldFsEntry = oldFsEntry });
                }
            }
        }

        private bool HasWork
        {
            get
            {
                lock (_changes)
                {
                    return _needQuit || _needScan || _changes.Count > 0;
                }
            }
        }

        private void UpdateHasWork()
        {
            if (HasWork)
            {
                _autoResetEvent.Set();
            }
            else
            {
                _autoResetEvent.Reset();
            }
        }

        private void WaitForWork()
        {
            if (!HasWork)
            {
                _autoResetEvent.WaitOne(300);
                // ready if we have no work for 300 ms
                if (!HasWork)
                {
                    _logger.Log("Ready");
                }
            }

            while (!HasWork)
            {
                _autoResetEvent.WaitOne();
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
                doubleSize = doubleSize / 1024;
            }
            return $"{doubleSize:0.##} {sizes[order]}";
        }

        private bool SendChanges()
        {
            var sw = Stopwatch.StartNew();

            var applyRequest = new ApplyRequest
            {
                BasePath = _srcPath
            };

            // fetch changes
            long totalSize = 0;
            lock (_changes)
            {
                if (_changes.Count == 0)
                {
                    return true;
                }

                applyRequest.Changes = new List<FsChange>(CHANGES_MAX_COUNT);
                int itemsCount = 0;
                foreach (var fsChange in _changes.Values)
                {
                    if (itemsCount >= CHANGES_MAX_COUNT || totalSize >= CHANGES_MAX_SIZE)
                    {
                        break;
                    }

                    applyRequest.Changes.Add(fsChange);
                    itemsCount++;
                    totalSize += fsChange.BodySize;
                }
            }

            if (applyRequest.Changes.Count == 1)
            {
                _logger.Log(applyRequest.Changes.First().ToString());
            }
            else
            {
                _logger.Log($"Sending {applyRequest.Changes.Count} changes, {PrettySize(totalSize)}");
            }

            var response = _agentStarter.SendCommand<ApplyResponse>(applyRequest);
            var responseResult = response.Result.ToDictionary(x => x.Key, y => y);

            bool hasErrors = false;
            // process sent changes
            lock (_changes)
            {
                foreach (var fsChange in applyRequest.Changes)
                {
                    if (!fsChange.Expired)
                    {
                        _changes.Remove(fsChange.Key);
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
                                    }
                                    else
                                    {
                                        hasErrors = true;
                                        _logger.Log(
                                            $"Change apply error {fsChange.ChangeType} {fsChange.FsEntry.Path}: {fsChangeResult.Error ?? "-"}", LogLevel.Error);
                                    }
                                }
                                // resend
                                _changes.Add(fsChange.Key, fsChange);
                            }
                        }
                    }
                    else
                    {
                        // remove expired
                        if (_changes.TryGetValue(fsChange.Key, out var oldFsChange) && oldFsChange.Expired)
                        {
                            _changes.Remove(fsChange.Key);
                        }
                    }
                }
            }

            UpdateHasWork();
            _logger.Log($"Sent {(applyRequest.Changes.Count == 1 ? "change" : $"{applyRequest.Changes.Count} changes")} in {sw.ElapsedMilliseconds} ms");

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
            _fileSystemWatcher.IncludeSubdirectories = true;
            _fileSystemWatcher.EnableRaisingEvents = true;

            while (!_needQuit)
            {
                bool hasErrors = false;
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
                    _logger.Log(ex.Message, LogLevel.Error);
                    if (!ex.Recoverable)
                    {
                        break;
                    }
                    hasErrors = true;
                }
                catch (Exception ex)
                {
                    _logger.Log(ex.Message, LogLevel.Error);
                    hasErrors = true;
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

        private void ConsoleOnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            _needQuit = true;
            UpdateHasWork();
            e.Cancel = true;
        }

        public void Dispose()
        {
            _agentStarter.Dispose();
            _fileSystemWatcher?.Dispose();
        }
    }
}
