using System.Collections.Generic;

namespace DevSyncLib;

public class AgentOptions(string destPath, List<string> excludeList)
{
    public string DestPath = destPath;
    public List<string> ExcludeList = excludeList;
}