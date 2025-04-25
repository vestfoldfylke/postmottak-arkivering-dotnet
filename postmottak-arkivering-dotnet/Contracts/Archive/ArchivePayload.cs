namespace postmottak_arkivering_dotnet.Contracts.Archive;

// ReSharper disable InconsistentNaming
// NOTE: P360 SIF requires service and method to be camelCase
//       and parameter can be a mix of PascalCase and camelCase,
//       hence the inconsistent naming
public class ArchivePayload
{
    public required string service { get; init; }
    public required string method { get; init; }
    public object? parameter { get; init; }
}