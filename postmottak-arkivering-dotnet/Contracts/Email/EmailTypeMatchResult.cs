namespace postmottak_arkivering_dotnet.Contracts.Email;

public enum EmailTypeMatched
{
    Yes = 1,
    No = 2,
    Maybe = 3
}

public class EmailTypeMatchResult
{
    public EmailTypeMatched Matched { get; init; }
    public string? Result { get; init; }
}