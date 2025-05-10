using System.Threading.Tasks;
using Microsoft.Graph.Models;

namespace postmottak_arkivering_dotnet.Contracts.Email;

public interface IEmailType
{
    bool IncludeFunFact { get; }
    string Title { get; }
    
    Task<bool> MatchCriteria(Message message);
    Task<string> HandleMessage(FlowStatus flowStatus);
}