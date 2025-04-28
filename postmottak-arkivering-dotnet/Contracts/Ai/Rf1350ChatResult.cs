using System.Collections.Generic;

namespace postmottak_arkivering_dotnet.Contracts.Ai;

public class Rf1350ChatResult
{
    public List<string> Attachments { get; set; } = [];
    public string OrganizationNumber { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectNumber { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
}