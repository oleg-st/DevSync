using System;
using System.Collections.Generic;
using System.IO;

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
        public List<FsChangeResult> ReadAndApplyChanges()
        {
            var list = new List<FsChangeResult>();
            while (true)
            {
                var fsChange = Reader.ReadFsChange();
                if (fsChange.IsEndMarker)
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
                        if (FsHelper.TryDeleteDirectory(path, out var exception) &&
                            FsHelper.TryDeleteFile(path, out exception))
                        {
                            resultCode = FsChangeResultCode.Ok;
                        }
                        else
                        {
                            resultCode = FsChangeResultCode.Error;
                            error = exception.Message;
                        }

                        break;
                    case FsChangeType.Rename:
                        try
                        {
                            File.Move(Path.Combine(BasePath, fsChange.OldFsEntry.Path), path, true);
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

                list.Add(new FsChangeResult
                {
                    ChangeType = fsChange.ChangeType,
                    Path = fsChange.FsEntry.Path,
                    ResultCode = resultCode,
                    Error = error
                });
            }

            return list;
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
            writer.WriteFsChange(FsChange.EndMarkerChange);
        }
    }
}
