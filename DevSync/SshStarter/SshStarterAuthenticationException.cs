using System;

namespace DevSync.SshStarter;

public class SshStarterAuthenticationException : Exception
{
    public SshStarterAuthenticationException(string message) : base(message)
    {
        }

    public SshStarterAuthenticationException(string message, Exception innerException) : base(message, innerException)
    {
        }
}