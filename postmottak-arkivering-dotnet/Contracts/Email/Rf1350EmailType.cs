using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Graph.Models;
using postmottak_arkivering_dotnet.Contracts.Ai;
using postmottak_arkivering_dotnet.Services;

namespace postmottak_arkivering_dotnet.Contracts.Email;

public class Rf1350EmailType : IEmailType
{
    public string Title { get; } = "RF 13.50";

    private const string FromAddress = "ikkesvar@regionalforvaltning.no";

    private readonly string[] _subjects = [
        "RF13.50 - Automatisk kvittering på innsendt søknad",
        "RF13.50 - Automatisk epost til arkiv"
    ];
    
    private const string AnmodningOmSluttutbetaling = "Anmodning om Sluttutbetaling";
    private const string AutomatiskKvitteringPåInnsendtSøknad = "Automatisk kvittering på innsendt søknad";
    private const string OverføringAvMottattSøknad = "Overføring av mottatt søknad!";

    private Rf1350ChatResult? _result;
    
    public async Task<bool> MatchCriteria(Message message, IAiAgentService aiAgentService)
    {
        await Task.CompletedTask;
        
        if (string.IsNullOrEmpty(message.From?.EmailAddress?.Address))
        {
            return false;
        }

        if (string.IsNullOrEmpty(message.Subject))
        {
            return false;
        }

        if (!message.From.EmailAddress.Address.Equals(FromAddress, StringComparison.OrdinalIgnoreCase)
            || !_subjects.Any(subject => message.Subject.StartsWith(subject, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }
        
        var (_, result) = await aiAgentService.Rf1350(message.Body!.Content!);
        if (string.IsNullOrEmpty(result?.Type) || string.IsNullOrEmpty(result?.ReferenceNumber))
        {
            return false;
        }
        
        if (!string.IsNullOrEmpty(result.ProjectNumber) && !Regex.IsMatch(result.ProjectNumber, @"^(\d{2})-(\d{1,6})$"))
        {
            return false;
        }
        
        if (!string.IsNullOrEmpty(result.ReferenceNumber) && !Regex.IsMatch(result.ReferenceNumber, @"^(\d{4})-(\d{4})$"))
        {
            return false;
        }

        _result = result;

        return true;
    }

    public async Task<string> HandleMessage(FlowStatus flowStatus, IArchiveService archiveService, IAiAgentService aiAgentService)
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

        if (_result.Type.Equals(OverføringAvMottattSøknad, StringComparison.OrdinalIgnoreCase))
        {
            return await HandleOverføringAvMottattSøknad(flowStatus, archiveService);
        }
        if (string.Equals(_result.Type, AutomatiskKvitteringPåInnsendtSøknad, StringComparison.OrdinalIgnoreCase))
        {
            return await HandleAutomatiskKvitteringPåInnsendtSøknad(flowStatus, archiveService);
        }
        if (string.Equals(_result.Type, AnmodningOmSluttutbetaling, StringComparison.OrdinalIgnoreCase))
        {
            return await HandleAnmodningOmSluttutbetaling(flowStatus, archiveService);
        }
        
        throw new InvalidOperationException($"Unknown RF13.50 type {_result.Type}");
    }
    
    private async Task<string> HandleOverføringAvMottattSøknad(FlowStatus flowStatus, IArchiveService archiveService)
    {
        if (string.IsNullOrEmpty(_result!.ProjectOwner))
        {
            throw new MissingFieldException("Project owner is missing");
        }
        
        List<string> caseStatuses = [
            "Under behandling",
            "Reservert"
        ];
        
        if (string.IsNullOrEmpty(flowStatus.Archive.CaseNumber))
        {
            var projects = await archiveService.GetProjects(new
            {
                _result!.ProjectNumber
            });

            var activeProject = projects.FirstOrDefault();
            
            if (activeProject is null)
            {
                throw new InvalidOperationException($"No projects found for the given project number {_result.ProjectNumber}");
            }
            
            var cases = await archiveService.GetCases(new
            {
                _result!.ProjectNumber,
                Title = $"RF13.50%{_result!.ReferenceNumber}%"
            });

            var activeCase = cases.FirstOrDefault(c => c is not null && caseStatuses.Contains(c["Status"]!.ToString()));

            if (activeCase is null)
            {
                var responsiblePersonEmail = activeProject["ResponsiblePerson"]?["Email"]?.ToString();
                if (string.IsNullOrEmpty(responsiblePersonEmail))
                {
                    throw new MissingFieldException($"Responsible person email is missing from ProjectNumber {_result.ProjectNumber}");
                }
                
                activeCase = await archiveService.CreateCase(new
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
    
    private async Task<string> HandleAutomatiskKvitteringPåInnsendtSøknad(FlowStatus flowStatus, IArchiveService archiveService)
    {
        if (string.IsNullOrEmpty(flowStatus.Archive.CaseNumber))
        {
            var cases = await archiveService.GetCases(new
            {
                caseNumber = flowStatus.Message.Subject
            });

            if (cases.Count == 0)
            {
                throw new InvalidOperationException("No cases found");
            }

            var caseNumber = cases.FirstOrDefault()?.AsObject()?["CaseNumber"]?.ToString();

            flowStatus.Archive.CaseNumber = caseNumber;
        }
        
        return "Arkivert og greier";
    }
    
    private async Task<string> HandleAnmodningOmSluttutbetaling(FlowStatus flowStatus, IArchiveService archiveService)
    {
        if (string.IsNullOrEmpty(flowStatus.Archive.CaseNumber))
        {
            var cases = await archiveService.GetCases(new
            {
                caseNumber = flowStatus.Message.Subject
            });

            if (cases.Count == 0)
            {
                throw new InvalidOperationException("No cases found");
            }

            var caseNumber = cases.FirstOrDefault()?.AsObject()?["CaseNumber"]?.ToString();

            flowStatus.Archive.CaseNumber = caseNumber;
        }
        
        return "Arkivert og greier";
    }
}