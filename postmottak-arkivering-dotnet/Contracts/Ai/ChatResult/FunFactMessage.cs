using System.ComponentModel;

namespace postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;

public class FunFactMessage
{
    [Description("Skal være en hyggelig, kort og kreativ fun-fact om arkivering")]
    public required string Message { get; init; }
}