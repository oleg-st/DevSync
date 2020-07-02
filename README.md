# DevSync
DevSync is a tool for real-time one-way files synchronization  
The [realsync](https://github.com/DmitryKoterov/dklab_realsync) alternative.  
DevSync uses its own synchronization protocol instead of rsync. It works much faster than realsync.  

# Requirements  
[.NET Core 3.1+ runtime](https://dotnet.microsoft.com/download/dotnet-core/) on the client and server.  
Supported operating systems are `Linux`, `macOS` or `Windows` (the same as `.NET Core`).  

# Installation:  
Download `DevSync.zip` file from [Releases](https://github.com/oleg-st/DevSync/releases) and unzip to any place.  
Run using `dotnet DevSync.dll` (all platforms) or `DevSync` (windows).  

Mac OS (brew):  
`brew cask install dotnet`  
`brew install oleg-st/devsync/devsync`  
Run using the `DevSync` command  

You must have private key file to use public key authentication.  
To automatically configure public key authentication, you can use the `--authorize-key` option. You will need to log in with the password once. If you do not have key pairs, they will be generated.  

# Usage  
Synchronize source directory to destination directory with exclude file:   
`dotnet DevSync.dll <source> <destination> <exclude file>`  

Synchronize using .realsync config file:  
`dotnet DevSync.dll --realsync <source>`  

Examples:

`dotnet DevSync.dll  --realsync /home/user/project`  
`DevSync.exe  "d:\work\project" user@server.dev:/home/user/project exclude-list.txt`  

# How it works
DevSync runs on the client, it monitors file changes and sends data to DevSyncAgent.  
DevSyncAgent runs on the server, executes DevSync commands, applies the changes.  
DevSyncAgent is deployed to `~/.devsync` on the server (requires about 45kb).  

You can use an external ssh client using the `--external-ssh` switch.  
External ssh launch command:  
`ssh -T -o 'EscapeChar none' -o 'ServerAliveInterval 30' -i <key path> -l <username> <server name>`  
