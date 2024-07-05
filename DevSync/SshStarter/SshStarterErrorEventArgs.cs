using System;

namespace DevSync.SshStarter;

public class SshStarterErrorEventArgs(string error) : EventArgs
{
    public string Error { get; set; } = error;
}