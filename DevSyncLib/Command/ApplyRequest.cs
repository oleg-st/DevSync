using DevSyncLib.Logger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DevSyncLib.Command
{
    public class ApplyRequest : Packet
    {
        public override short Signature => 5;

        private List<FsSenderChange> _changes;

        // max items body size in chunk (soft limit)
        public const int ChangesMaxSize = 100 * 1024 * 1024;
        // max items in chunk
        public const int ChangesMaxCount = 2500;

        public List<FsSenderChange> SentChanges;
        public long SentChangesSize;

        protected Reader Reader;

        public string BasePath;

        public override void Read(Reader reader)
        {
            Reader = reader;
        }

        public void SetChanges(IEnumerable<FsSenderChange> changes)
        {
            if (_changes == null)
            {
                _changes = new List<FsSenderChange>(ChangesMaxCount);
            }
            else
            {
                _changes.Clear();
            }
            _changes.AddRange(changes.Take(ChangesMaxCount));
        }

        public bool HasChanges => _changes.Count > 0;

        private FsChangeResult ApplyFsChange(FsChange fsChange, FileMaskList excludeList)
        {
            var path = Path.Combine(BasePath, fsChange.Path);
            FsChangeResultCode resultCode;
            string error = null;
            switch (fsChange.ChangeType)
            {
                case FsChangeType.Change when fsChange.IsDirectory:
                    try
                    {
                        Directory.CreateDirectory(path);
                        Directory.SetLastWriteTime(path, fsChange.LastWriteTime);
                        resultCode = FsChangeResultCode.Ok;
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                        resultCode = FsChangeResultCode.Error;
                    }

                    break;
                case FsChangeType.Change:
                    try
                    {
                        if (fsChange.HasBody && Reader.ReadFsChangeBody(path, fsChange))
                        {
                            resultCode = FsChangeResultCode.Ok;
                        }
                        else
                        {
                            // error occurred in sender
                            error = "Sender error";
                            resultCode = FsChangeResultCode.SenderError;
                        }
                    }
                    catch (EndOfStreamException)
                    {
                        // end of data
                        throw;
                    }
                    catch (Exception ex)
                    {
                        resultCode = FsChangeResultCode.Error;
                        error = ex.Message;
                    }

                    break;
                case FsChangeType.Remove:
                    // directory
                    if (Directory.Exists(path))
                    {
                        var scanDirectory = new ScanDirectory(Logger, excludeList, false);
                        Exception exception = null;
                        foreach (var fsEntry in scanDirectory.ScanPath(BasePath, fsChange.Path))
                        {
                            var fsEntryPath = Path.Combine(BasePath, fsEntry.Path);
                            try
                            {
                                if (fsEntry.IsDirectory)
                                {
                                    Directory.Delete(fsEntryPath, false);
                                }
                                else
                                {
                                    File.Delete(fsEntryPath);
                                }
                            }
                            catch (Exception ex)
                            {
                                exception ??= ex;
                                Logger.Log($"Error deleting {fsEntryPath}: {ex.Message}", LogLevel.Warning);
                            }
                        }

                        try
                        {
                            Directory.Delete(path, false);
                        }
                        catch (Exception ex)
                        {
                            exception ??= ex;
                            Logger.Log($"Error deleting {path}: {ex.Message}", LogLevel.Warning);
                        }

                        if (exception == null)
                        {
                            resultCode = FsChangeResultCode.Ok;
                        }
                        else
                        {
                            // scan directory see any file -> error (handle excludes)
                            if (scanDirectory.ScanPath(BasePath, fsChange.Path).Any(x => !x.IsDirectory))
                            {
                                resultCode = FsChangeResultCode.Error;
                                error = exception.Message;
                            }
                            else
                            {
                                resultCode = FsChangeResultCode.Ok;
                            }
                        }
                    }
                    else if (File.Exists(path))
                    {
                        try
                        {
                            File.Delete(path);
                            resultCode = FsChangeResultCode.Ok;

                        }
                        catch (Exception ex)
                        {
                            resultCode = FsChangeResultCode.Error;
                            error = ex.Message;

                        }
                    }
                    else
                    {
                        resultCode = FsChangeResultCode.Ok;
                    }
                    break;
                case FsChangeType.Rename:
                    try
                    {
                        var oldPath = Path.Combine(BasePath, fsChange.OldPath);
                        if (Directory.Exists(oldPath))
                        {
                            Directory.Move(oldPath, path);
                        }
                        else
                        {
                            File.Move(oldPath, path, true);
                        }

                        resultCode = FsChangeResultCode.Ok;
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                        resultCode = FsChangeResultCode.Error;
                    }
                    break;
                default:
                    resultCode = FsChangeResultCode.Error;
                    error = "Unknown change type";
                    break;
            }

            return new FsChangeResult
            {
                ChangeType = fsChange.ChangeType,
                Path = fsChange.Path,
                ResultCode = resultCode,
                ErrorMessage = error
            };
        }

        // read and apply changes
        public IEnumerable<FsChangeResult> ReadAndApplyChanges(FileMaskList excludeList)
        {
            while (true)
            {
                var fsChange = Reader.ReadFsChange();
                if (fsChange.IsEmpty)
                {
                    break;
                }

                var fsChangeResult = ApplyFsChange(fsChange, excludeList);
                if (fsChangeResult.ResultCode != FsChangeResultCode.Ok && fsChange.ChangeType == FsChangeType.Change)
                {
                    var path = Path.Combine(BasePath, fsChange.Path);
                    if (fsChange.IsDirectory && File.Exists(path)
                        || !fsChange.IsDirectory && Directory.Exists(path))
                    {
                        // delete and retry
                        var fsRemoveChangeResult = ApplyFsChange(
                            new FsChange(FsChangeType.Remove, fsChange.Path),
                                excludeList);
                        if (fsRemoveChangeResult.ResultCode == FsChangeResultCode.Ok)
                        {
                            fsChangeResult = ApplyFsChange(fsChange, excludeList);
                        }
                    }
                }

                // TODO: skip ok codes (sender do not use them at the moment)
                if (fsChangeResult.ResultCode != FsChangeResultCode.Ok)
                {
                    yield return fsChangeResult;
                }
            }
        }

        public void ClearChanges()
        {
            _changes.Clear();
            SentChanges.Clear();
            SentChangesSize = 0;
        }

        public override void Write(Writer writer)
        {
            if (SentChanges == null)
            {
                SentChanges = new List<FsSenderChange>(ChangesMaxCount);
            }
            else
            {
                SentChanges.Clear();
            }
            SentChangesSize = 0;
            foreach (var fsChange in _changes)
            {
                if (SentChangesSize >= ChangesMaxSize)
                {
                    break;
                }

                if (!fsChange.Expired)
                {
                    if (fsChange.NeedToResolve)
                    {
                        fsChange.Resolve(BasePath);
                    }
                    if (fsChange.HasBody)
                    {
                        var path = Path.Combine(BasePath, fsChange.Path);
                        writer.WriteFsChangeBody(path, fsChange);
                    }
                    else
                    {
                        writer.WriteFsChange(fsChange);
                    }
                }
                SentChanges.Add(fsChange);
                SentChangesSize += fsChange.BodySize;
            }
            writer.WriteFsChange(FsChange.Empty);
        }

        public ApplyRequest(ILogger logger) : base(logger)
        {
        }
    }
}
