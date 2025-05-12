using System;
using System.Text.Json.Nodes;
using Microsoft.Graph.Models;

namespace postmottak_arkivering_dotnet.Contracts;

public class FlowStatus
{
    public DateTime? RetryAfter { get; set; }
    public int RunCount { get; set; }

    public ArchiveStatus Archive { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? ErrorStack { get; set; }
    public required Message Message { get; init; }
    public object? Result { get; set; }
    public bool SendToArkivarerForHandling { get; set; }
    public required string Type { get; init; }
}

public class ArchiveStatus
{
    public JsonNode? Case { get; set; }
    public bool CaseCreated { get; set; }
    public string? CaseNumber { get; set; }
    public string? DocumentNumber { get; set; }
    public JsonNode? Project { get; set; }
    public JsonNode? SoknadSender { get; set; }
    public JsonNode? SyncEnterprise { get; set; }
}