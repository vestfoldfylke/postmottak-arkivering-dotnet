using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Graph.Models;
using postmottak_arkivering_dotnet.Services;

namespace postmottak_arkivering_dotnet.Contracts.Archive;

public interface IEmailType
{
    string Id { get; }
    string Title { get; }
    
    Task<bool> MatchCriteria(Message message);
    Task<string> HandleMessage(FlowStatus flowStatus, IArchiveService archiveService);
}

public static class EmailType
{
    private static readonly IEmailType[] EmailTypes =
    [
        new CaseNumberEmailType(),
        new Rf1350EmailType()
    ];
    
    public static async Task<IEmailType?> GetEmailType(Message message)
    {
        foreach (var emailType in EmailTypes)
        {
            if (await emailType.MatchCriteria(message))
            {
                return emailType;
            }
        }

        return null;
    }
}

public class CaseNumberEmailType : IEmailType
{
    public string Id { get; } = "Osteklut";
    public string Title { get; } = "Jarlsbergfilla";
    
    public async Task<bool> MatchCriteria(Message message)
    {
        await Task.CompletedTask;
        return message.Subject switch
        {
            "Søknad om rusmidler" => true,
            "Søknad om skudd" => true,
            _ => false
        };
    }

    public Task<string> HandleMessage(FlowStatus flowStatus, IArchiveService archiveService)
    {
        // GetCase
            // ikke hit allikevel?
        
        // Create document in case (if not already done)
            // throw shit
            
        // Reply sender
            // throw shit
        return Task.FromResult("tuttu");
    }
}

public class Rf1350EmailType : IEmailType
{
    public string Id { get; } = "RF1350";
    public string Title { get; } = "RF 13.50";

    private readonly string _fromAddress = "ikkesvar@regionalforvaltning.no";
    private readonly string[] _subjects = [
        "RF13.50 - Automatisk kvittering på innsendt søknad",
        "RF13.50 - Automatisk epost til arkiv"
    ];
    
    public async Task<bool> MatchCriteria(Message message)
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
        
        return message.From.EmailAddress.Address.Equals(_fromAddress, StringComparison.OrdinalIgnoreCase)
               && _subjects.Any(subject => message.Subject.StartsWith(subject, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<string> HandleMessage(FlowStatus flowStatus, IArchiveService archiveService)
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
            
            throw new NotImplementedException("HandleMessage not implemented for Rf1350EmailType");
        }
        
        // CreateCase
        
        
        throw new NotImplementedException("Husefbilsbvalvbsø");
    }
}