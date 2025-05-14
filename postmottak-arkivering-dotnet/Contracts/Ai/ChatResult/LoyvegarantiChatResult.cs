using System.ComponentModel;

namespace postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;

public class LoyvegarantiChatResult
{
    [Description("Er en kort beskrivelse av innholdet")]
    public required string Description { get; init; }
    
    [Description("Er alltid i store bokstaver. Og står før 'Org.nr'")]
    public required string OrganizationName { get; init; }
    
    [Description("Er 9 siffer langt og kan inneholde mellomrom")]
    public required string OrganizationNumber { get; init; }
    
    [Description("Er en hensiksmessig tittel for innholdet")]
    public required string Title { get; init; }
}