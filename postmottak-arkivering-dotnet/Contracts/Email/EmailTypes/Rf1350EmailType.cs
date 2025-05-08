using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph.Models;
using postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;
using postmottak_arkivering_dotnet.Services;
using postmottak_arkivering_dotnet.Services.Ai;

namespace postmottak_arkivering_dotnet.Contracts.Email.EmailTypes;

public partial class Rf1350EmailType : IEmailType
{
    private readonly IAiArntIvanService _aiArntIvanService;
    private readonly IArchiveService _archiveService;
    
    private const string FromAddress = "ikkesvar@regionalforvaltning.no";

    private readonly string[] _subjects = [
        "RF13.50 - Automatisk kvittering på innsendt søknad",
        "RF13.50 - Automatisk epost til arkiv"
    ];
    
    private readonly List<string> _caseStatuses = [
        "Under behandling",
        "Reservert"
    ];
    
    private const string AnmodningOmSluttutbetaling = "Anmodning om Sluttutbetaling";
    private const string AutomatiskKvitteringPaInnsendtSoknad = "Automatisk kvittering på innsendt søknad";
    private const string OverforingAvMottattSoknad = "Overføring av mottatt søknad";

    private Rf1350ChatResult? _result;
    
    public string Title => "RF13.50";

    public Rf1350EmailType(IServiceProvider serviceProvider)
    {
        _aiArntIvanService = serviceProvider.GetService<IAiArntIvanService>()!;
        _archiveService = serviceProvider.GetService<IArchiveService>()!;
    }
    
    public async Task<bool> MatchCriteria(Message message)
    {
        await Task.CompletedTask;
        
        if (string.IsNullOrEmpty(message.From?.EmailAddress?.Address))
        {
            return false;
        }

        if (!message.From.EmailAddress.Address.Equals(FromAddress, StringComparison.OrdinalIgnoreCase)
            || !_subjects.Any(subject => message.Subject!.StartsWith(subject, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }
        
        var (_, result) = await _aiArntIvanService.Ask<Rf1350ChatResult>(message.Body!.Content!);
        if (string.IsNullOrEmpty(result?.Type) || string.IsNullOrEmpty(result.ReferenceNumber))
        {
            return false;
        }
        
        if (!string.IsNullOrEmpty(result.ProjectNumber) && !RegexProjectNumber().IsMatch(result.ProjectNumber))
        {
            return false;
        }
        
        if (!string.IsNullOrEmpty(result.ReferenceNumber) && !RegexReferenceNumber().IsMatch(result.ReferenceNumber))
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
            _result = JsonSerializer.Deserialize<Rf1350ChatResult>(flowStatus.Result.ToString()!);
        }
        
        if (_result is null)
        {
            throw new InvalidOperationException("Result is null. Somethings wrong");
        }

        if (_result.Type.Equals(OverforingAvMottattSoknad, StringComparison.OrdinalIgnoreCase))
        {
            return await HandleOverforingAvMottattSoknad(flowStatus);
        }
        
        if (_result.Type.Equals(AutomatiskKvitteringPaInnsendtSoknad, StringComparison.OrdinalIgnoreCase))
        {
            return await HandleAutomatiskKvitteringPaInnsendtSoknad(flowStatus);
        }
        
        if (_result.Type.Equals(AnmodningOmSluttutbetaling, StringComparison.OrdinalIgnoreCase))
        {
            return await HandleAnmodningOmSluttutbetaling(flowStatus);
        }
        
        throw new InvalidOperationException($"Unknown {Title} type {_result.Type}");
    }
    
    private async Task<string> HandleOverforingAvMottattSoknad(FlowStatus flowStatus)
    {
        if (string.IsNullOrEmpty(_result!.ProjectOwner))
        {
            throw new MissingFieldException("Project owner is missing");
        }
        
        if (string.IsNullOrEmpty(flowStatus.Archive.CaseNumber))
        {
            var projects = await _archiveService.GetProjects(new
            {
                _result!.ProjectNumber
            });

            var activeProject = projects.FirstOrDefault();
            
            if (activeProject is null)
            {
                throw new InvalidOperationException($"No projects found for the given project number {_result.ProjectNumber}");
            }
            
            var cases = await _archiveService.GetCases(new
            {
                _result!.ProjectNumber,
                Title = $"RF13.50%{_result!.ReferenceNumber}%"
            });

            var activeCase = cases.FirstOrDefault(c => c is not null && _caseStatuses.Contains(c["Status"]!.ToString()));

            if (activeCase is null)
            {
                var responsiblePersonEmail = activeProject["ResponsiblePerson"]?["Email"]?.ToString();
                if (string.IsNullOrEmpty(responsiblePersonEmail))
                {
                    throw new MissingFieldException($"Responsible person email is missing from ProjectNumber {_result.ProjectNumber}");
                }
                
                activeCase = await _archiveService.CreateCase(new
                {
                    Project = _result!.ProjectNumber,
                    ResponsiblePersonEmail = responsiblePersonEmail,
                    Status = "R",
                    Title = $"RF13.50 - {_result.ProjectName} - {_result!.ReferenceNumber} - {_result!.ProjectOwner}"
                });
            }

            flowStatus.Archive.CaseNumber = activeCase["CaseNumber"]?.ToString();
        }

        return "Arkivert og greier";
    }
    
    private async Task<string> HandleAutomatiskKvitteringPaInnsendtSoknad(FlowStatus flowStatus)
    {
        /*if (string.IsNullOrEmpty(flowStatus.Archive.CaseNumber))
        {
            var cases = await _archiveService.GetCases(new
            {
                caseNumber = flowStatus.Message.Subject
            });

            if (cases.Count == 0)
            {
                throw new InvalidOperationException("No cases found");
            }

            var caseNumber = cases.FirstOrDefault()?.AsObject()["CaseNumber"]?.ToString();

            flowStatus.Archive.CaseNumber = caseNumber;
        }
        
        return "Arkivert og greier";*/

        return await Task.FromResult($"Not implemented yet for {flowStatus.Type}");
    }
    
    private async Task<string> HandleAnmodningOmSluttutbetaling(FlowStatus flowStatus)
    {
        /*if (string.IsNullOrEmpty(flowStatus.Archive.CaseNumber))
        {
            var cases = await _archiveService.GetCases(new
            {
                caseNumber = flowStatus.Message.Subject
            });

            if (cases.Count == 0)
            {
                throw new InvalidOperationException("No cases found");
            }

            var caseNumber = cases.FirstOrDefault()?.AsObject()["CaseNumber"]?.ToString();

            flowStatus.Archive.CaseNumber = caseNumber;
        }
        
        return "Arkivert og greier";*/
        
        return await Task.FromResult($"Not implemented yet for {flowStatus.Type}");
    }

    [GeneratedRegex(@"^(\d{2})-(\d{1,6})$")]
    private static partial Regex RegexProjectNumber();
    
    [GeneratedRegex(@"^(\d{4})-(\d{4})$")]
    private static partial Regex RegexReferenceNumber();
}