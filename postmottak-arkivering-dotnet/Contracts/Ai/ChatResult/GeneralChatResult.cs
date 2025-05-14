using System.Collections.Generic;
using System.ComponentModel;

namespace postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;

public class GeneralChatResult
{
    [Description("Er på formatet: 00/00000")]
    public string? CaseNumber { get; init; }
    
    [Description("Skal settes til true dersom input inneholder sensitiv informasjon. Sensitiv informasjon er blant annet: Fødselsnummer, Bankkontonummer, Personnummer, Passord, Kredittkortnummer, BankID, diagnoser, helsedata")]
    public bool ContainsSensitiveData { get; init; }
    
    [Description("Er en liste med kategorier for sensitiv informasjon. Kategoriene er beskrevet i ContainsSensitiveData")]
    public List<string>? SensitiveDataCategories { get; init; }
    
    [Description("Er en kort beskrivelse av innholdet")]
    public string? Description { get; init; }
    
    [Description("Er på formatet: 00/00000-0")]
    public string? DocumentNumber { get; init; }
    
    [Description("SKAL være en Chuck Norris vits som inneholder en bjørn og en fjert")]
    public required string Joke { get; init; }
    
    [Description("Er 9 siffer langt og kan inneholde mellomrom")]
    public string? OrganizationNumber { get; init; }
    
    [Description("Er på formatet: 00-0000")]
    public string? ProjectNumber { get; init; }
    
    [Description("Er en hensiksmessig tittel for innholdet")]
    public required string Title { get; init; }
}