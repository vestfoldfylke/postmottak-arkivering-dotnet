using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph.Models;
using postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;
using postmottak_arkivering_dotnet.Services;
using postmottak_arkivering_dotnet.Services.Ai;

namespace postmottak_arkivering_dotnet.Contracts.Email.EmailTypes;

public partial class Rf1350EmailType : IEmailType
{
    private readonly IAiArntIvanService _aiArntIvanService;
    private readonly IArchiveService _archiveService;
    private readonly IGraphService _graphService;
    
    private const string FromAddress = "ikkesvar@regionalforvaltning.no";

    private readonly string[] _subjects = [
        "RF13.50 - Automatisk kvittering på innsendt søknad",
        "RF13.50 - Automatisk epost til arkiv"
    ];
    
    private readonly List<string> _caseStatuses = [
        "Under behandling",
        "Reservert"
    ];
    
    private readonly string _epostInnDocumentCategory;
    private readonly string _postmottakUpn;
    
    private const string AnmodningOmSluttutbetaling = "Anmodning om Sluttutbetaling";
    private const string AutomatiskKvitteringPaInnsendtSoknad = "Automatisk kvittering på innsendt søknad";
    private const string OverforingAvMottattSoknad = "Overføring av mottatt søknad";

    private Rf1350ChatResult? _result;

    public bool IncludeFunFact => true;
    public string Title => "RF13.50";

    public Rf1350EmailType(IServiceProvider serviceProvider)
    {
        _aiArntIvanService = serviceProvider.GetService<IAiArntIvanService>()!;
        _archiveService = serviceProvider.GetService<IArchiveService>()!;
        _graphService = serviceProvider.GetService<IGraphService>()!;
        
        IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
        _epostInnDocumentCategory = configuration["ARCHIVE_DOCUMENT_CATEGORY_EPOST_INN"] ?? throw new NullReferenceException("ARCHIVE_DOCUMENT_CATEGORY_EPOST_INN cannot be null");
        _postmottakUpn = configuration["Postmottak_UPN"] ?? throw new NullReferenceException("Postmottak_UPN cannot be null");
    }
    
