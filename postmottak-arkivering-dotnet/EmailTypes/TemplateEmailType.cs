using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
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
    private readonly IGraphService _graphService;

    private readonly string _postmottakUpn = "";
    private readonly string[] _subjects = [
    ];

    private readonly List<string> _toRecipients = [];

    private TemplateChatResult? _result;  // TODO: Change this
    
    public bool Enabled => false; // TODO: Change this (probably)
    public bool IncludeFunFact => false;
    public string Result => JsonSerializer.Serialize(_result, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    public string Title => "Template"; // TODO: Change this

    public TemplateEmailType(IServiceProvider serviceProvider)
    {
        // TODO: Remove the services you don't need
        _aiArntIvanService = serviceProvider.GetService<IAiArntIvanService>()!;
        _graphService = serviceProvider.GetService<IGraphService>()!;
        
        var configuration = serviceProvider.GetService<IConfiguration>()!;
        if (Enabled)
        {
            _postmottakUpn = configuration["POSTMOTTAK_UPN"] ?? throw new NullReferenceException();
            // TODO: Change this
            _toRecipients = configuration["EMAILTYPE_TEMPLATE_ADDRESSES"]?.Split(',').ToList() ??
                            throw new NullReferenceException();
            // TODO: Add more if needed
        }
    }
    
    public async Task<(bool, string?)> MatchCriteria(Message message)
    {
        await Task.CompletedTask;

        if (!_subjects.Any(subject => message.Subject!.Contains(subject, StringComparison.OrdinalIgnoreCase)))
        {
            return (false, "Emnet samsvarer ikke med noen av de forventede emnene");
        }
        
        // TODO: Change this
        var (_, result) = await _aiArntIvanService.Ask<TemplateChatResult>(message.Body!.Content!);
        if (result is null || !result.Property3)
        {
            var resultString = JsonSerializer.Serialize(result);
            return (false, $"Emne samsvarte med en av de forventede {Title.ToLower()} emnene, men AI-resultatet indikerer at det ikke er en {nameof(TemplateEmailType)}:<br />AI-resultat:<br />{resultString}");
        }

        _result = result;

        return (true, null);
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