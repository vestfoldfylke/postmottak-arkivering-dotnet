using System;
using System.Linq;
using Microsoft.Graph.Models;

namespace postmottak_arkivering_dotnet.Utils;

public static class HelperTools
{
    private const string ExternalSubjectPrefix = "[EKSTERN]";

    public static string GenerateHtmlBox(string message) => $"""
        <div style="border: 1pt solid #848484; background-color: #fff4d0; font-size: 12pt;
                    line-height: 12pt; font-family: 'Arial'; color: Black; text-align: left;">
            {message}
        </div>
        """;

    public static string NormalizeSubject(string? subject)
    {
        if (string.IsNullOrEmpty(subject))
        {
            return string.Empty;
        }

        return subject.StartsWith(ExternalSubjectPrefix, StringComparison.OrdinalIgnoreCase)
            ? subject[ExternalSubjectPrefix.Length..].TrimStart()
            : subject;
    }
    
    public static DateTimeOffset GetDateTimeOffset(DateTime? dateTime = null, string timeZone = "Europe/Oslo") =>
        TimeZoneInfo.ConvertTime(dateTime ?? DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById(timeZone));

    public static bool IsToPostmottak(Message message, string postmottakUpn) =>
        message.ToRecipients is { Count: 1 } &&
        message.ToRecipients.Any(recipient => recipient.EmailAddress?.Address == postmottakUpn);

    public static string GetUtcDateTimeString(DateTimeOffset dateTimeOffset) =>
        dateTimeOffset.ToString("yyyy-MM-ddTHH:mm:ssZ");
}