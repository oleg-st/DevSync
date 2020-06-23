using System;
using DevSync.Cryptography;
using DevSyncLib;
using DevSyncLib.Logger;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Security.Cryptography.Ciphers;
using Renci.SshNet.Security.Cryptography.Ciphers.Modes;

namespace DevSync.SshStarter.Internal
{
    public class SshStarter : ISshStarter
    {
        private PrivateKeyFile _privateKeyFile;

        public string Host { get; set; }
        public int Port { get; set; }

        public string Username { get; set; }

        private string _keyFilePath;

        public event EventHandler<SshStarterErrorEventArgs> OnConnectError;

        public string KeyFilePath
        {
            get => _keyFilePath;
            set
            {
                _keyFilePath = value;
                _privateKeyFile?.Dispose();
                _privateKeyFile = null;
            }
        }

        private SshClient _sshClient;

        public AuthenticationMethodMode AuthenticationMode { get; set; }

        protected ILogger Logger { get; set; }

        public SshStarter(ILogger logger)
        {
            Logger = logger;
        }

        protected void Cleanup()
        {
            lock (this)
            {
                _sshClient?.Dispose();
                _sshClient = null;
            }
        }

        private AuthenticationMethod GetAuthenticationMethod()
        {
            // try key
            if (AuthenticationMode == AuthenticationMethodMode.Key)
            {
                return new PrivateKeyAuthenticationMethod(Username, GetPrivateKeyFile());
            }
            // try password
            return new PasswordAuthenticationMethod(Username, GetPassword($"Enter {Username}@{Host}'s password: "));
        }

        private string GetPassword(string title)
        {
            Logger.Pause();
            Console.Write(title);
            var keyPassPhrase = "";
            Console.TreatControlCAsInput = true;
            try
            {
                do
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.C && (key.Modifiers & ConsoleModifiers.Control) != 0)
                    {
                        throw new SyncInterruptException();
                    }

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
            }
            finally
            {
                Console.TreatControlCAsInput = false;
            }

            Console.WriteLine();
            Logger.Resume();
            return keyPassPhrase;
        }

        private PrivateKeyFile GetPrivateKeyFile()
        {
            if (_privateKeyFile == null)
            {
                try
                {
                    _privateKeyFile = new PrivateKeyFile(KeyFilePath);
                }
                catch (SshPassPhraseNullOrEmptyException)
                {
                    _privateKeyFile = new PrivateKeyFile(KeyFilePath, GetPassword("Enter passphrase for key: "));
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
            // aes-ctr (most secure)
            connectionInfo.Encryptions["aes128-ctr"] =
                new CipherInfo(128, (key, iv) => AesCipherFactory.Create(key, iv, AesCipherFactory.Mode.Ctr));
            connectionInfo.Encryptions["aes192-ctr"] =
                new CipherInfo(192, (key, iv) => AesCipherFactory.Create(key, iv, AesCipherFactory.Mode.Ctr));
            connectionInfo.Encryptions["aes256-ctr"] =
                new CipherInfo(256, (key, iv) => AesCipherFactory.Create(key, iv, AesCipherFactory.Mode.Ctr));
            // aes-cbc (less secure)
            connectionInfo.Encryptions["aes128-cbc"]
                = new CipherInfo(128, (key, iv) => AesCipherFactory.Create(key, iv, AesCipherFactory.Mode.Cbc));
            connectionInfo.Encryptions["aes192-cbc"]
                = new CipherInfo(192, (key, iv) => AesCipherFactory.Create(key, iv, AesCipherFactory.Mode.Cbc));
            connectionInfo.Encryptions["aes256-cbc"]
                = new CipherInfo(256, (key, iv) => AesCipherFactory.Create(key, iv, AesCipherFactory.Mode.Cbc));
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

        public void Connect()
        {
            try
            {
                Cleanup();

                var connectionInfo = new ConnectionInfo(Host, Port, Username, GetAuthenticationMethod())
                {
                    RetryAttempts = int.MaxValue,
                    Timeout = new TimeSpan(0, 0, 20)
                };
                InitEncryption(connectionInfo);

                var sshClient = new SshClient(connectionInfo);
                sshClient.ErrorOccurred += (sender, args) =>
                {
                    OnConnectError?.Invoke(this, new SshStarterErrorEventArgs { Error = args.Exception.Message });
                };
                lock (this)
                {
                    _sshClient = sshClient;
                }
                sshClient.Connect();
            }
            catch (SshAuthenticationException ex)
            {
                throw new SshStarterAuthenticationException(ex.Message, ex);
            }
        }

        public ISshStarterCommand RunCommand(string command)
        {
            SshCommand sshCommand;
            lock (_sshClient)
            {
                sshCommand = _sshClient.CreateCommand(command);
            }
            return new SshStarterCommand(sshCommand);
        }

        public void Dispose()
        {
            Cleanup();
        }
    }
}
