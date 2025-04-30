using System.Threading.Tasks;
using Microsoft.Graph.Models;
using postmottak_arkivering_dotnet.Services;

namespace postmottak_arkivering_dotnet.Contracts.Email.EmailTypes;

public class CaseNumberEmailType : IEmailType
{
    private readonly IAiAgentService _aiAgentService;
    private readonly IArchiveService _archiveService;

    public string Title { get; } = "Jarlsbergfilla";

    public CaseNumberEmailType(IAiAgentService aiAgentService, IArchiveService archiveService)
    {
        _aiAgentService = aiAgentService;
        _archiveService = archiveService;
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
        // throw shit
            
        // Reply sender
        // throw shit
        return Task.FromResult("tuttu");
    }
}