using System.ComponentModel;

namespace postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;

public class Rf1350ChatResult
{
    [Description("Er 9 siffer langt og kan inneholde mellomrom")]
    public string OrganizationNumber { get; init; } = string.Empty;
    [Description("Ligger alltid etter Prosjektnavn:")]
    public string ProjectName { get; init; } = string.Empty;
    [Description("Ligger alltid etter Prosjekteier:")]
    public string ProjectOwner { get; init; } = string.Empty;
    [Description("Er på formatet: 00-0000")]
    public string ProjectNumber { get; init; } = string.Empty;
    [Description("Er på formatet: 0000-0000")]
    public string ReferenceNumber { get; init; } = string.Empty;
    [Description("Skal alltid være en av følgende typer og du må selv finne ut hvilken som stemmer ut fra input:\n- 'Anmodning om Sluttutbetaling'\n- 'Automatisk kvittering på innsendt søknad'\n- 'Overføring av mottatt søknad'")]
    public string Type { get; init; } = string.Empty;
}