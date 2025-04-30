using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Graph.Models;
using postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;
using postmottak_arkivering_dotnet.Services;

namespace postmottak_arkivering_dotnet.Contracts.Email.EmailTypes;

public class PengetransportenEmailType : IEmailType
{
    private readonly IArchiveService _archiveService;
    private readonly IAiAgentService _aiAgentService;

    private readonly string[] _subjects = [
        "Faktura",
        "Regning",
        "Inkasso",
        "Inkassovarsel",
        "Invoice",
        "Bill",
        "Purring",
        "Kvittering",
        "Betaling",
        "Utskrift",
        "Kreditnota",
        "Debetnota",
        "Proformafaktura",
        "Skattefaktura",
        "Salgsfaktura",
        "Oppgjør",
        "Skyldig saldo",
        "Forfalt betaling",
        "Faktureringsvarsel",
        "Betalingspåminnelse",
        "Kontoutskrift",
        "Finansdokument",
        "Transaksjonsoppføring",
        "Fakturanummer",
        "Refusjonskrav",
        "Receipt",
        "Payment",
        "Credit Note",
        "Debit Note",
        "Proforma Invoice",
        "Tax Invoice",
        "Sales Invoice",
        "Settlement",
        "Balance Due",
        "Overdue Payment",
        "Billing Notice",
        "Payment Reminder",
        "Account Statement",
        "Financial Document",
        "Transaction Record",
        "Invoice Number",
        "Refund Claim"
    ];

    private PengetransportenChatResult? _result;
    
    public string Title { get; } = "Pengetransporten";

    public PengetransportenEmailType(IAiAgentService aiAgentService, IArchiveService archiveService)
    {
        _aiAgentService = aiAgentService;
        _archiveService = archiveService;
    }
    
    public async Task<bool> MatchCriteria(Message message)
    {
        await Task.CompletedTask;

        if (string.IsNullOrEmpty(message.Subject))
        {
            return false;
        }
        
        if (!_subjects.Any(subject => message.Subject.Contains(subject, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }
        
        var (_, result) = await _aiAgentService.Pengetransporten(message.Body!.Content!);
        if (result is null || !result.IsInvoiceRelated)
        {
            return false;
        }

        _result = result;

        return true;
    }

    public async Task<string> HandleMessage(FlowStatus flowStatus)
    {
        if (flowStatus.Result is null)
        {
            flowStatus.Result = _result;
        }
        else
        {
            _result = JsonSerializer.Deserialize<PengetransportenChatResult>(flowStatus.Result.ToString()!);
        }
        
        if (_result is null)
        {
            throw new InvalidOperationException("Result is null. Somethings wrong");
        }
        
        // forward it to someone
        return await Task.FromResult($"Denne e-posten er håndtert av KI og videresendt på begrunnelse: {_result.Description}");
    }
}