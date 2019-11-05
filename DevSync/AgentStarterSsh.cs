using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
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

        private string _host, _username, _keyFilePath;
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

        public string KeyFilePath
        {
            get => _keyFilePath;
            set
            {
                _keyFilePath = value;
                _privateKeyFile = null;
                IsStarted = false;
            }
        }

        public bool DeployAgent { get; set; }

        private PrivateKeyFile _privateKeyFile;

        protected override void Cleanup()
        {
            base.Cleanup();
            IsStarted = false;
            SshClient sshClient;
            SshCommand sshCommand;
            lock (this)
            {
                sshClient = _sshClient;
                sshCommand = _sshCommand;
            }
            sshCommand?.Dispose();
            sshClient?.Dispose();
        }

        private void CleanupDeferred()
        {
            base.Cleanup();
            IsStarted = false;
            SshClient sshClient;
            SshCommand sshCommand;
            lock (this)
            {
                sshClient = _sshClient;
                sshCommand = _sshCommand;
            }
            Task.Run(() =>
            {
                sshCommand?.Dispose();
                sshClient?.Dispose();
            });
        }
        protected void DoDeployAgent(SshClient sshClient, string path)
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
                using var sshCommand = sshClient.CreateCommand($"mkdir -p {path} && tar xf - -C {path}");
                var asyncResult = sshCommand.BeginExecute();
                using (sshCommand.InputStream)
                {
                    sshCommand.InputStream.Write(tarBytes);
                }

                sshCommand.EndExecute(asyncResult);
                if (sshCommand.ExitStatus != 0)
                {
                    throw new SyncException(
                        $"Deploy failed {path} ({sshCommand.ExitStatus}, {sshCommand.Error.Trim()})");
                }
            }

            Logger.Log($"Deployed agent in {sw.ElapsedMilliseconds} ms");
        }

        private string GetKeyPassPhrase()
        {
            Logger.Pause();
            Console.Write("Enter key passphrase: ");
            var keyPassPhrase = "";
            do
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter)
                {
                    break;
                }

                if (key.Key != ConsoleKey.Backspace)
                {
                    keyPassPhrase += key.KeyChar;
                }
                else if (key.Key == ConsoleKey.Backspace && keyPassPhrase.Length > 0)
                {
                    keyPassPhrase = keyPassPhrase.Substring(0, keyPassPhrase.Length - 1);
                }
            } while (true);
            Console.WriteLine();
            Logger.Resume();
            return keyPassPhrase;
        }

        private PrivateKeyFile GetPrivateKeyFile()
        {
            if (_privateKeyFile == null)
            {

                if (!File.Exists(_keyFilePath))
                {
                    throw new SyncException($"Your ssh private key is not found: {_keyFilePath}. Use ssh-keygen to create");
                }
                try
                {
                    _privateKeyFile = new PrivateKeyFile(_keyFilePath);
                }
                catch (SshPassPhraseNullOrEmptyException)
                {
                    _privateKeyFile = new PrivateKeyFile(_keyFilePath, GetKeyPassPhrase());
                }
            }
            return _privateKeyFile;
        }

        public override void DoStart()
        {
            try
            {
                Cleanup();
                var connectionInfo = new ConnectionInfo(_host, _username,
                    new PrivateKeyAuthenticationMethod(_username, GetPrivateKeyFile())
                )
                {
                    RetryAttempts = int.MaxValue,
                    Timeout = new TimeSpan(0, 0, 20),
                };

                var sshClient = new SshClient(connectionInfo);
                sshClient.Connect();
                sshClient.ErrorOccurred += (sender, args) =>
                {
                    Logger.Log(args.Exception.Message, LogLevel.Error);
                    // use cleanup on other thread to prevent race condition
                    CleanupDeferred();
                };

                // TODO: path
                var deployPath = ".devsync";
                if (DeployAgent)
                {
                    DoDeployAgent(sshClient, deployPath);
                }

                /*
                 * COMPlus_EnableDiagnostics turns off clr-debug-pipe
                 * https://github.com/dotnet/coreclr/blob/master/Documentation/building/debugging-instructions.md
                 */
                var sshCommand =
                    sshClient.CreateCommand($"COMPlus_EnableDiagnostics=0 dotnet {deployPath}/DevSyncAgent.dll");
                sshCommand.BeginExecute(ar =>
                {
                    Logger.Log($"Agent died with exit code {sshCommand.ExitStatus}", LogLevel.Error);
                    // use cleanup on other thread to prevent race condition
                    CleanupDeferred();
                });

                PacketStream = new PacketStream(sshCommand.OutputStream, sshCommand.InputStream, Logger);
                lock (this)
                {
                    _sshClient = sshClient;
                    _sshCommand = sshCommand;
                }
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
