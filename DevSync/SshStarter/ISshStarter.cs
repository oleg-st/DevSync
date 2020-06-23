using System;

namespace DevSync.SshStarter
{
    public interface ISshStarter : IDisposable
    {
        string Host { get; set; }

        int Port { get; set; }

        string Username { get; set; }

        string KeyFilePath { get; set; }

        AuthenticationMethodMode AuthenticationMode { get; set; }

        event EventHandler<SshStarterErrorEventArgs> OnConnectError;

        void Connect();

        ISshStarterCommand RunCommand(string command);
    }
}
