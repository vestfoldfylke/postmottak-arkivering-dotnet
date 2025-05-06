using System.Collections.Generic;

namespace postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;

public class PluginTestChatResult
{
    public required List<PluginTestChat> Results { get; set; }
}

public class PluginTestChat
{
    public string CaseNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}
