using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DevSync.SshStarter;
using DevSyncLib;
using DevSyncLib.Command;
using DevSyncLib.Logger;
using ICSharpCode.SharpZipLib.Tar;

namespace DevSync
{
    public class AgentStarterSsh : AgentStarter
    {
        private ISshStarter _sshStarter;
        private ISshStarterCommand _sshStarterCommand;

        private string _host, _username, _keyFilePath;

        private AuthenticationMethodMode _authenticationMethodMode;

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
                IsStarted = false;
            }
        }

        public bool DeployAgent { get; set; }

        public bool AuthorizeKey { get; set; }

        public bool ExternalSsh { get; set; }

        /*
         * Occurs when shell cannot find dotnet command
         *
         * Command not found:
         * http://www.tldp.org/LDP/abs/html/exitcodes.html
         */
        protected const byte CommandNotFoundCode = 127;

        /*
         * Occurs when DevSync agent is not found and .NET Core SDK is installed.
         */
        protected const byte NotFoundDotnet = 1;

        /*
         * Occurs when DevSync agent is not found and .NET Core SDK is not installed.
         *
         * LibHostSdkFindFailure:
         * https://github.com/dotnet/core-setup/blob/master/Documentation/design-docs/host-error-codes.md
         */
        protected const byte LibHostSdkFindFailure = 0x91;

        /*
         * Occurs when DevSync agent dependencies is not found.
         * ResolverResolveFailure:
         * https://github.com/dotnet/core-setup/blob/master/Documentation/design-docs/host-error-codes.md
         */
        protected const byte ResolverResolveFailure = 0x8c;

        protected override void Cleanup()
        {
            base.Cleanup();
            IsStarted = false;
            ISshStarter sshClient;
            ISshStarterCommand sshCommand;
            lock (this)
            {
                sshClient = _sshStarter;
                sshCommand = _sshStarterCommand;
            }

            sshCommand?.Dispose();
            sshClient?.Dispose();
        }

        private void CleanupDeferred()
        {
            base.Cleanup();
            IsStarted = false;
            ISshStarter sshClient;
            ISshStarterCommand sshCommand;
            lock (this)
            {
                sshClient = _sshStarter;
                sshCommand = _sshStarterCommand;
            }

            Task.Run(() =>
            {
                sshCommand?.Dispose();
                sshClient?.Dispose();
            });
        }

        protected byte[] CreateTarForAgent()
        {
            var files = new[]
            {
                "DevSyncAgent.dll",
                "DevSyncAgent.deps.json",
                "DevSyncAgent.runtimeconfig.json",
                "DevSyncLib.dll",
                "DevSyncLib.deps.json"
            };

            using var memoryStream = new MemoryStream();
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

            return memoryStream.ToArray();
        }

        protected void DoDeployAgent(ISshStarter sshStarter, string path)
        {
            var sw = Stopwatch.StartNew();
            var tarBytes = CreateTarForAgent();
            using var sshCommand = sshStarter.RunCommand($"mkdir -p {path} && tar xf - -C {path}");
            try
            {
                using (sshCommand.InputStream)
                {
                    sshCommand.InputStream.Write(tarBytes, 0, tarBytes.Length);
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (EndOfStreamException)
            {
            }
            catch (IOException)
            {
            }
            sshCommand.Wait();
            if (sshCommand.ExitCode != 0)
            {
                throw new SyncException(
                    $"Agent deploy failed {path} ({sshCommand.ExitCode}, {sshCommand.Error.Trim()})");
            }

            Logger.Log($"Agent deployed in {sw.ElapsedMilliseconds} ms");
        }

        protected void DoAuthorizeKey(ISshStarter sshStarter)
        {
            var publicKeyPath = _keyFilePath + ".pub";
            if (!File.Exists(publicKeyPath) || !File.Exists(_keyFilePath))
            {
                Logger.Log("Generating public/private rsa key pair.");
                var sshKeyGenerator = new SshRsaKeyGenerator();
                using (var fs = new FileStream(_keyFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    fs.Write(Encoding.ASCII.GetBytes(sshKeyGenerator.ToPrivateKey()));
                    // 0600, rw-------
                    fs.ChangeMode(0b110_000_000);
                }
                Logger.Log($"Your identification has been saved in {_keyFilePath}");
                File.WriteAllText(publicKeyPath, sshKeyGenerator.ToRfcPublicKey($"{Environment.UserName}@{Environment.MachineName}") + Environment.NewLine);
                Logger.Log($"Your public key has been saved in {publicKeyPath}");
            }
            var publicKey = File.ReadAllText(publicKeyPath).Trim();
            var sw = Stopwatch.StartNew();
            using var sshCommand = sshStarter.RunCommand($"mkdir -p .ssh && chmod 700 .ssh && echo {publicKey} >> .ssh/authorized_keys && chmod 600 .ssh/authorized_keys");
            sshCommand.Wait();
            if (sshCommand.ExitCode != 0)
            {
                throw new SyncException(
                    $"Failed to add public key to authorized keys ({sshCommand.ExitCode}, {sshCommand.Error.Trim()})");
            }

            Logger.Log($"Public key added to authorized keys in {sw.ElapsedMilliseconds} ms");
        }

        private ISshStarter CreateSshStarter()
        {
            ISshStarter sshStarter;
            if (ExternalSsh)
            {
                sshStarter = new SshStarter.External.SshStarter();
            }
            else
            {
                sshStarter = new SshStarter.Internal.SshStarter(Logger);
            }
            sshStarter.Host = _host;
            sshStarter.KeyFilePath = _keyFilePath;
            sshStarter.Username = _username;
            sshStarter.AuthenticationMode = _authenticationMethodMode;
            return sshStarter;
        }

        public override void DoStart()
        {
            restart:
            _authenticationMethodMode = AuthenticationMethodMode.Key;

            retry:
            try
            {
                Cleanup();
                if (_authenticationMethodMode == AuthenticationMethodMode.Key && !File.Exists(KeyFilePath))
                {
                    if (!AuthorizeKey)
                    {
                        throw new SyncException($"Your ssh private key is not found: {KeyFilePath}. Use ssh-keygen to generate key pair or use --authorize-key option");
                    }
                    _authenticationMethodMode = AuthenticationMethodMode.Password;
                }
                var sshStarter = CreateSshStarter();
                sshStarter.OnConnectError += (sender, args) =>
                {
                    Logger.Log(args.Error, LogLevel.Error);
                    // use cleanup on other thread to prevent race condition
                    CleanupDeferred();
                };
                sshStarter.Connect();

                if (_authenticationMethodMode == AuthenticationMethodMode.Password && AuthorizeKey)
                {
                    DoAuthorizeKey(sshStarter);
                    AuthorizeKey = false;
                    goto restart;
                }

                if (DeployAgent)
                {
                    DoDeployAgent(sshStarter, DeployPath);
                }

                /*
                 * COMPlus_EnableDiagnostics turns off clr-debug-pipe
                 * https://github.com/dotnet/coreclr/blob/master/Documentation/building/debugging-instructions.md
                 */
                var sshCommand =
                    sshStarter.RunCommand($"COMPlus_EnableDiagnostics=0 dotnet {DeployPath}/DevSyncAgent.dll");
                sshCommand.OnExit += (sender, args) =>
                {
                    SetAgentExitCode(sshCommand.ExitCode, sshCommand.Error);
                    // use cleanup on other thread to prevent race condition
                    CleanupDeferred();
                };
                PacketStream = new PacketStream(sshCommand.OutputStream, sshCommand.InputStream, Logger);
                lock (this)
                {
                    _sshStarter = sshStarter;
                    _sshStarterCommand = sshCommand;
                }
            }
            catch (SshStarterAuthenticationException ex)
            {
                if (!AuthorizeKey)
                {
                    throw new SyncException(ex.Message);
                }
                Logger.Log(ex.Message, LogLevel.Error);
                // fallback to password
                _authenticationMethodMode = AuthenticationMethodMode.Password;
                goto retry;
            }
        }

        protected override void ProcessAgentExitCode()
        {
            // exit code is byte on some platforms
            var byteAgentExitCode = (byte)AgentExitCode;
            if (byteAgentExitCode == CommandNotFoundCode)
            {
                throw new SyncException(".NET Core 3.0 runtime is not installed on destination");
            }

            if (byteAgentExitCode == LibHostSdkFindFailure || byteAgentExitCode == ResolverResolveFailure || byteAgentExitCode == NotFoundDotnet)
            {
                if (DeployAgent)
                {
                    throw new SyncException("DevSync agent is not installed on destination");
                }

                DeployAgent = true;
                throw new SyncException("DevSync agent is not installed on destination. Will try to deploy agent", true, false);
            }
        }

        public AgentStarterSsh(ILogger logger) : base(logger)
        {
        }
    }
}
