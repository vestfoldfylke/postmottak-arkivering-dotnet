using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph.Models;
using Microsoft.OpenApi.Models;
using postmottak_arkivering_dotnet.Contracts;
using postmottak_arkivering_dotnet.Contracts.Archive;
using postmottak_arkivering_dotnet.Services;

namespace postmottak_arkivering_dotnet.Functions;

public class Archive
{
    private readonly IArchiveService _archiveService;
    private readonly IBlobService _blobService;
    private readonly IGraphService _graphService;
    private readonly ILogger<Archive> _logger;

    private readonly string _blobStorageContainerName;
    private readonly string _mailFolderInboxId;
    private readonly string _mailFolderManualHandlingId;
    private readonly string _mailFolderFinishedId;
    private readonly string[] _mailKnownSubjects;
    private readonly string _postboxUpn;

    private readonly EmailAddress _replyFromAddress;
    private readonly List<EmailAddress> _replyToAddresses;
    private readonly string _replyBody;

    public Archive(IConfiguration configuration, ILogger<Archive> logger, IGraphService graphService,
        IArchiveService archiveService, IBlobService blobService)
    {
        _logger = logger;
        _graphService = graphService;
        _archiveService = archiveService;
        _blobService = blobService;
        
        _blobStorageContainerName = configuration["BlobStorageContainerName"] ?? throw new NullReferenceException();
        
        var knownSubjects = configuration["Postmottak_MailKnownSubjects"] ?? "";
        
        _mailFolderInboxId = configuration["Postmottak_MailFolder_Inbox_Id"] ?? throw new NullReferenceException();
        _mailFolderManualHandlingId = configuration["Postmottak_MailFolder_ManualHandling_Id"] ?? throw new NullReferenceException();
        _mailFolderFinishedId = configuration["Postmottak_MailFolder_Finished_Id"] ?? throw new NullReferenceException();
        _mailKnownSubjects = knownSubjects.Split(",");
        _postboxUpn = configuration["Postmottak_UPN"] ?? throw new NullReferenceException();
        
        _replyFromAddress = new EmailAddress
        {
            Address = _postboxUpn
        };

        _replyToAddresses =
        [
            new EmailAddress
            {
                Address = configuration["Postmottak_ReplyToAddress"] ?? throw new NullReferenceException(),
            }
        ];
        
        _replyBody = configuration["Postmottak_ReplyBody"] ?? throw new NullReferenceException();
    }

