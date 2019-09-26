﻿using System;
using System.Collections.Generic;
using System.IO;

namespace DevSyncLib.Command
{
    public class CommandRunner
    {
        private string _path;
        private bool _initialized;
        private readonly ExcludeList _excludeList;

        public CommandRunner()
        {
            _excludeList = new ExcludeList();
        }

        public Packet Run(Packet request)
        {
            try
            {
                switch (request)
                {
                    case ScanRequest versionCommandRequest:
                        return RunScan(versionCommandRequest);

                    case InitRequest initRequest:
                        return RunInit(initRequest);

                    case ApplyRequest applyRequest:
                        return RunApply(applyRequest);

                    default:
                        throw new SyncException($"Unknown command {request.GetType()}");
                }
            }
            catch (EndOfStreamException)
            {
                // end of data
                throw;
            }
            catch (SyncException ex)
            {
                return new ErrorResponse { Message = ex.Message, Recoverable = ex.Recoverable };
            }
            catch (Exception ex)
            {
                return new ErrorResponse { Message = ex.Message };
            }
        }

        protected void CheckInitialized()
        {
            if (!_initialized)
            {
                throw new SyncException("Agent is not initialized");
            }
        }

        protected ScanResponse RunScan(ScanRequest request)
        {
            CheckInitialized();

            var scanDirectory = new ScanDirectory();
            scanDirectory.Run(_path, _excludeList);

            return new ScanResponse
            {
                FileList = scanDirectory.FileList
            };
        }

        protected InitResponse RunInit(InitRequest request)
        {
            _path = request.AgentOptions.DestPath;
            _excludeList.SetList(request.AgentOptions.ExcludeList);

            if (string.IsNullOrEmpty(_path) || _path == "/")
            {
                throw new SyncException($"Invalid destination path {_path}");
            }

            if (!Directory.Exists(_path))
            {
                throw new SyncException("Destination path is not exists");
            }

            _initialized = true;
            return new InitResponse { Ok = true };
        }

        protected ApplyResponse RunApply(ApplyRequest applyRequest)
        {
            CheckInitialized();
            applyRequest.BasePath = _path;
            var response = new ApplyResponse {Result = applyRequest.ReadAndApplyChanges()};
            return response;
        }
    }
}