using System.Threading.Tasks;
using Microsoft.Graph.Models;

namespace postmottak_arkivering_dotnet.Contracts.Email;

public interface IEmailType
{
    string Title { get; }
    
    Task<bool> MatchCriteria(Message message);
    Task<string> HandleMessage(FlowStatus flowStatus);
}