using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace DevSync.SshStarter.External
{
    class SshStarterCommand : ISshStarterCommand
    {
        private readonly Process _process;

        public event EventHandler OnExit;

        public int? ExitCode { get; private set; }

        public string Error { get; private set; }

        private readonly StringBuilder _errorLines = new StringBuilder();

        private readonly ManualResetEvent _errorWaitHandle = new ManualResetEvent(false);

        public SshStarterCommand(Process process)
        {
            _process = process;
            _process.Exited += (sender, args) =>
            {
                ExitCode = _process.ExitCode;
                SetError();
                OnExit?.Invoke(this, EventArgs.Empty);
            };
            _process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    _errorLines.AppendLine(args.Data);
                }
                else
                {
                    _errorWaitHandle.Set();
                }
            };
            _process.Start();
            _process.BeginErrorReadLine();
        }

        private void SetError()
        {
            _errorWaitHandle.WaitOne();
            lock (this)
            {
                Error ??= _errorLines.ToString();
            }
        }

        public void Dispose()
        {
            try
            {
                // the try-catch is because Kill() will throw if the process is disposed
                _process.CancelErrorRead();
                _errorWaitHandle.Set();
                _process.Kill(true);
                _errorWaitHandle.Dispose();
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
