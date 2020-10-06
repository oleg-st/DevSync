using DevSyncLib;
using DevSyncLib.Command;
using DevSyncLib.Logger;
using System;
using System.IO;
using System.Linq;

namespace DevSyncAgent
{
    public class CommandRunner
    {
        private readonly ILogger _logger;
        private string _path;
        private bool _initialized;
        private readonly FileMaskList _excludeList;

        public CommandRunner(ILogger logger)
        {
            _logger = logger;
            _excludeList = new FileMaskList();
        }

        public Packet Run(Packet request)
        {
            try
            {
                return request switch
                {
                    ScanRequest _ => RunScan(),
                    InitRequest initRequest => RunInit(initRequest),
                    ApplyRequest applyRequest => RunApply(applyRequest),
                    _ => throw new SyncException($"Unknown command {request.GetType()}"),
                };
            }
            catch (EndOfStreamException)
            {
                // end of data
                throw;
            }
            catch (SyncException ex)
            {
                return new ErrorResponse(_logger) { Message = ex.Message, Recoverable = ex.Recoverable, NeedToWait = ex.NeedToWait };
            }
            catch (Exception ex)
            {
                return new ErrorResponse(_logger) { Message = ex.Message };
            }
        }

        protected void CheckInitialized()
        {
            if (!_initialized)
            {
                throw new SyncException("Agent is not initialized");
            }
        }

        protected ScanResponse RunScan()
        {
            CheckInitialized();

            var scanResponse = new ScanResponse(_logger);
            var scanDirectory = new ScanDirectory(_logger, _excludeList);
            scanResponse.FileList = scanDirectory.ScanPath(_path);
            return scanResponse;
        }

        protected InitResponse RunInit(InitRequest request)
        {
            _path = request.AgentOptions.DestPath;
            if (!_excludeList.SetList(request.AgentOptions.ExcludeList))
            {
                throw new SyncException($"Invalid exclude list {request.AgentOptions.ExcludeList.Aggregate((x, y) => x + ", " + y) ?? ""}");
            }

            if (string.IsNullOrEmpty(_path) || _path == "/")
            {
                throw new SyncException($"Invalid destination path {_path}");
            }

            if (!Directory.Exists(_path))
            {
                throw new SyncException("Destination path is not exists");
            }

            _initialized = true;
            return new InitResponse(_logger) { Ok = true };
        }

        protected ApplyResponse RunApply(ApplyRequest applyRequest)
        {
            CheckInitialized();
            applyRequest.BasePath = _path;
            var response = new ApplyResponse(_logger) { Result = applyRequest.ReadAndApplyChanges(_excludeList) };
            return response;
        }
    }
}
