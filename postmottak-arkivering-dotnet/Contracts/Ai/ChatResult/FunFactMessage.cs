using System.ComponentModel;

namespace postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;

public class FunFactMessage
{
    [Description("Skal være en hyggelig, veldig veldig kort og kreativ fun-fact om arkivering. Maks en linje. Alt MÅ være eksisterende og riktige fakta. Aldri finn opp facts!")]
    public required string Message { get; init; }
}