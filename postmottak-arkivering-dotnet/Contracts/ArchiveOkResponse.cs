using System.Collections.Generic;
using JetBrains.Annotations;

namespace postmottak_arkivering_dotnet.Contracts;

public class ArchiveOkResponse
{
    public List<HandledMessage> HandledMessages { [UsedImplicitly] get; init; } = [];
    public List<string?> UnhandledMessageIds { [UsedImplicitly] get; init; } = [];
}

public class HandledMessage
{
    public string? MessageId { [UsedImplicitly] get; init; }
    public required string Type { [UsedImplicitly] get; init; }
}