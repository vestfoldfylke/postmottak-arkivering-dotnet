namespace postmottak_arkivering_dotnet.Contracts.Archive;

public class ArchivePayload
{
    public string? service { get; set; }
    public string? method { get; set; }
    public object? parameter { get; set; }
}