using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
using postmottak_arkivering_dotnet.Contracts.Ai;
using postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;
using postmottak_arkivering_dotnet.Contracts.Email;
using postmottak_arkivering_dotnet.Services;
using postmottak_arkivering_dotnet.Services.Ai;
using postmottak_arkivering_dotnet.Utils;
using Serilog.Context;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace postmottak_arkivering_dotnet.Functions;

public class Archive
{
    private readonly IAiArntIvanService _aiArntIvanService;
    private readonly IAiPluginTestService _aiPluginTestService;
    private readonly IBlobService _blobService;
    private readonly IEmailTypeService _emailTypeService;
    private readonly IGraphService _graphService;
    private readonly ILogger<Archive> _logger;
    private readonly IStatisticsService _statisticsService;
    
    private readonly string _blobStorageFailedName;
    private readonly string _blobStorageQueueName;
    private readonly string _mailFolderInboxId;
    private readonly string _mailFolderManualHandlingId;
    private readonly string _mailFolderFinishedId;
    private readonly string _postboxUpn;
    private readonly int[] _retryIntervals;

    public Archive(IConfiguration configuration,
        IAiArntIvanService aiArntIvanService,
        IAiPluginTestService aiPluginTestService,
        IBlobService blobService,
        IEmailTypeService emailTypeService,
        IGraphService graphService,
        ILogger<Archive> logger,
        IStatisticsService statisticsService)
    {
        _aiArntIvanService = aiArntIvanService;
        _aiPluginTestService = aiPluginTestService;
        _blobService = blobService;
        _emailTypeService = emailTypeService;
        _graphService = graphService;
        _logger = logger;
        _statisticsService = statisticsService;

        _blobStorageFailedName = configuration["BLOB_STORAGE_FAILED_NAME"] ?? "failed";
        _blobStorageQueueName = configuration["BLOB_STORAGE_QUEUE_NAME"] ?? "queue";
        _retryIntervals = configuration["RETRY_INTERVALS"]?.Split(',').Select(int.Parse).ToArray() ?? throw new NullReferenceException();
        
        _mailFolderInboxId = configuration["POSTMOTTAK_MAIL_FOLDER_INBOX_ID"] ?? throw new NullReferenceException();
        _mailFolderManualHandlingId = configuration["POSTMOTTAK_MAIL_FOLDER_MANUALHANDLING_ID"] ?? throw new NullReferenceException();
        _mailFolderFinishedId = configuration["POSTMOTTAK_MAIL_FOLDER_FINISHED_ID"] ?? throw new NullReferenceException();
        _postboxUpn = configuration["POSTMOTTAK_UPN"] ?? throw new NullReferenceException();
    }

