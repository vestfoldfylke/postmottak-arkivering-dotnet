using System;
using System.Collections.Generic;

namespace postmottak_arkivering_dotnet.Contracts.Archive;

public class ArchiveStatus
{
    public DateTime? Archived { get; set; }
    public List<AttachmentStatus> Attachments { get; init; } = [];
    public string? CaseNumber { get; set; }
}

public record AttachmentStatus(string FileName, DateTime? WrittenToBlob);