using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph.Models;
using postmottak_arkivering_dotnet.Contracts;
using postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;
using postmottak_arkivering_dotnet.Contracts.Email;
using postmottak_arkivering_dotnet.Services;
using postmottak_arkivering_dotnet.Services.Ai;
using Vestfold.Extensions.Archive.Services;

namespace postmottak_arkivering_dotnet.EmailTypes;

public class LoyvegarantiEmailType : IEmailType
{
    private readonly IAiArntIvanService _aiArntIvan;
    private readonly IArchiveService _archiveService;
    private readonly IGraphService _graphService;
    
    private const string FromAddress = "post@matrixinsurance.no";
    
    private readonly List<string> _caseStatuses = [
        "Under behandling",
        "Reservert"
    ];
    
    private readonly string[] _subjects = [
        "Løyve",
        "Org.nr"
    ];

    private const string MatrixInsuranceReferenceNumber = "966431695";

    private readonly string _documentCategory = "";
    private readonly string _postmottakUpn = "";
    private readonly string _responsibleEnterpriseRecno = "";

    private LoyvegarantiChatResult? _result;

    public bool Enabled => false;
    public bool IncludeFunFact => false;
    public string Result => JsonSerializer.Serialize(_result, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    public string Title => "Løyvegaranti";

    public LoyvegarantiEmailType(IServiceProvider serviceProvider)
    {
        _aiArntIvan = serviceProvider.GetService<IAiArntIvanService>()!;
        _archiveService = serviceProvider.GetRequiredService<IArchiveService>();
        _graphService = serviceProvider.GetRequiredService<IGraphService>();
        
        IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
        
        if (Enabled)
        {
            _documentCategory = configuration["ARCHIVE_DOCUMENT_CATEGORY_EPOST_INN"] ?? throw new NullReferenceException();
            _postmottakUpn = configuration["POSTMOTTAK_UPN"] ?? throw new NullReferenceException();
            _responsibleEnterpriseRecno = configuration["EMAILTYPE_LOYVEGARANTI_RESPONSIBLE_ENTERPRISE_RECNO"] ??
                                          throw new NullReferenceException();
        }
    }
    
    public async Task<EmailTypeMatchResult> MatchCriteria(Message message)
    {
        await Task.CompletedTask;
        
        if (string.IsNullOrEmpty(message.From?.EmailAddress?.Address))
        {
            return new EmailTypeMatchResult
            {
                Matched = EmailTypeMatched.No,
                Result = "Avsender mangler. WHHAAAT?"
            };
        }
        
        if (!message.From.EmailAddress.Address.Equals(FromAddress, StringComparison.OrdinalIgnoreCase))
        {
            return new EmailTypeMatchResult
            {
                Matched = EmailTypeMatched.No,
                Result = $"Avsender er ikke {FromAddress}. Dette er ikke en {Title.ToLower()} e-post"
            };
        }
        
        if (!_subjects.Any(subject => message.Subject!.Contains(subject, StringComparison.OrdinalIgnoreCase)))
        {
            return new EmailTypeMatchResult
            {
                Matched = EmailTypeMatched.No,
                Result = $"E-postens emne inneholder ikke et gyldig søkeord for {Title.ToLower()}. Gyldige søkeord er: {string.Join(", ", _subjects)}"
            };
        }
        
        var (_, result) = await _aiArntIvan.Ask<LoyvegarantiChatResult>($"{message.Subject!} - {message.Body!.Content!}");
        if (result is null || string.IsNullOrEmpty(result.OrganizationName) || string.IsNullOrEmpty(result.OrganizationNumber))
        {
            var resultString = JsonSerializer.Serialize(result);
            return new EmailTypeMatchResult
            {
                Matched = EmailTypeMatched.Maybe,
                Result = $"Avsender og emne samsvarte med {Title.ToLower()}, men AI-resultatet indikerer at det ikke er en {nameof(LoyvegarantiEmailType)}:<br />AI-resultat:<br />{resultString}"
            };
        }

        _result = result;
        
        return new EmailTypeMatchResult
        {
            Matched = EmailTypeMatched.Yes
        };
    }

    public async Task<string> HandleMessage(FlowStatus flowStatus)
    {
        if (flowStatus.Result is null)
        {
            flowStatus.Result = _result;
        }
        else
        {
            _result = JsonSerializer.Deserialize<LoyvegarantiChatResult>(flowStatus.Result.ToString()!);
        }
        
        if (_result is null)
        {
            throw new InvalidOperationException("Result is null. Somethings wrong");
        }
        
        if (string.IsNullOrEmpty(flowStatus.Archive.CaseNumber))
        {
            var cases = await _archiveService.GetCases(new
            {
                ArchiveCode = _result.OrganizationNumber,
                Title = $"Drosjeløyve - % - {_result.OrganizationNumber}%"
            });

            var activeCase = cases.FirstOrDefault(c => c is not null && _caseStatuses.Contains(c["Status"]!.ToString()));

            if (activeCase is null)
            {
                activeCase = await _archiveService.CreateCase(new
                {
                    ArchiveCodes = new object[]
                    {
                        new {
                            ArchiveCode = _result.OrganizationNumber,
                            ArchiveType = "ORG",
                            IsManualText = true,
                            Sort = 1
                        },
                        new {
                            ArchiveCode = "N12",
                            ArchiveType = "FAGKLASSE PRINSIPP",
                            Sort = 2
                        },
                        new {
                            ArchiveCode = "&18",
                            ArchiveType = "TILLEGGSKODE PRINSIPP",
                            Sort = 3
                        }
                    },
                    CaseType = "Sak",
                    ResponsibleEnterpriseRecno = _responsibleEnterpriseRecno,
                    Status = "B",
                    SubArchive = "Løyver",
                    Title = $"Drosjeløyve - {_result.OrganizationName} - {_result.OrganizationNumber}"
                });
            }

            flowStatus.Archive.CaseNumber = activeCase["CaseNumber"]!.ToString();
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
                Category = _documentCategory,
                Contacts = new object[]
                {
                    new
                    {
                        ReferenceNumber = MatrixInsuranceReferenceNumber,
                        Role = "Avsender"
                    }
                },
                DocumentDate = DateTime.Now.ToString("O"),
                Files = new List<object>
                {
                    new
                    {
                        Format = "EML",
                        Status = "F",
                        Title = flowStatus.Message.Subject!,
                        Data = messageBytes,
                        VersionFormat = "P"
                    }
                },
                ResponsibleEnterpriseRecno = _responsibleEnterpriseRecno,
                Status = "J",
                Title = $"Løyvegaranti - {_result.OrganizationName} - {_result.OrganizationNumber}",
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
        
        return await Task.FromResult($"Denne e-posten er håndtert av KI og gjort noe med på begrunnelse: {_result.Description}");
    }
}