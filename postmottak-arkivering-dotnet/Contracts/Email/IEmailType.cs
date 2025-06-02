using System.Threading.Tasks;
using Microsoft.Graph.Models;

namespace postmottak_arkivering_dotnet.Contracts.Email;

public interface IEmailType
{
    bool Enabled { get; }
    bool IncludeFunFact { get; }
    public string Result { get; }
    string Title { get; }
    
    Task<EmailTypeMatchResult> MatchCriteria(Message message);
    Task<string> HandleMessage(FlowStatus flowStatus);
}