using System.Text.Json.Serialization;

namespace postmottak_arkivering_dotnet.Contracts.Ai.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LøyveGarantiType
{
    Løyvegaranti,
    EndringAvLøyvegaranti,
    OpphørAvLøyvegaranti
}