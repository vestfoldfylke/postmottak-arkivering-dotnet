using Microsoft.Graph.Models;

namespace postmottak_arkivering_dotnet.Contracts.Email;

public class UnknownMessage
{
    public bool PartialMatch { get; init; }
    public required Message Message { get; init; }
    public required string Result { get; init; }
}