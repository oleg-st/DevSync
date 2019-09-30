using System;
using System.Diagnostics;
using System.IO;
using DevSyncLib;
using DevSyncLib.Command;
using DevSyncLib.Logger;
using ICSharpCode.SharpZipLib.Tar;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace DevSync
{
    public class AgentStarterSsh : AgentStarter
    {
        private SshClient _sshClient;
        private SshCommand _sshCommand;

        private string _host, _username;
        public string Host
        {
            get => _host;
            set
            {
                _host = value;
                IsStarted = false;
            }
        }

        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                IsStarted = false;
            }
        }

        public bool DeployAgent { get; set; }

        protected override void Cleanup()
        {
            base.Cleanup();

            _sshCommand?.Dispose();
            _sshClient?.Dispose();
        }

        protected void DoDeployAgent(string path)
        {
            var sw = Stopwatch.StartNew();
            var files = new[]
            {
                "DevSyncAgent.dll",
                "DevSyncAgent.deps.json",
                "DevSyncAgent.runtimeconfig.json",
                "DevSyncLib.dll",
                "DevSyncLib.deps.json"
            };

            using (var memoryStream = new MemoryStream())
            {
                using (var tarArchive = TarArchive.CreateOutputTarArchive(memoryStream))
                {
                    var assemblyPath = Path.GetDirectoryName(typeof(PacketStream).Assembly.Location);

                    foreach (var filename in files)
                    {
                        var tarEntry = TarEntry.CreateEntryFromFile(Path.Combine(assemblyPath, filename));
                        tarEntry.Name = Path.GetFileName(filename);
                        tarArchive.WriteEntry(tarEntry, true);
                    }
                }

                var tarBytes = memoryStream.ToArray();
                using var sshCommand = _sshClient.CreateCommand($"mkdir -p {path} && tar xf - -C {path}");
                var asyncResult = sshCommand.BeginExecute();
                using (sshCommand.InputStream)
                {
                    sshCommand.InputStream.Write(tarBytes);
                }

                sshCommand.EndExecute(asyncResult);
                if (sshCommand.ExitStatus != 0)
                {
                    throw new SyncException($"Deploy failed {path} ({sshCommand.ExitStatus})");
                }
            }

            Logger.Log($"Deployed agent in {sw.ElapsedMilliseconds} ms");
        }

        public override void DoStart()
        {
            try
            {
                Cleanup();

                // TODO: use default user ssh key
                var privateKeyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".ssh/id_rsa");
                if (!File.Exists(privateKeyPath))
                {
                    throw new SyncException($"Your ssh private key is not found: {privateKeyPath}. Use ssh-keygen");
                }

                var connectionInfo = new ConnectionInfo(_host, _username,
                    new PrivateKeyAuthenticationMethod(_username, new PrivateKeyFile(privateKeyPath))
                )
                {
                    RetryAttempts = int.MaxValue,
                    Timeout = new TimeSpan(0, 0, 20),
                };

                _sshClient = new SshClient(connectionInfo);
                _sshClient.Connect();
                _sshClient.ErrorOccurred += (sender, args) =>
                {
                    Logger.Log(args.Exception.Message, LogLevel.Error);
                    IsStarted = false;
                };

                // TODO: path
                var deployPath = ".devsync";
                if (DeployAgent)
                {
                    DoDeployAgent(deployPath);
                }

                /*
                 * COMPlus_EnableDiagnostics turns off clr-debug-pipe
                 * https://github.com/dotnet/coreclr/blob/master/Documentation/building/debugging-instructions.md
                 */
                _sshCommand =
                    _sshClient.CreateCommand($"COMPlus_EnableDiagnostics=0 dotnet {deployPath}/DevSyncAgent.dll");
                _sshCommand.BeginExecute(ar =>
                {
                    Logger.Log($"Agent died with exit code {_sshCommand.ExitStatus}", LogLevel.Error);
                    _sshCommand.Dispose();
                    IsStarted = false;
                });

                PacketStream = new PacketStream(_sshCommand.OutputStream, _sshCommand.InputStream, Logger);
            }
            catch (SshAuthenticationException ex)
            {
                throw new SyncException(ex.Message);
            }
        }

        public AgentStarterSsh(ILogger logger) : base(logger)
        {
        }
    }
}
