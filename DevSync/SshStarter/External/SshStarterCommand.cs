using System;
using System.Diagnostics;
using System.IO;

namespace DevSync.SshStarter.External
{
    class SshStarterCommand : ISshStarterCommand
    {
        private readonly Process _process;

        public event EventHandler OnExit;

        public int ExitCode { get; private set; }

        public string Error { get; private set; }

        public SshStarterCommand(Process process)
        {
            _process = process;
            _process.Exited += (sender, args) =>
            {
                ExitCode = _process.ExitCode;
                SetError();
                OnExit?.Invoke(this, EventArgs.Empty);
            };
            _process.Start();
        }

        private void SetError()
        {
            lock (this)
            {
                if (Error == null)
                {
                    Error = _process.StandardError.ReadToEnd();
                }
            }
        }

        public void Dispose()
        {
            try
            {
                // the try-catch is because Kill() will throw if the process is disposed
                _process.Kill();
            }
            catch
            {
                // ignore errors
            }
        }

        public Stream OutputStream => _process.StandardOutput.BaseStream;

        public Stream InputStream => _process.StandardInput.BaseStream;

        public void Wait()
        {
            _process.WaitForExit();
            ExitCode = _process.ExitCode;
            SetError();
            var lines = Error.Split('\n');
            foreach (var line in lines)
            {
                // detect SshStarterAuthenticationException
                if (line.ToLowerInvariant().Contains("permission denied"))
                {
                    throw new SshStarterAuthenticationException(line.Trim());
                }
            }
        }
    }
}
