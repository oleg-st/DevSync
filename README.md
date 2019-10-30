# DevSync
DevSync: replicate developer's files to server  
The [realsync](https://github.com/DmitryKoterov/dklab_realsync) alternative  

DevSync uses own synchronization protocol instead of rsync. It's faster than realsync (especially on windows because rsync is slow there).

# Requirements  
Requires [.NET Core 3.0 runtime](https://dotnet.microsoft.com/download/dotnet-core/3.0) both on source and destination  

# Usage

Synchronize source directory to destination directory with exclude file:   
dotnet DevSync.dll &lt;source&gt; &lt;destination&gt; &lt;exclude file&gt; --deploy  

Synchronize using .realsync config file:  
dotnet DevSync.dll --realsync &lt;source&gt; --deploy  

Use --deploy option to copy DevSyncAgent to ~/.devsync directory on first run (~60kb).  
You must have ~/.ssh/id_rsa key file to use private key authentication.

Examples:

dotnet DevSync.dll  --realsync /home/user/project --deploy  
DevSync.exe  d:\work\project user@server.dev:/home/user/project exclude-list.txt --deploy  
