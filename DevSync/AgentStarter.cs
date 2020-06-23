using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using DevSyncLib;
using DevSyncLib.Command;
using DevSyncLib.Logger;

namespace DevSync
{
    public abstract class AgentStarter : IDisposable
    {
        protected bool IsInitialized;
        protected PacketStream PacketStream;

        // Initialize options
        protected string DestPathValue;
        protected List<string> ExcludeListValue;

        // TODO: hardcoded path
        protected const string DeployPath = ".devsync";

        public bool IsStarted { get; protected set; }

        protected ILogger Logger { get; set; }

        protected int AgentExitCode { get; set; }

        protected readonly CancellationTokenSource CancellationTokenSource;

        protected AgentStarter(ILogger logger)
        {
            Logger = logger;
            CancellationTokenSource = new CancellationTokenSource();
        }

        public string DestPath
        {
            get => DestPathValue;
            set
            {
                DestPathValue = value;
                IsInitialized = false;
            }
        }

        public List<string> ExcludeList
        {
            get => ExcludeListValue;
            set
            {
                ExcludeListValue = value;
                IsInitialized = false;
            }
        }

        public void Start()
        {
            if (IsStarted)
            {
                return;
            }
            CancellationTokenSource.Token.ThrowIfCancellationRequested();

            try
            {
                var sw = Stopwatch.StartNew();
                SetAgentExitCode(0, null);
                DoStart();
                IsStarted = true;
                IsInitialized = false;
                Logger.Log($"Agent started in {sw.ElapsedMilliseconds} ms");
            }
            catch (Exception)
            {
                Cleanup();
                throw;
            }
        }

        protected void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }
            CancellationTokenSource.Token.ThrowIfCancellationRequested();

            var sw = Stopwatch.StartNew();
            var response = SendCommandInternal<InitResponse>(new InitRequest(Logger)
            {
                AgentOptions = new AgentOptions
                {
                    DestPath = DestPath,
                    ExcludeList = ExcludeList
                }
            });
            if (!response.Ok)
            {
                throw new SyncException("Agent initialize failed");
            }
            IsInitialized = true;
            Logger.Log($"Agent initialized in {sw.ElapsedMilliseconds} ms");
        }

        public abstract void DoStart();

        protected virtual void Cleanup()
        {
        }

        protected T SendCommandInternal<T>(Packet packet) where T : class
        {
            CancellationTokenSource.Token.ThrowIfCancellationRequested();
            try
            {
                return PacketStream.SendCommand<T>(packet);
            }
            catch (Exception)
            {
                Cleanup();
                IsStarted = false;
                if (AgentExitCode != 0)
                {
                    ProcessAgentExitCode();
                }
                throw;
            }
        }

        protected virtual void ProcessAgentExitCode()
        {
        }

        public T SendCommand<T>(Packet packet) where T : class
        {
            Start();
            Initialize();
            return SendCommandInternal<T>(packet);
        }

        public void Dispose()
        {
            Cleanup();
        }

        public static AgentStarter Create(SyncOptions syncOptions, ILogger logger)
        {
            AgentStarter agentStarter;
            
            if (string.IsNullOrEmpty(syncOptions.Host))
            {
                agentStarter = new AgentStarterLocal(logger);
            }
            else
            {
                agentStarter = new AgentStarterSsh(logger)
                {
                    Host = syncOptions.Host,
                    Port =  syncOptions.Port,
                    Username = syncOptions.UserName,
                    DeployAgent = syncOptions.DeployAgent,
                    KeyFilePath = syncOptions.KeyFilePath,
                    AuthorizeKey = syncOptions.AuthorizeKey,
                    ExternalSsh = syncOptions.ExternalSsh
                };
            }

            agentStarter.DestPath = syncOptions.DestinationPath;
            agentStarter.ExcludeList = syncOptions.ExcludeList;
            return agentStarter;
        }

        protected void SetAgentExitCode(int exitCode, string errorMessage)
        {
            AgentExitCode = exitCode;
            if ((exitCode == 0 && string.IsNullOrWhiteSpace(errorMessage)) ||
                CancellationTokenSource.IsCancellationRequested
            )
            {
                return;
            }
            Logger.Log($"Agent died with exit code {exitCode}{(!string.IsNullOrWhiteSpace(errorMessage) ? $": {errorMessage.Trim()}" : "")}", LogLevel.Error);
        }

        public void Stop()
        {
            CancellationTokenSource.Cancel();
            Cleanup();
        }

        public static string GetAssemblyDirectoryName()
        {
            return Path.GetDirectoryName(Assembly.GetCallingAssembly().Location) ?? "";
        }
    }
}
