using System.ComponentModel;

namespace postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;

public class TemplateChatResult
{
    // TODO: Change this
    [Description("Dette skal være en god beskrivelse til KI for hva denne propertien skal inneholde")]
    public string Property { get; init; } = "";
    
    // TODO: Change this
    [Description("Dette skal være en god beskrivelse til KI for hva denne propertien skal inneholde")]
    public required string Property2 { get; init; }
    
    // TODO: Change this
    [Description("Dette skal være en god beskrivelse til KI for om denne propertien skal være true eller false")]
    public required bool Property3 { get; init; }
    
    // TODO: Add more if needed
}