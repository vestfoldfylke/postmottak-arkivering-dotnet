using System.Collections.Generic;

namespace postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;

public class PengetransportenChatResult
{
    public List<string> Attachments { get; init; } = [];
    public string? ChuckNorrisJoke { get; init; }
    public string Description { get; init; } = string.Empty;
    public bool IsInvoiceRelated { get; init; }
}