    [Function("ArchiveEmails")]
    [OpenApiOperation(operationId: "ArchiveEmails")]
    [OpenApiSecurity("Authentication", SecuritySchemeType.ApiKey, Name = "X-Functions-Key", In = OpenApiSecurityLocationType.Header)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Description = "Executed successfully")]
    [OpenApiResponseWithBody(HttpStatusCode.InternalServerError, "application/json", typeof(ErrorResponse), Description = "Error occured")]
    public async Task<IActionResult> ArchiveEmails([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        _logger.LogInformation("ArchiveEmails function started");
        
        var mailMessages = await _graphService.GetMailMessages(_postboxUpn, _mailFolderInboxId, ["attachments"]);
        if (mailMessages.Count == 0)
        {
            _logger.LogInformation("No messages found in Inbox folder");
            return new OkResult();
        }
        
        List<Message> unhandledMessages = await HandleKnownSubjects(mailMessages);

        await HandleUnknownMessages(unhandledMessages);
        
        return new OkResult();
    }
    
    [Function("ListFolders")]
    [OpenApiOperation(operationId: "ListFolders")]
    [OpenApiSecurity("Authentication", SecuritySchemeType.ApiKey, Name = "X-Functions-Key", In = OpenApiSecurityLocationType.Header)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(List<MailFolder>), Description = "List of mail folders")]
    [OpenApiResponseWithBody(HttpStatusCode.InternalServerError, "application/json", typeof(ErrorResponse), Description = "Error occured")]
    public async Task<IActionResult> ListFolders([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        var parentFolder = req.Query.Keys.Contains("parentFolder") ? req.Query["parentFolder"].ToString() : null;
        
        var mailFolders = !string.IsNullOrEmpty(parentFolder)
            ? await _graphService.GetMailChildFolders(_postboxUpn, parentFolder)
            : await _graphService.GetMailFolders(_postboxUpn);

        return new OkObjectResult(mailFolders);
    }

    private async Task<List<Message>> HandleKnownSubjects(List<Message> messages)
    {
        var unhandledMessages = new List<Message>();

        foreach (var message in messages)
        {
            if (string.IsNullOrEmpty(message.Id))
            {
                _logger.LogWarning("Message is missing required property Id and will be ignored. Message: {@Message}", message);
                continue;
            }
            
            if (string.IsNullOrEmpty(message.Subject))
            {
                _logger.LogWarning("MessageId {MessageId} is missing required property Subject and will not be handled. Subject: {Subject}", message.Id, message.Subject);
                unhandledMessages.Add(message);
                continue;
            }
            
            if (!IsKnownSubject(message.Subject))
            {
                unhandledMessages.Add(message);
                continue;
            }
            
            ArchiveStatus archiveStatus = await _blobService.DownloadBlobContent<ArchiveStatus?>(_blobStorageContainerName, $"{message.Id}/ArchiveStatus.json") ?? new ArchiveStatus();
            if (message.Attachments is not null && archiveStatus.Attachments.Count < message.Attachments.Count)
            {
                foreach (var attachment in message.Attachments.Where(a => archiveStatus.Attachments.All(asa => asa.FileName != a.Name)))
                {
                    if (!attachment.OdataType!.Contains("fileAttachment"))
                    {
                        _logger.LogInformation("MessageId {MessageId} has attachment of unknown type ({Type}) and will not be handled",
                            message.Id, attachment.OdataType);
                        unhandledMessages.Add(message);
                        continue;
                    }
                
                    var fileAttachment = attachment as FileAttachment;
                    await _blobService.UploadBlobFromStream(_blobStorageContainerName, $"{message.Id}/attachments/{fileAttachment!.Name!}", fileAttachment.ContentBytes!);
                    archiveStatus.Attachments.Add(new AttachmentStatus(fileAttachment.Name!, DateTime.UtcNow));
                }
                
                await UpdateArchiveStatus(message.Id, archiveStatus);
            }
            
            await ArchiveMessage(message, archiveStatus);
            
            Message? postMoveMessage =
                await _graphService.MoveMailMessage(_postboxUpn, message.Id!, _mailFolderFinishedId);
            if (postMoveMessage is null)
            {
                unhandledMessages.Add(message);
                continue;
            }
            
            await _blobService.RemoveBlobs(_blobStorageContainerName, message.Id);
            
            await _graphService.ReplyMailMessage(_postboxUpn, postMoveMessage.Id!, _replyFromAddress, _replyToAddresses,
                _replyBody, postMoveMessage.ConversationId!, _mailFolderFinishedId);
            
            _logger.LogInformation("MessageId {MessageId} automatically handled and successfully moved to finished folder", message.Id);
        }

        return unhandledMessages;
    }

    private async Task HandleUnknownMessages(List<Message> messages)
    {
        foreach (var message in messages)
        {
            // TODO: Use KI to figure out how to archive this message

            _logger.LogWarning("MessageId {MessageId} has unknown subject '{Subject}' and will be moved to manual handling folder", message.Id, message.Subject);
            
            Message? postMoveMessage =
                await _graphService.MoveMailMessage(_postboxUpn, message.Id!, _mailFolderManualHandlingId);
            if (postMoveMessage is null)
            {
                continue;
            }
            
            _logger.LogInformation("MessageId {MessageId} successfully moved to manual handling folder", message.Id);
        }
    }

    private bool IsKnownSubject(string subject) => _mailKnownSubjects.Contains(subject, StringComparer.OrdinalIgnoreCase);
    
    private Task UpdateArchiveStatus(string messageId, ArchiveStatus archiveStatus) =>
        _blobService.UploadBlob(_blobStorageContainerName, $"{messageId}/ArchiveStatus.json", JsonSerializer.Serialize(archiveStatus));

    private async Task ArchiveMessage(Message message, ArchiveStatus archiveStatus)
    {
        if (archiveStatus.Archived is not null && !string.IsNullOrEmpty(archiveStatus.CaseNumber))
        {
            _logger.LogInformation("MessageId {MessageId} has already been archived at {@ArchiveStatus} with CaseNumber {CaseNumber}",
                message.Id, archiveStatus.Archived, archiveStatus.CaseNumber);
            return;
        }
        
        /*var result = await _archiveService.Archive(new ArchivePayload
        {
            method = "GetCases",
            service = "CaseService",
            parameter = new
            {
                CaseNumber = "24/00051"
            }
        });*/
        archiveStatus.Archived = DateTime.UtcNow;
        archiveStatus.CaseNumber = "whatever";
            
        await UpdateArchiveStatus(message.Id!, archiveStatus);
    }
}