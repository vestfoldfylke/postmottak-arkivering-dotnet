using System.ComponentModel;

namespace postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;

public class InnsynChatResult
{
    [Description("Du sier om innholdet er en henvendelse om innsyn i et eller flere dokumenter i arkivet")]
    public string Description { get; init; } = string.Empty;
    
    [Description("Du skal være minst 90% sikker på at innholdet er en henvendelse om innsyn i et eller flere dokumenter i arkivet før du setter IsInnsyn = true")]
    public bool IsInnsyn { get; init; }
}