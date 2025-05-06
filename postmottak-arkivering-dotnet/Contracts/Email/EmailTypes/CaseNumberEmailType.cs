using System;
using System.Threading.Tasks;
using Microsoft.Graph.Models;

namespace postmottak_arkivering_dotnet.Contracts.Email.EmailTypes;

public class CaseNumberEmailType : IEmailType
{
    public string Title { get; } = "CaseNumber";

    public CaseNumberEmailType(IServiceProvider serviceProvider)
    {
    }

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

    public Task<string> HandleMessage(FlowStatus flowStatus)
    {
        // GetCase
        // ikke hit allikevel?
        
        // Create document in case (if not already done)
            
        // Reply sender
        return Task.FromResult("Finito");
    }
}