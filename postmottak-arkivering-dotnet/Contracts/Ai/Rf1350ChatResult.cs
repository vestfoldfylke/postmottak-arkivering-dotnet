using System.Collections.Generic;

namespace postmottak_arkivering_dotnet.Contracts.Ai;

public class Rf1350ChatResult
{
    public List<string> Attachments { get; init; } = [];
    public string OrganizationNumber { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public string ProjectOwner { get; init; } = string.Empty;
    public string ProjectNumber { get; init; } = string.Empty;
    public string ReferenceNumber { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
}