using System.ComponentModel;

namespace postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;

public class PengetransportenChatResult
{
    [Description("Du sier om innholdet er enten:\n- Faktura\n- Regning\n- Inkassovarsel\n- Purring på faktura eller regning")]
    public string Description { get; init; } = string.Empty;
    
    [Description("Du skal være minst 90% sikker på at det er en gyldig kategori før du setter invoiceProperty = true")]
    public bool IsInvoiceRelated { get; init; }
}