    public async Task<bool> MatchCriteria(Message message)
    {
        await Task.CompletedTask;
        
        if (string.IsNullOrEmpty(message.From?.EmailAddress?.Address))
        {
            return false;
        }

        if (!message.From.EmailAddress.Address.Equals(FromAddress, StringComparison.OrdinalIgnoreCase)
            || !_subjects.Any(subject => message.Subject!.StartsWith(subject, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }
        
        var (_, result) = await _aiArntIvanService.Ask<Rf1350ChatResult>(message.Body!.Content!);
        if (string.IsNullOrEmpty(result?.Type) || string.IsNullOrEmpty(result.ReferenceNumber))
        {
            return false;
        }
        
        if (!string.IsNullOrEmpty(result.ProjectNumber) && !RegexProjectNumber().IsMatch(result.ProjectNumber))
        {
            return false;
        }
        
        if (!string.IsNullOrEmpty(result.ReferenceNumber) && !RegexReferenceNumber().IsMatch(result.ReferenceNumber))
        {
            return false;
        }

        _result = result;

        return true;
    }

    public async Task<string> HandleMessage(FlowStatus flowStatus)
    {
        if (flowStatus.Result is null)
        {
            flowStatus.Result = _result;
        }
        else
        {
            _result = JsonSerializer.Deserialize<Rf1350ChatResult>(flowStatus.Result.ToString()!);
        }
        
        if (_result is null)
        {
            throw new InvalidOperationException("Result is null. Somethings wrong");
        }

        if (_result.Type.Equals(OverforingAvMottattSoknad, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(_result.ProjectOwner))
            {
                throw new MissingFieldException("Project owner is missing");
            }
            
            return await HandleOverforingAvMottattSoknad(flowStatus);
        }
        
        if (_result.Type.Equals(AutomatiskKvitteringPaInnsendtSoknad, StringComparison.OrdinalIgnoreCase))
        {
            return await HandleAutomatiskKvitteringPaInnsendtSoknad(flowStatus);
        }
        
        if (_result.Type.Equals(AnmodningOmSluttutbetaling, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(_result.ProjectOwner))
            {
                throw new MissingFieldException("Project owner is missing");
            }
            
            return await HandleAnmodningOmSluttutbetaling(flowStatus);
        }
        
        throw new InvalidOperationException($"Unknown {Title} type {_result.Type}");
    }
    
    private async Task<string> HandleOverforingAvMottattSoknad(FlowStatus flowStatus)
    {
        if (string.IsNullOrEmpty(_result!.OrganizationNumber))
        {
            throw new InvalidOperationException("Organization number is missing");
        }
        
        if (string.IsNullOrEmpty(flowStatus.Archive.CaseNumber))
        {
            var projects = await _archiveService.GetProjects(new
            {
                _result!.ProjectNumber
            });

            var activeProject = projects.FirstOrDefault();
            
            if (activeProject is null)
            {
                throw new InvalidOperationException($"No projects found for the given project number {_result.ProjectNumber}");
            }
            
            flowStatus.Archive.Project = activeProject;
            
            var cases = await _archiveService.GetCases(new
            {
                _result!.ProjectNumber,
                Title = $"RF13.50%{_result!.ReferenceNumber}%"
            });

            var activeCase = cases.FirstOrDefault(c => c is not null && _caseStatuses.Contains(c["Status"]!.ToString()));

            if (activeCase is null)
            {
                var responsiblePersonEmail = activeProject["ResponsiblePerson"]!["Email"]!.ToString();
                if (string.IsNullOrEmpty(responsiblePersonEmail))
                {
                    throw new MissingFieldException($"Responsible person email is missing from ProjectNumber {_result.ProjectNumber}");
                }
                
                activeCase = await _archiveService.CreateCase(new
                {
                    ArchiveCodes = new object[]
                    {
                        new {
                            ArchiveCode = "243",
                            ArchiveType = "FELLESKLASSE PRINSIPP",
                            Sort = 1
                        },
                        new {
                            ArchiveCode = "U01",
                            ArchiveType = "FAGKLASSE PRINSIPP",
                            Sort = 2
                        }
                    },
                    Project = _result!.ProjectNumber,
                    ResponsiblePersonEmail = responsiblePersonEmail,
                    Status = "B",
                    Title = $"RF13.50 - Søknad - {_result.ProjectName} - {_result!.ReferenceNumber} - {_result!.ProjectOwner}"
                });

                flowStatus.Archive.CaseCreated = true;
            }
            
            flowStatus.Archive.CaseNumber = activeCase["CaseNumber"]!.ToString();
        }

        if (flowStatus.Archive.SyncEnterprise is null)
        {
            var organizationNumber = _result.OrganizationNumber.Replace(" ", "");
            flowStatus.Archive.SyncEnterprise = (await _archiveService.SyncEnterprise(organizationNumber))["enterprise"];
        }
        
        if (string.IsNullOrEmpty(flowStatus.Archive.DocumentNumber))
        {
            var messageBytes = await _graphService.GetMailMessageRaw(_postmottakUpn, flowStatus.Message.Id!);
            
            List<Attachment> mailAttachments = (bool)flowStatus.Message.HasAttachments!
                ? await _graphService.GetMailMessageAttachments(_postmottakUpn, flowStatus.Message.Id!)
                : [];
            
            List<MailAttachment> attachments = mailAttachments
                .Where(a => a.OdataType == "#microsoft.graph.fileAttachment")
                .Select(_graphService.GetMailAttachment)
                .ToList();
            
            var payload = new
            {
                Archive = "Saksdokument",
                flowStatus.Archive.CaseNumber,
                Category = _epostInnDocumentCategory,
                Contacts = new object[]
                {
                    new
                    {
                        ReferenceNumber = flowStatus.Archive.SyncEnterprise!["EnterpriseNumber"]!.ToString(),
                        Role = "Avsender"
                    }
                },
                DocumentDate = DateTime.Now.ToString("O"),
                Files = new List<object>
                {
                    new
                    {
                        Format = "eml",
                        Status = "F",
                        Title = flowStatus.Message.Subject!,
                        Data = messageBytes,
                        VersionFormat = "P"
                    }
                },
                ResponsiblePersonEmail = flowStatus.Archive.Project!["ResponsiblePerson"]!["Email"]!.ToString(),
                Status = "J",
                Title = $"RF13.50 - Søknad - {_result.ProjectName} - {_result.ReferenceNumber} - {_result.ProjectOwner}",
            };
            
            attachments.ForEach(a =>
            {
                var (extension, versionFormat) = _archiveService.GetFileExtension(a.Name);
                payload.Files.Add(new
                {
                    Format = extension,
                    Status = "F",
                    Title = a.Name,
                    Data = a.Content,
                    VersionFormat = versionFormat
                });
            });
            
            var document = await _archiveService.CreateDocument(payload);

            flowStatus.Archive.DocumentNumber = document["DocumentNumber"]!.ToString();
        }

        string caseHandle = flowStatus.Archive.CaseCreated
            ? "Sak ble også automatisk opprettet siden robåten ikke fant en eksisterende sak."
            : "Robåten fant en eksisterende sak og arkiverte dokumentet i denne.";
        return $"Overføring av mottatt søknad er automatisk arkivert med dokumentnummer {flowStatus.Archive.DocumentNumber}. {caseHandle}.";
    }
    
    private async Task<string> HandleAutomatiskKvitteringPaInnsendtSoknad(FlowStatus flowStatus)
    {
        if (string.IsNullOrEmpty(flowStatus.Archive.CaseNumber) || flowStatus.Archive.SoknadSender is null)
        {
            var cases = await _archiveService.GetCases(new
            {
                _result!.ProjectNumber,
                Title = $"RF13.50%{_result!.ReferenceNumber}%"
            });

            var activeCase = cases.FirstOrDefault(c => c is not null && _caseStatuses.Contains(c["Status"]!.ToString()));

            if (activeCase?["Documents"] is null)
            {
                throw new InvalidOperationException($"No cases or documents found for the given project number {_result.ProjectNumber}. Wait for it to be created.");
            }
            
            flowStatus.Archive.Case = activeCase;
            flowStatus.Archive.CaseNumber = activeCase["CaseNumber"]!.ToString();
            
            var documentTitle = "RF13.50 - Søknad -";
            var epostInnDocumentCategory = _epostInnDocumentCategory.Replace("recno:", "");
            var lameDocuments = activeCase["Documents"]!.AsArray();
            var lameDocument = lameDocuments.FirstOrDefault(d => d!["DocumentTitle"]!.ToString().StartsWith(documentTitle, StringComparison.OrdinalIgnoreCase) && d["Category"]!["Recno"]!.ToString() == epostInnDocumentCategory);
            if (lameDocument is null)
            {
                throw new InvalidOperationException($"No document that starts with title {documentTitle} found on caseNumber {flowStatus.Archive.CaseNumber}");
            }
            
            var documents = await _archiveService.GetDocuments(new
            {
                DocumentNumber = lameDocument!["DocumentNumber"]!.ToString()
            });
            
            var soknadDocument = documents.FirstOrDefault();
            if (soknadDocument?["Contacts"] is null)
            {
                flowStatus.SendToArkivarerForHandling = true;
                throw new InvalidOperationException($"No lame document or Contacts found for the given document number {lameDocument!["DocumentNumber"]}");
            }

            flowStatus.Archive.SoknadSender = soknadDocument["Contacts"]!.AsArray().FirstOrDefault(c => c!["Role"]!.ToString() == "Avsender");
            if (flowStatus.Archive.SoknadSender?["ReferenceNumber"] is null)
            {
                flowStatus.SendToArkivarerForHandling = true;
                throw new InvalidOperationException($"No sender or ReferenceNumber found for the given document number {lameDocument!["DocumentNumber"]}");
            }
        }
        
        if (string.IsNullOrEmpty(flowStatus.Archive.DocumentNumber))
        {
            var messageBytes = await _graphService.GetMailMessageRaw(_postmottakUpn, flowStatus.Message.Id!);
            
            List<Attachment> mailAttachments = (bool)flowStatus.Message.HasAttachments!
                ? await _graphService.GetMailMessageAttachments(_postmottakUpn, flowStatus.Message.Id!)
                : [];
            
            List<MailAttachment> attachments = mailAttachments
                .Where(a => a.OdataType == "#microsoft.graph.fileAttachment")
                .Select(_graphService.GetMailAttachment)
                .ToList();
            
            var payload = new
            {
                Archive = "Saksdokument",
                flowStatus.Archive.CaseNumber,
                Category = "E-post ut",
                Contacts = new object[]
                {
                    new
                    {
                        ReferenceNumber = flowStatus.Archive.SoknadSender!["ReferenceNumber"]!.ToString(),
                        Role = "Mottaker"
                    }
                },
                DocumentDate = DateTime.Now.ToString("O"),
                Files = new List<object>
                {
                    new
                    {
                        Format = "eml",
                        Status = "F",
                        Title = flowStatus.Message.Subject!,
                        Data = messageBytes,
                        VersionFormat = "P"
                    }
                },
                ResponsiblePersonEmail = flowStatus.Archive.Case!["ResponsiblePerson"]!["Email"]!.ToString(),
                Status = "J",
                Title = "RF13.50 - Kvittering på søknad"
            };
            
            attachments.ForEach(a =>
            {
                var (extension, versionFormat) = _archiveService.GetFileExtension(a.Name);
                payload.Files.Add(new
                {
                    Format = extension,
                    Status = "F",
                    Title = a.Name,
                    Data = a.Content,
                    VersionFormat = versionFormat
                });
            });
            
            var document = await _archiveService.CreateDocument(payload);

            flowStatus.Archive.DocumentNumber = document["DocumentNumber"]!.ToString();
        }
        
        return $"Kvittering på søknad er automatisk arkivert med dokumentnummer {flowStatus.Archive.DocumentNumber}.";
    }
    
    private async Task<string> HandleAnmodningOmSluttutbetaling(FlowStatus flowStatus)
    {
        /*if (string.IsNullOrEmpty(flowStatus.Archive.CaseNumber))
        {
            var cases = await _archiveService.GetCases(new
            {
                caseNumber = flowStatus.Message.Subject
            });

            if (cases.Count == 0)
            {
                throw new InvalidOperationException("No cases found");
            }

            var caseNumber = cases.FirstOrDefault()?.AsObject()["CaseNumber"]!.ToString();

            flowStatus.Archive.CaseNumber = caseNumber;
        }
        
        return "Arkivert og greier";*/
        
        return await Task.FromResult($"Not implemented yet for {flowStatus.Type}.");
    }

    [GeneratedRegex(@"^(\d{2})-(\d{1,6})$")]
    private static partial Regex RegexProjectNumber();
    
    [GeneratedRegex(@"^(\d{4})-(\d{4})$")]
    private static partial Regex RegexReferenceNumber();
}