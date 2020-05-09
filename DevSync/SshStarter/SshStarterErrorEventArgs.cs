using System;

namespace DevSync.SshStarter
{
    public class SshStarterErrorEventArgs : EventArgs
    {
        public string Error { get; set; }
    }
}
