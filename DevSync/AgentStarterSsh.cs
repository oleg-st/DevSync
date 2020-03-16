using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using DevSync.Cryptography;
using DevSyncLib;
using DevSyncLib.Command;
using DevSyncLib.Logger;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Security.Cryptography.Ciphers;
using Renci.SshNet.Security.Cryptography.Ciphers.Modes;

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
            var tarBytes = CreateTarForAgent();
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
                    $"Agent deploy failed {path} ({sshCommand.ExitStatus}, {sshCommand.Error.Trim()})");
            }

            Logger.Log($"Agent deployed in {sw.ElapsedMilliseconds} ms");
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
                    throw new SyncException(
                        $"Your ssh private key is not found: {_keyFilePath}. Use ssh-keygen to create");
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

        private void InitEncryption(ConnectionInfo connectionInfo)
        {
            /*
             * Sorted by performance / strength / OpenSSH preferences
             *
             * OpenSSH cipher performance
             * https://possiblelossofprecision.net/?p=2255
             * https://security.stackexchange.com/questions/180544/is-there-a-list-of-weak-ssh-ciphers
             */
            connectionInfo.Encryptions.Clear();
            // aes-ctr (partially native with CtrCipherMode, most secure)
            connectionInfo.Encryptions["aes128-ctr"] =
                new CipherInfo(128, (key, iv) => new NativeAesCipherCtr(key, iv));
            connectionInfo.Encryptions["aes192-ctr"] =
                new CipherInfo(192, (key, iv) => new NativeAesCipherCtr(key, iv));
            connectionInfo.Encryptions["aes256-ctr"] =
                new CipherInfo(256, (key, iv) => new NativeAesCipherCtr(key, iv));
            // aes-cbc (fully native, fastest but less secure)
            connectionInfo.Encryptions["aes128-cbc"]
                = new CipherInfo(128, (key, iv) => new NativeAesCipherCbc(key, iv));
            connectionInfo.Encryptions["aes192-cbc"]
                = new CipherInfo(192, (key, iv) => new NativeAesCipherCbc(key, iv));
            connectionInfo.Encryptions["aes256-cbc"]
                = new CipherInfo(256, (key, iv) => new NativeAesCipherCbc(key, iv));
            // not recommended
            connectionInfo.Encryptions["arcfour256"]
                = new CipherInfo(256, (key, iv) => new Arc4Cipher(key, true));
            connectionInfo.Encryptions["arcfour"]
                = new CipherInfo(128, (key, iv) => new Arc4Cipher(key, false));
            connectionInfo.Encryptions["arcfour128"]
                = new CipherInfo(128, (key, iv) => new Arc4Cipher(key, true));
            connectionInfo.Encryptions["blowfish-cbc"]
                = new CipherInfo(128, (key, iv) => new BlowfishCipher(key, new CbcCipherMode(iv), null));
            connectionInfo.Encryptions["cast128-cbc"]
                = new CipherInfo(128, (key, iv) => new CastCipher(key, new CbcCipherMode(iv), null));
            connectionInfo.Encryptions["3des-cbc"]
                = new CipherInfo(192, (key, iv) => new NativeTripleDesCipherCbc(key, iv));
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
                    Timeout = new TimeSpan(0, 0, 20)
                };
                InitEncryption(connectionInfo);

                var sshClient = new SshClient(connectionInfo);
                sshClient.ErrorOccurred += (sender, args) =>
                {
                    Logger.Log(args.Exception.Message, LogLevel.Error);
                    // use cleanup on other thread to prevent race condition
                    CleanupDeferred();
                };
                sshClient.Connect();

                if (DeployAgent)
                {
                    DoDeployAgent(sshClient, DeployPath);
                }

                /*
                 * COMPlus_EnableDiagnostics turns off clr-debug-pipe
                 * https://github.com/dotnet/coreclr/blob/master/Documentation/building/debugging-instructions.md
                 */
                var sshCommand =
                    sshClient.CreateCommand($"COMPlus_EnableDiagnostics=0 dotnet {DeployPath}/DevSyncAgent.dll");

                sshCommand.BeginExecute(ar =>
                {
                    SetAgentExitCode(sshCommand.ExitStatus, sshCommand.Error);
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
