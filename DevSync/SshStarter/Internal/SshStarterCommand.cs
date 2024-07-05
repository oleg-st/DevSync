using Renci.SshNet;
using System;
using System.Diagnostics;
using System.IO;

namespace DevSync.SshStarter.Internal;

class SshStarterCommand : ISshStarterCommand
{
    private readonly SshCommand _sshCommand;

    public int? ExitCode { get; set; }
    public string? Error { get; set; }

    public event EventHandler? OnExit;

    private readonly IAsyncResult? _asyncResult;

    private Stream? _inputStream;

    public SshStarterCommand(SshCommand sshCommand)
    {
        _sshCommand = sshCommand;
        _asyncResult = _sshCommand.BeginExecute(_ =>
        {
            ExitCode = sshCommand.ExitStatus;
            Error = sshCommand.Error;
            OnExit?.Invoke(this, EventArgs.Empty);
        });
    }

    public void Wait()
    {
        lock (this)
        {
            Debug.Assert(_asyncResult != null);
            _sshCommand.EndExecute(_asyncResult);
            ExitCode = _sshCommand.ExitStatus;
            Error = _sshCommand.Error;
        }
    }

    protected void Cleanup()
    {
        lock (this)
        {
            _sshCommand.Dispose();
        }
    }

    public void Dispose()
    {
        Cleanup();
    }

    public Stream OutputStream
    {
        get
        {
            lock (this)
            {
                return _sshCommand.OutputStream;
            }
        }
    }

    public Stream InputStream
    {
        get
        {
            lock (this)
            {
                return _inputStream ??= _sshCommand.CreateInputStream();
            }
        }
    }
}