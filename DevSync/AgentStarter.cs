using System;
using System.Collections.Generic;
using System.Diagnostics;
using DevSyncLib;
using DevSyncLib.Command;

namespace DevSync
{
    public abstract class AgentStarter : IDisposable
    {
        protected bool IsInitialized;
        protected PacketStream PacketStream;

        // Initialize options
        protected string DestPathValue;
        protected List<string> ExcludeListValue;

        public bool IsStarted { get; protected set; }

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
            try
            {
                var sw = Stopwatch.StartNew();
                DoStart();
                IsStarted = true;
                IsInitialized = false;
                Console.WriteLine($"Started in {sw.ElapsedMilliseconds} ms");
            }
            catch (Exception)
            {
                Cleanup();
                throw;
            }
        }

        protected void Initialize()
        {
            var sw = Stopwatch.StartNew();
            var response = SendCommandInternal<InitResponse>(new InitRequest
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
            Console.WriteLine($"Initialized in {sw.ElapsedMilliseconds} ms");
        }

        public abstract void DoStart();

        protected virtual void Cleanup()
        {
        }

        protected T SendCommandInternal<T>(Packet packet) where T : class
        {
            try
            {
                return PacketStream.SendCommand<T>(packet);
            }
            catch (Exception)
            {
                Cleanup();
                IsStarted = false;
                throw;
            }
        }

        public T SendCommand<T>(Packet packet) where T : class
        {
            if (!IsStarted)
            {
                Start();
            }

            if (!IsInitialized)
            {
                Initialize();
            }

            return SendCommandInternal<T>(packet);
        }

        public void Dispose()
        {
            Cleanup();
        }

        public static AgentStarter Create(SyncPath syncPath, List<string> excludeList, bool deployAgent)
        {
            AgentStarter agentStarter;
            
            if (string.IsNullOrEmpty(syncPath.Host))
            {
                agentStarter = new AgentStarterLocal();
            }
            else
            {
                agentStarter = new AgentStarterSsh
                {
                    Host = syncPath.Host,
                    Username = syncPath.UserName,
                    DeployAgent = deployAgent
                };
            }

            agentStarter.DestPath = syncPath.Path;
            agentStarter.ExcludeList = excludeList;
            return agentStarter;
        }
    }
}
