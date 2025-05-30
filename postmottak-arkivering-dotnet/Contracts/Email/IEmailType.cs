using System.Threading.Tasks;
using Microsoft.Graph.Models;

namespace postmottak_arkivering_dotnet.Contracts.Email;

public interface IEmailType
{
    bool Enabled { get; }
    bool IncludeFunFact { get; }
    string Title { get; }
    
    Task<(bool, string?)> MatchCriteria(Message message);
    Task<string> HandleMessage(FlowStatus flowStatus);
}