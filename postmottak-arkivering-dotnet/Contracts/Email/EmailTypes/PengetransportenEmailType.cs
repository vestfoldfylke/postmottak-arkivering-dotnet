using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph.Models;
using postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;
using postmottak_arkivering_dotnet.Services;
using postmottak_arkivering_dotnet.Services.Ai;

namespace postmottak_arkivering_dotnet.Contracts.Email.EmailTypes;

public class PengetransportenEmailType : IEmailType
{
    private readonly IAiPengetransportenService _aiPengetransportenService;
    private readonly IGraphService _graphService;

    private readonly string _postmottakUpn;
    private readonly string[] _subjects = [
        "Faktura",
        "Regning",
        "Inkasso",
        "Inkassovarsel",
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
        "Bill",
        "Invoice",
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

    public PengetransportenEmailType(IServiceProvider serviceProvider)
    {
        _aiPengetransportenService = serviceProvider.GetService<IAiPengetransportenService>()!;
        _graphService = serviceProvider.GetService<IGraphService>()!;
        
        var configuration = serviceProvider.GetService<IConfiguration>()!;
        _postmottakUpn = configuration["Postmottak_UPN"] ?? throw new InvalidOperationException("Postmottak_UPN is not set in configuration");
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
        
        var (_, result) = await _aiPengetransportenService.Ask(message.Body!.Content!);
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

        List<string> toRecipients =
        [
            "testarkivering@vestfoldfylke.no"
        ];
        
        await _graphService.ForwardMailMessage(_postmottakUpn, flowStatus.Message.Id!, toRecipients);
        
        return await Task.FromResult($"Denne e-posten er håndtert av KI og videresendt til {string.Join(',', toRecipients)} på begrunnelse: {_result.Description}");
    }
}