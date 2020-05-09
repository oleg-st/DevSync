using System;
using System.IO;

namespace DevSync.SshStarter
{
    public interface ISshStarterCommand : IDisposable
    {
        event EventHandler OnExit;

        int ExitCode { get; }

        string Error { get; }

        Stream OutputStream { get; }

        Stream InputStream { get; }

        void Wait();
    }
}
