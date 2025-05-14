using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph.Models;
using postmottak_arkivering_dotnet.Contracts;
using postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;
using postmottak_arkivering_dotnet.Contracts.Email;
using postmottak_arkivering_dotnet.Services;
using postmottak_arkivering_dotnet.Services.Ai;
using postmottak_arkivering_dotnet.Utils;

namespace postmottak_arkivering_dotnet.EmailTypes;

public class InnsynEmailType : IEmailType
{
    private readonly IAiArntIvanService _aiArntIvanService;
    private readonly IGraphService _graphService;

    private readonly string _postmottakUpn;
    private readonly string[] _subjects = [
        "Innsyn"
    ];

    private readonly List<string> _toRecipients;

    private InnsynChatResult? _result;
    
    public bool Enabled => true;
    public bool IncludeFunFact => true;
    public string Title => "Innsyn";

    public InnsynEmailType(IServiceProvider serviceProvider)
    {
        _aiArntIvanService = serviceProvider.GetService<IAiArntIvanService>()!;
        _graphService = serviceProvider.GetService<IGraphService>()!;
        
        var configuration = serviceProvider.GetService<IConfiguration>()!;
        _postmottakUpn = configuration["Postmottak_UPN"] ?? throw new InvalidOperationException("Postmottak_UPN is not set in configuration");
        _toRecipients = configuration["EmailType_Innsyn_Addresses"]?.Split(',').ToList() ?? throw new InvalidOperationException("EmailType_Innsyn_Addresses is not set in configuration");
    }
    
    public async Task<bool> MatchCriteria(Message message)
    {
        await Task.CompletedTask;

        if (!_subjects.Any(subject => message.Subject!.Contains(subject, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }
        
        var (_, result) = await _aiArntIvanService.Ask<InnsynChatResult>(message.Body!.Content!);
        if (result is null || !result.IsInnsyn)
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
            _result = JsonSerializer.Deserialize<InnsynChatResult>(flowStatus.Result.ToString()!);
        }
        
        if (_result is null)
        {
            throw new InvalidOperationException("Result is null. Somethings wrong");
        }
        
        string forwardMessage = @$"Denne e-posten er håndtert av KI og videresendt på begrunnelse: {_result.Description}.
                                    <br />Ta kontakt med arkivet dersom du mener at dette er feil.";
        
        await _graphService.ForwardMailMessage(_postmottakUpn, flowStatus.Message.Id!, _toRecipients, HelperTools.GenerateHtmlBox(forwardMessage));
        
        return $"Denne e-posten er håndtert av KI på begrunnelse: {_result.Description}, og videresendt til <ul>{string.Join("", _toRecipients.Select(recipient => $"<li>{recipient}</li>"))}</ul>";
    }
}