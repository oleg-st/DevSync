using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DevSyncLib.Logger;

namespace DevSyncLib.Command
{
    public class ApplyRequest: Packet
    {
        public override short Signature => 5;

        public List<FsChange> Changes;

        protected Reader Reader;

        public string BasePath;

        public override void Read(Reader reader)
        {
            Reader = reader;
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

                var path = Path.Combine(BasePath, fsChange.FsEntry.Path);
                FsChangeResultCode resultCode;
                string error = null;
                switch (fsChange.ChangeType)
                {
                    case FsChangeType.Change when fsChange.FsEntry.IsDirectory:
                        try
                        {
                            Directory.CreateDirectory(path);
                            Directory.SetLastWriteTime(path, fsChange.FsEntry.LastWriteTime);
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
                            bool bodyReadSuccess;
                            var directoryName = Path.GetDirectoryName(path);
                            if (fsChange.HasBody)
                            {
                                bodyReadSuccess = Reader.ReadFsChangeBody(path, fsChange);
                            }
                            else
                            {
                                Directory.CreateDirectory(directoryName);
                                // create empty file
                                using (new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                                {
                                }

                                bodyReadSuccess = true;
                                File.SetLastWriteTime(path, fsChange.FsEntry.LastWriteTime);
                            }

                            if (bodyReadSuccess)
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
                            var scanDirectory = new ScanDirectory(_logger, excludeList, false);
                            Exception exception = null;
                            foreach (var fsEntry in scanDirectory.ScanPath(BasePath, fsChange.FsEntry.Path))
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
                                    if (exception == null)
                                    {
                                        exception = ex;
                                    }
                                    _logger.Log($"Delete error {fsEntryPath} {ex}", LogLevel.Warning);
                                }
                            }

                            try
                            {
                                Directory.Delete(path, false);
                            }
                            catch (Exception ex)
                            {
                                if (exception == null)
                                {
                                    exception = ex;
                                }
                                _logger.Log($"Delete error {path} {ex}", LogLevel.Warning);
                            }

                            if (exception == null)
                            {
                                resultCode = FsChangeResultCode.Ok;
                            }
                            else
                            {
                                // scan directory see any file -> error (handle excludes)
                                if (scanDirectory.ScanPath(BasePath, fsChange.FsEntry.Path).Any(x => !x.IsDirectory))
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
                            if (fsChange.FsEntry.IsDirectory)
                            {
                                Directory.Move(Path.Combine(BasePath, fsChange.OldFsEntry.Path), path);
                            }
                            else
                            {
                                File.Move(Path.Combine(BasePath, fsChange.OldFsEntry.Path), path, true);
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

                // TODO: skip ok codes (sender do not use them for now)
                if (resultCode != FsChangeResultCode.Ok)
                {
                    yield return new FsChangeResult
                    {
                        ChangeType = fsChange.ChangeType,
                        Path = fsChange.FsEntry.Path,
                        ResultCode = resultCode,
                        ErrorMessage = error
                    };
                }
            }
        }

        public override void Write(Writer writer)
        {
            foreach (var fsChange in Changes)
            {
                if (!fsChange.Expired)
                {
                    writer.WriteFsChange(fsChange);
                    if (fsChange.HasBody)
                    {
                        var path = Path.Combine(BasePath, fsChange.FsEntry.Path);
                        writer.WriteFsChangeBody(path, fsChange);
                    }
                }
            }
            writer.WriteFsChange(FsChange.Empty);
        }

        public ApplyRequest(ILogger logger) : base(logger)
        {
        }
    }
}
