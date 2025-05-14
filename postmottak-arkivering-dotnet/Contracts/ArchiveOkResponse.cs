using System.Collections.Generic;

namespace postmottak_arkivering_dotnet.Contracts;

public class ArchiveOkResponse
{
    public List<HandledMessage> HandledMessages { get; init; } = [];
    public List<string?> UnhandledMessageIds { get; init; } = [];
}

public class HandledMessage
{
    public string? MessageId { get; init; }
    public required string Type { get; init; }
}