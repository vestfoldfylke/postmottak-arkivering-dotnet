using System.ComponentModel;
using postmottak_arkivering_dotnet.Contracts.Ai.Enums;

namespace postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;

public class LoyvegarantiChatResult
{
    [Description("Er en kort beskrivelse av innholdet")]
    public required string Description { get; init; }
    
    [Description("Er alltid i store bokstaver. Og står før 'Org.nr'")]
    public required string OrganizationName { get; init; }
    
    [Description("Organisasjonsnummer: nøyaktig 9 sifre uten mellomrom, f.eks. \"123456789\". Fjern eventuelle mellomrom fra teksten (\"123 456 789\" blir \"123456789\").")]
    public required string OrganizationNumber { get; init; }
    
    public required LøyveGarantiType Type { get; init; }
}