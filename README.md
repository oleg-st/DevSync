# DevSync
DevSync: replicate developer's files to server  
The [realsync](https://github.com/DmitryKoterov/dklab_realsync) alternative  

DevSync uses own synchronization protocol instead of rsync. It's faster than realsync (especially on windows because rsync is slow there).

# Requirements  
Requires [.NET Core 3.0 runtime](https://dotnet.microsoft.com/download/dotnet-core/3.0) both on source and destination.  

# Usage

Run in console:  
`dotnet DevSync.dll` (all platforms) or `DevSync.exe` (windows).

Synchronize source directory to destination directory with exclude file:   
`dotnet DevSync.dll <source> <destination> <exclude file> --deploy`  

Synchronize using .realsync config file:  
`dotnet DevSync.dll --realsync <source> --deploy`  

Use --deploy option to copy DevSyncAgent (client) to ~/.devsync directory on destination host at least on first launch (requires ~60kb on destination host).  
You must have private key file to use public key authentication.

Examples:

`dotnet DevSync.dll  --realsync /home/user/project --deploy`  
`DevSync.exe  "d:\work\project" user@server.dev:/home/user/project exclude-list.txt --deploy`  
