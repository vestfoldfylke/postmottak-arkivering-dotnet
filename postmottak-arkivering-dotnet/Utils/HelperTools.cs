namespace postmottak_arkivering_dotnet.Utils;

public static class HelperTools
{
    public static string GenerateHtmlBox(string message) => @$"
        <div style='border: 1pt solid #848484; padding: 10pt; background-color: #fff4d0; font-size: 12pt;
                    line-height: 12pt; font-family: 'Arial'; color: Black; text-align: left;'>
            {message}
        </div>";
}