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

public class TemplateEmailType : IEmailType
{
    // TODO: Remove the services you don't need
    private readonly IAiArntIvanService _aiArntIvanService;
    private readonly IArchiveService _archiveService;
    private readonly IGraphService _graphService;

    private readonly string _postmottakUpn = "";
    private readonly string[] _subjects = [
    ];

    private readonly List<string> _toRecipients = [];

    private TemplateChatResult? _result;  // TODO: Change this
    
    public bool Enabled => false; // TODO: Change this (probably)
    public bool IncludeFunFact => true;
    public string Title => "Template"; // TODO: Change this

    public TemplateEmailType(IServiceProvider serviceProvider)
    {
        // TODO: Remove the services you don't need
        _aiArntIvanService = serviceProvider.GetService<IAiArntIvanService>()!;
        _archiveService = serviceProvider.GetService<IArchiveService>()!;
        _graphService = serviceProvider.GetService<IGraphService>()!;
        
        var configuration = serviceProvider.GetService<IConfiguration>()!;
        if (Enabled)
        {
            _postmottakUpn = configuration["Postmottak_UPN"] ??
                             throw new InvalidOperationException("Postmottak_UPN is not set in configuration");
            // TODO: Change this
            _toRecipients = configuration["EmailType_Template_Addresses"]?.Split(',').ToList() ??
                            throw new InvalidOperationException(
                                "EmailType_Innsyn_Addresses is not set in configuration");
            // TODO: Add more variables if needed
        }
    }
    
    public async Task<bool> MatchCriteria(Message message)
    {
        await Task.CompletedTask;

        if (!_subjects.Any(subject => message.Subject!.Contains(subject, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }
        
        // TODO: Change this
        var (_, result) = await _aiArntIvanService.Ask<TemplateChatResult>(message.Body!.Content!);
        if (result is null || !result.Property3)
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
            _result = JsonSerializer.Deserialize<TemplateChatResult>(flowStatus.Result.ToString()!);
        }
        
        if (_result is null)
        {
            throw new InvalidOperationException("Result is null. Somethings wrong");
        }
        
        // TODO: Change this
        string forwardMessage = @$"Denne e-posten er håndtert av KI og videresendt på begrunnelse: {_result.Property}.
                                    <br />Ta kontakt med arkivet dersom du mener at dette er feil.";
        
        await _graphService.ForwardMailMessage(_postmottakUpn, flowStatus.Message.Id!, _toRecipients, HelperTools.GenerateHtmlBox(forwardMessage));
        
        // TODO: Change this
        return $"Denne e-posten er håndtert av KI på begrunnelse: {_result.Property2}, og videresendt til <ul>{string.Join("", _toRecipients.Select(recipient => $"<li>{recipient}</li>"))}</ul>";
    }
}