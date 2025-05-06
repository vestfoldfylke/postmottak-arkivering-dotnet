using System;
using System.Collections.Generic;
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
using postmottak_arkivering_dotnet.Contracts.Ai;
using postmottak_arkivering_dotnet.Contracts.Email;
using postmottak_arkivering_dotnet.Services;
using postmottak_arkivering_dotnet.Services.Ai;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace postmottak_arkivering_dotnet.Functions;

public class Archive
{
    private readonly IBlobService _blobService;
    private readonly IGraphService _graphService;
    private readonly IAiPengetransportenService _aiPengetransportenService;
    private readonly IAiPluginTestService _aiPluginTestService;
    private readonly IAiRf1350Service _aiRf1350Service;
    private readonly ILogger<Archive> _logger;
    private readonly IEmailTypeService _emailTypeService;

    private readonly string _blobStorageContainerName;
    private readonly string _mailFolderInboxId;
    private readonly string _mailFolderManualHandlingId;
    private readonly string _mailFolderFinishedId;
    private readonly string _postboxUpn;

    public Archive(IConfiguration configuration, ILogger<Archive> logger, IGraphService graphService,
        IBlobService blobService, IAiPengetransportenService aiPengetransportenService,
        IAiPluginTestService aiPluginTestService, IAiRf1350Service aiRf1350Service, IEmailTypeService emailTypeService)
    {
        _logger = logger;
        _graphService = graphService;
        _blobService = blobService;
        _aiPengetransportenService = aiPengetransportenService;
        _aiPluginTestService = aiPluginTestService;
        _aiRf1350Service = aiRf1350Service;
        _emailTypeService = emailTypeService;
        
        _blobStorageContainerName = configuration["BlobStorageContainerName"] ?? throw new NullReferenceException();
        
        _mailFolderInboxId = configuration["Postmottak_MailFolder_Inbox_Id"] ?? throw new NullReferenceException();
        _mailFolderManualHandlingId = configuration["Postmottak_MailFolder_ManualHandling_Id"] ?? throw new NullReferenceException();
        _mailFolderFinishedId = configuration["Postmottak_MailFolder_Finished_Id"] ?? throw new NullReferenceException();
        _postboxUpn = configuration["Postmottak_UPN"] ?? throw new NullReferenceException();
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
                
                IEmailType existingEmailType = _emailTypeService.CreateEmailTypeInstance(flowStatus.Type);

                messagesToHandle.Add((existingEmailType, flowStatus));
                continue;
            }
            
            IEmailType? emailType = await _emailTypeService.GetEmailType(message);
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

    [Function("AskArntIvan")]
    [OpenApiOperation(operationId: "AskArntIvan")]
    [OpenApiSecurity("Authentication", SecuritySchemeType.ApiKey, Name = "X-Functions-Key",
        In = OpenApiSecurityLocationType.Header)]
    [OpenApiRequestBody("application/json", typeof(AiPromptRequest), Description = "Prompt to ask Arnt Ivan")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "text/plain", typeof(string), Description = "Response from Arnt Ivan")]
    [OpenApiResponseWithBody(HttpStatusCode.BadRequest, "text/plain", typeof(string),
        Description = "Unknown agent")]
    [OpenApiResponseWithBody(HttpStatusCode.InternalServerError, "text/plain", typeof(string),
        Description = "Error occured")]
    public async Task<IActionResult> AskArntIvan([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
        [FromBody] AiPromptRequest promptRequest)
    {
        _logger.LogInformation("AskArntIvan function started");
        
        switch (promptRequest.Agent)
        {
            case "Pengetransporten":
            {
                var (_, result) = await _aiPengetransportenService.Ask(promptRequest.Prompt);
                return new OkObjectResult(result);
            }
            case "PluginTest":
            {
                var (_, result) = await _aiPluginTestService.Ask(promptRequest.Prompt);
                return new OkObjectResult(result);
            }
            case "Rf1350":
            {
                var (_, result) = await _aiRf1350Service.Ask(promptRequest.Prompt);
                return new OkObjectResult(result);
            }
            default:
                return new BadRequestObjectResult("Unknown agent");
        }
    }

    private async Task HandleKnownMessageTypes(List<(IEmailType, FlowStatus)> messagesToHandle)
    {
        foreach ((IEmailType emailType, FlowStatus flowStatus) in messagesToHandle)
        {
            try
            {
                var handledMessage = await emailType.HandleMessage(flowStatus);
                
                // update body to reflect that its handled
                Message message = new Message
                {
                    Body = new ItemBody
                    {
                        Content =
                            @$"<div style='border: 1px solid black; padding: 10px; background-color: #f9f9f9;'>
                            <div style='color: red;'>Automatisk håndteringstype: <b>{emailType.Title}</b><br />
                            Klokkeslett: <i>{DateTime.Now:dd.MM.yyyy HH:mm:ss}</i><br />
                            Melding: {handledMessage}</div></div><br /><br/>{flowStatus.Message.Body!.Content}",
                        ContentType = BodyType.Html
                    },
                };

                await _graphService.PatchMailMessage(_postboxUpn, flowStatus.Message.Id!, message);
                
                await _graphService.MoveMailMessage(_postboxUpn, flowStatus.Message.Id!, _mailFolderFinishedId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling message with Id {MessageId} of EmailType {EmailType}", flowStatus.Message.Id, emailType.GetType().Name);
                flowStatus.ErrorMessage = ex.Message;
                flowStatus.ErrorStack = ex.StackTrace;
                flowStatus.RetryAfter = DateTime.UtcNow.AddSeconds(5); // TODO: Change this to a more appropriate value

                await UpsertFlowStatusBlob(flowStatus.Message.Id!, flowStatus);
            }
        }
    }

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
    
    private Task UpsertFlowStatusBlob(string messageId, FlowStatus flowStatus) =>
        _blobService.UploadBlob(_blobStorageContainerName, $"{messageId}-flowstatus.json", JsonSerializer.Serialize(flowStatus, new JsonSerializerOptions
        {
            IndentSize = 2,
            WriteIndented = true
        }));
}