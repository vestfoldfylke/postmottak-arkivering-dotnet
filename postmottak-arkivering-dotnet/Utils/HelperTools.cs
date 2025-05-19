using System;

namespace postmottak_arkivering_dotnet.Utils;

public static class HelperTools
{
    public static string GenerateHtmlBox(string message) => @$"
        <div style='border: 1pt solid #848484; padding: 10pt; background-color: #fff4d0; font-size: 12pt;
                    line-height: 12pt; font-family: 'Arial'; color: Black; text-align: left;'>
            {message}
        </div>";
    
    public static DateTimeOffset GetDateTimeOffset(DateTime? dateTime = null, string timeZone = "Europe/Oslo") =>
        TimeZoneInfo.ConvertTime(dateTime ?? DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById(timeZone));
}