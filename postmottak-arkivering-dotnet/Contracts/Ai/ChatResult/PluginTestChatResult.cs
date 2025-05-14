using System.Collections.Generic;
using System.ComponentModel;

namespace postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;

public class PluginTestChatResult
{
    [Description("En liste med resultater")]
    public required List<PluginTestChat> Results { get; set; }
}

public class PluginTestChat
{
    [Description("Er på formatet: 00/00000")]
    public string CaseNumber { get; set; } = string.Empty;
    
    [Description("Er en hensiksmessig tittel for innholdet")]
    public string Title { get; set; } = string.Empty;
}
