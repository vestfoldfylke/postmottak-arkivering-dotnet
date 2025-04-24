using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Blobs.Models;
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
        
        var mailMessages = await _graphService.GetMailMessages(_postboxUpn, _mailFolderInboxId);
        if (mailMessages.Count == 0)
        {
            _logger.LogInformation("No messages found in Inbox folder");
            return new OkResult();
        }

        var mailBlobs = await _blobService.ListBlobs(_blobStorageContainerName, "");

        List<(IEmailType, FlowStatus)> messagesToHandle = [];
        List<Message> unknownMessages = [];

        foreach (var message in mailMessages)
        {
            BlobItem? blobItem = mailBlobs.Find(blob => blob.Name.StartsWith(message.Id!));

            if (blobItem is not null)
            { 
                var flowStatus = await _blobService.DownloadBlobContent<FlowStatus?>(_blobStorageContainerName, blobItem.Name);
                if (flowStatus is null)
                {
                     _logger.LogError("Failed to download blob content for BlobName {BlobName}", blobItem.Name);
                     continue;
                }

                if (flowStatus.RetryAfter is not null && flowStatus.RetryAfter > DateTime.UtcNow)
                {
                     _logger.LogInformation("MessageId {MessageId} has retry after {RetryAfter} and will not be handled now", message.Id, flowStatus.RetryAfter);
                     continue;
                }

                Type? type = Type.GetType($"postmottak_arkivering_dotnet.Contracts.Archive.{flowStatus.Type}");
                if (type is null)
                {
                     _logger.LogError("Type {Type} not found for MessageId {MessageId}", flowStatus.Type, message.Id);
                     continue;
                }
                
                IEmailType? existingEmailType = Activator.CreateInstance(type) as IEmailType; // CaseNumberEmailType
                if (existingEmailType is null)
                {
                     _logger.LogError("Failed to create instance of IEmailType for Type {Type}", flowStatus.Type);
                     continue;
                }

                messagesToHandle.Add((existingEmailType, flowStatus));
                continue;
            }
            
            IEmailType? emailType = await EmailType.GetEmailType(message);
            if (emailType is null)
            {
                unknownMessages.Add(message);
                continue;
            }

            messagesToHandle.Add((emailType, new FlowStatus
            {
                Type = emailType.GetType().Name,
                Message = message
            }));
        }
        
        await HandleUnknownMessages(unknownMessages);
        
        await HandleKnownMessageTypes(messagesToHandle);
        
        /*List<Message> unhandledMessages = await HandleKnownSubjects(mailMessages);

        await HandleUnknownMessages(unhandledMessages);*/
        
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

    private async Task HandleKnownMessageTypes(List<(IEmailType, FlowStatus)> messagesToHandle)
    {
        foreach ((IEmailType emailType, FlowStatus flowStatus) in messagesToHandle)
        {
            try
            {
                await emailType.HandleMessage(flowStatus, _archiveService);
            }
            catch (Exception ex)
            {
                flowStatus.ErrorMessage = ex.Message;
                flowStatus.ErrorStack = ex.StackTrace;
                flowStatus.RetryAfter = DateTime.UtcNow.AddSeconds(5); // TODO: Change this to a more appropriate value

                await UpdateArchiveStatus(flowStatus.Message.Id!, flowStatus);
            }
        }
    }
    
    /*private async Task<List<Message>> HandleKnownSubjects(List<Message> messages)
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
            
            FlowStatus flowStatus = await _blobService.DownloadBlobContent<FlowStatus?>(_blobStorageContainerName, $"{message.Id}-flowstatus.json") ?? new FlowStatus();
            
            await ArchiveMessage(message, flowStatus);
            
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
    }*/

    private async Task HandleUnknownMessages(List<Message> messages)
    {
        foreach (var message in messages)
        {
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
    
    private Task UpdateArchiveStatus(string messageId, FlowStatus flowStatus) =>
        _blobService.UploadBlob(_blobStorageContainerName, $"{messageId}-flowstatus.json", JsonSerializer.Serialize(flowStatus, new JsonSerializerOptions
        {
            IndentSize = 2,
            WriteIndented = true
        }));

    /*private async Task ArchiveMessage(Message message, FlowStatus flowStatus)
    {
        if (flowStatus.Archive.Archived is not null && !string.IsNullOrEmpty(flowStatus.Archive.CaseNumber))
        {
            _logger.LogInformation("MessageId {MessageId} has already been archived at {@ArchiveDateTime} with CaseNumber {CaseNumber}",
                message.Id, flowStatus.Archive.Archived, flowStatus.Archive.CaseNumber);
            return;
        }
        
        flowStatus.Archive.Archived = DateTime.UtcNow;
        flowStatus.Archive.CaseNumber = "whatever";

        await UpdateArchiveStatus(message.Id!, flowStatus);
    }*/
}