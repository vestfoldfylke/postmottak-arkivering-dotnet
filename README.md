# postmottak-arkivering
Løsning som automatisk arkiverer gjenkjennbare e-poster fra en postboks og inn i arkivsystem

## Oppsett
- Lag en outlook regel på innboks-mappen som flytter e-post som skal sjekkes for automatisering/automatiseres til {Postmottak_MailFolder_Inbox_Id}
- Opprett en `local.settings.json` fil med følgende innhold og bytt ut med riktige verdier:
```json
{
    "IsEncrypted": false,
    "Values": {
        "AzureWebJobsStorage": "UseDevelopmentStorage=true",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
        "AppName": "postmottak-arkivering-dotnet",
        "Version": "0.0.1",
        "BetterStack_Endpoint": "https://something.betterstackdata.com",
        "BetterStack_SourceToken": "super_secret",
        "Serilog_MinimumLevel_Override_Microsoft.Hosting": "Warning",
        "Serilog_MinimumLevel_Override_Microsoft.AspNetCore": "Warning",
        "Serilog_MinimumLevel_Override_OpenApiTriggerFunction": "Warning",
        "AZURE_CLIENT_ID": "AppRegClientId",
        "AZURE_CLIENT_SECRET": "AppRegClientSecret",
        "AZURE_TENANT_ID": "AppRegTenantId",
        "Postmottak_UPN": "mailbox@domain.no",
        "Postmottak_MailFolder_Inbox_Id": "Inbox folder id",
        "Postmottak_MailFolder_Finished_Id": "Finished folder id",
        "Postmottak_MailFolder_ManualHandling_Id": "Manual handling folder id",
        "Postmottak_MailKnownSubjects": "Skilttagging,Veiherping,Busskapring"
    }
}
```

## Tanker og løsning

### Postboks i microsoft land
- Innboks (her havner alt)
    - Automatisk flyttet av regel (ting som bare skal flyttes)
    - Robot-input (ting som ikke ble flyttet av regel (altså flyttet av regel etterpå))
    - Til manuell håndtering (ting som ikke ble tatt automatisk og som arkivet må ta)
    - Automatisk arkivert
        - Søknad om spillemidler
        - Spørsmål om veiutbedring
        - ...forskjellige greier som er automatisk arkivert

- Roboten / script / noe lytter på "Robot-input" - henter alle eposter derfra
- Leser inn epostene (evt med vedlegg om det trengs) via graph api
- Sjekker om det er mulig å automatisere arkiveringen av den eposten
- Hvis ja:
    - Kjører ting som trengs for å arkivere den
    - Arkiverer
    - Setter en oppdatering på eposten (enten skriv til den faktiske eposten, eller enda bedre, send et svar på den samme? Så det havner i samme tråd) slik at arkivet har mulighet til å se hva som har skjedd med den
    - Skal vi ha mulighet for å svare opp faktisk avsender i enkelte tilfeller? Det er GØY med KI (send til Rune i første omgang)
- Hvis nei:
    - Bare flytt til manuell håndtering-mappen

### Ting å tenke på
- Løsningen MÅ enkelt kunne utvides til å håndtere flere typer e-poster
- Løsningen må kunne brukes uten KI der det er hensiktsmessig (f. eks hvis det holder å gjenkjenne et emnefelt) "SPILLEMIDLER NORSK TIPPING"
- Løsningen må holdes nede i scope, for å ikke havne i grøfta.
- Vi kan nok ikke sende alt av epost til en KI eller? Kanskje Mistral?

### KI-tanker
- Trene modell til å gi oss ønsket output - ved gitt input-epost
- Semantic kernel eller? Langchain? Noe annet?

- Agents
    - En agent som klassifiserer hva slags type epost
    - En agent som har tilgang på arkiv-api, kan hente, og lage saker / dokumenter + kan hente kodetabeller, og sette riktig metadata på arkiverte eposter

- I stedet for å bruke 2-3 uker på å trene på 10 000 eposter, som blir utdatert om et år, og må trenes på nytt
    - Legg inn de epostene som det kommer MYE av, fortell KI hva slags output vi ønsker for disse typene, og at den må være over en viss prosent sikker på at det er riktig, før vi kan si at vi kan arkivere automatisk.

- Er det mulig at arkivet selv trener roboten kontinuerlig, uten at det er nevneverdig vanskelig?


### Sidecart
- Hva om vi bruker KI på det som ikke kan arkiveres automatisk fra statiske emnefelt osv - og i første omgang lager forslag til arkivet om hvordan en e-post skal arkiveres? For å trene den?

### Teknisk løsning da
- dotnet?
- Setup / check
    - Sjekk at postboks er satt opp som vi forventer
    - Sett opp som det trengs om det ikke er det (lag mappene og drit)
- Kjøring (når det kommer inn ny epost? ELllller bare hvert 10 min ellerno - holder nok)
    - Hent e-poster via graph
    - For hver e-post:
        - Håndter