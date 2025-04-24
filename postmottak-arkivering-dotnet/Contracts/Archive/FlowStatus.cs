using System;
using Microsoft.Graph.Models;

namespace postmottak_arkivering_dotnet.Contracts.Archive;

public class FlowStatus
{
    public DateTime? Finished { get; set; }
    public int RunCount { get; set; }
    public DateTime? RetryAfter { get; set; }

    public required string Type { get; init; }
    public Archive Archive { get; set; } = new();
    public required Message Message { get; init; }
    public string? ErrorMessage { get; set; }
    public string? ErrorStack { get; set; }
}

public class Archive
{
    public DateTime? Archived { get; set; }
    public string? CaseNumber { get; set; }
}