using System;
using System.Threading.Tasks;
using Microsoft.Graph.Models;
using postmottak_arkivering_dotnet.Services;

namespace postmottak_arkivering_dotnet.Contracts.Email;

public interface IEmailType
{
    string Title { get; }
    
    Task<bool> MatchCriteria(Message message, IAiAgentService aiAgentService);
    Task<string> HandleMessage(FlowStatus flowStatus, IArchiveService archiveService, IAiAgentService aiAgentService);
}

public static class EmailType
{
    private static readonly string[] Types =
    [
        "Rf1350EmailType",
        "CaseNumberEmailType"
    ];
    
    public static async Task<IEmailType?> GetEmailType(Message message, IAiAgentService aiAgentService)
    {
        if (string.IsNullOrEmpty(message.Body?.Content))
        {
            return null;
        }
        
        foreach (var type in Types)
        {
            IEmailType emailType =
                (Activator.CreateInstance(Type.GetType($"postmottak_arkivering_dotnet.Contracts.Email.{type}")!) as
                    IEmailType)!;

            if (await emailType.MatchCriteria(message, aiAgentService))
            {
                return emailType;
            }
        }

        return null;
    }
}

public class CaseNumberEmailType : IEmailType
{
    public string Title { get; } = "Jarlsbergfilla";
    
    public async Task<bool> MatchCriteria(Message message, IAiAgentService aiAgentService)
    {
        await Task.CompletedTask;
        return message.Subject switch
        {
            "Søknad om rusmidler" => true,
            "Søknad om skudd" => true,
            _ => false
        };
    }

    public Task<string> HandleMessage(FlowStatus flowStatus, IArchiveService archiveService, IAiAgentService aiAgentService)
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