    [Function("ArchiveEmails")]
    [OpenApiOperation(operationId: "ArchiveEmails")]
    [OpenApiSecurity("Authentication", SecuritySchemeType.ApiKey, Name = "X-Functions-Key", In = OpenApiSecurityLocationType.Header)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, contentType: "application/json", typeof(ArchiveOkResponse), Description = "Executed successfully")]
    public async Task<IActionResult> ArchiveEmails([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        ArchiveOkResponse response = await GetAndHandleEmails();

        return new OkObjectResult(response);
    }
    
    [Function("GetAndHandleEmailsTimer")]
    public async Task GetAndHandleEmailsTrigger([TimerTrigger("0 0 */1 * * *")] TimerInfo myTimer)
    {
        await GetAndHandleEmails();
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
            case "ArntIvan":
            {
                var (_, result) = await _aiArntIvanService.Ask<GeneralChatResult>(promptRequest.Prompt);
                return new OkObjectResult(result);
            }
            case "Pengetransporten":
            {
                var (_, result) = await _aiArntIvanService.Ask<PengetransportenChatResult>(promptRequest.Prompt);
                return new OkObjectResult(result);
            }
            case "PluginTest":
            {
                var (_, result) = await _aiPluginTestService.Ask(promptRequest.Prompt);
                return new OkObjectResult(result);
            }
            case "Rf1350":
            {
                var (_, result) = await _aiArntIvanService.Ask<Rf1350ChatResult>(promptRequest.Prompt);
                return new OkObjectResult(result);
            }
            default:
                return new BadRequestObjectResult("Unknown agent");
        }
    }

    private async Task<ArchiveOkResponse> GetAndHandleEmails()
    {
        var mailMessages = await _graphService.GetMailMessages(_postboxUpn, _mailFolderInboxId);
        if (mailMessages.Count == 0)
        {
            _logger.LogInformation("No messages found in Inbox folder");
            return new ArchiveOkResponse();
        }

        var mailBlobs = await _blobService.ListBlobs(_blobStorageQueueName);

        List<(IEmailType, FlowStatus)> messagesToHandle = [];
        List<Message> unknownMessages = [];

        foreach (var message in mailMessages)
        {
            BlobItem? blobItem = mailBlobs.Find(blob => blob.Name.Contains(message.Id!));

            if (blobItem is not null)
            { 
                var flowStatus = await _blobService.DownloadBlobContent<FlowStatus?>(blobItem.Name);
                if (flowStatus is null)
                {
                     _logger.LogError("Failed to download blob content for BlobName {BlobName}. Blob might be hold on (be strong) to longer than needed. Check blob storage container retention", blobItem.Name);
                     continue;
                }

                if (flowStatus.SendToArkivarerForHandling)
                {
                    // TODO: Remove with time
                    _logger.LogWarning("Hit kommer vi absolutt aldri! MessageId {MessageId} is unhandelable. Send to arkivarer for handling", message.Id);
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

        var okResponse = new ArchiveOkResponse
        {
            HandledMessages = messagesToHandle.Select(message => new HandledMessage
            {
                MessageId = message.Item2.Message.Id,
                Type = message.Item2.Type
            }).ToList(),
            UnhandledMessageIds = unknownMessages.Select(message => message.Id).ToList()
        };

        _logger.LogInformation("Handled {HandledMessageCount} messages. {UnknownMessageCount} messages are unhandled",
            messagesToHandle.Count, unknownMessages.Count);

        return okResponse;
    }
    
    [SuppressMessage("ReSharper", "StructuredMessageTemplateProblem")]
    private async Task HandleKnownMessageTypes(List<(IEmailType, FlowStatus)> messagesToHandle)
    {
        foreach ((IEmailType emailType, FlowStatus flowStatus) in messagesToHandle)
        {
            using (GlobalLogContext.PushProperty("MessageId", flowStatus.Message.Id))
            using (GlobalLogContext.PushProperty("EmailType", flowStatus.Type))
            {
                try
                {
                    _logger.LogInformation("Starting {EmailType}.HandleMessage for MessageId {MessageId}");
                    var handledMessage = await emailType.HandleMessage(flowStatus);
                    var funFact = emailType.IncludeFunFact ? await _aiArntIvanService.FunFact() : string.Empty;
                    var funFactMessage = emailType.IncludeFunFact && !string.IsNullOrEmpty(funFact)
                        ? $"<br />{funFact}"
                        : string.Empty;

                    string comment = HelperTools.GenerateHtmlBox($@"
                                    Automatisk håndteringstype: <b>{emailType.Title}</b><br />
                                    Klokkeslett: <i>{DateTime.Now:dd.MM.yyyy HH:mm:ss}</i><br />
                                    Melding: {handledMessage}
                                    {funFactMessage}");

                    // update body to reflect that its handled
                    Message message = new Message
                    {
                        Body = new ItemBody
                        {
                            Content = $"{comment}<br /><br />{flowStatus.Message.Body!.Content}",
                            ContentType = BodyType.Html
                        },
                    };

                    await _graphService.PatchMailMessage(_postboxUpn, flowStatus.Message.Id!, message);

                    await _graphService.MoveMailMessage(_postboxUpn, flowStatus.Message.Id!, _mailFolderFinishedId);

                    await RemoveFlowStatusBlob(flowStatus.Type, flowStatus.Message.Id!);
                    
                    await _statisticsService.InsertStatistics(handledMessage, flowStatus.Message.Id!, flowStatus.Type, flowStatus.Message.From?.EmailAddress?.Address);
                    
                    _logger.LogInformation("Finished {EmailType}.HandleMessage for MessageId {MessageId} with result {HandledMessage}",
                        flowStatus.Type, flowStatus.Message.Id, handledMessage);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling MessageId {MessageId} with {EmailType}");
                    flowStatus.ErrorMessage = ex.Message;
                    flowStatus.ErrorStack = ex.StackTrace;
                    flowStatus.RunCount++;

                    if (flowStatus.SendToArkivarerForHandling || flowStatus.RunCount > _retryIntervals.Length)
                    {
                        _logger.LogWarning(
                            "MessageId {MessageId} is unhandelable. Message will be moved to manual handling folder and arkivarer must do something!");
                        await _graphService.MoveMailMessage(_postboxUpn, flowStatus.Message.Id!,
                            _mailFolderManualHandlingId);

                        await UpsertFlowStatusBlob(flowStatus.Message.Id!, flowStatus, _blobStorageFailedName);
                        await RemoveFlowStatusBlob(flowStatus.Type, flowStatus.Message.Id!);
                        continue;
                    }

                    int retryAfterSeconds = _retryIntervals[flowStatus.RunCount - 1];
                    flowStatus.RetryAfter = DateTime.UtcNow.AddSeconds(retryAfterSeconds);

                    await UpsertFlowStatusBlob(flowStatus.Message.Id!, flowStatus);
                }
            }
        }
    }

    private async Task HandleUnknownMessages(List<Message> messages)
    {
        foreach (var message in messages)
        {
            _logger.LogInformation("MessageId {MessageId} is of an unknown type and will be moved to manual handling folder", message.Id);
            
            await _statisticsService.InsertStatistics("Email with unknown type", message.Id!, "Unknown", message.From?.EmailAddress?.Address);
            
            await _graphService.MoveMailMessage(_postboxUpn, message.Id!, _mailFolderManualHandlingId);
        }
    }
    
    private async Task UpsertFlowStatusBlob(string messageId, FlowStatus flowStatus, string? folder = null)
    {
        try
        {
            folder ??= _blobStorageQueueName;
            await _blobService.UploadBlob($"{folder}/{flowStatus.Type}/{messageId}-flowstatus.json",
                JsonSerializer.Serialize(flowStatus, new JsonSerializerOptions
                {
                    IndentSize = 2,
                    WriteIndented = true
                }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to serialize flowStatus or upload blob for MessageId {MessageId}. Things might happen multiple times", messageId);
        }
    }
    
    private async Task RemoveFlowStatusBlob(string flowType, string messageId, string? folder = null)
    {
        folder ??= _blobStorageQueueName;
        var blobPath = $"{folder}/{flowType}/{messageId}-flowstatus.json";
        
        try
        {
            await _blobService.RemoveBlobs(blobPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove blob with BlobPath {BlobPath}. Blob might be hold on (be strong) to longer than needed. Check blob storage container retention", blobPath);
        }
    }
}