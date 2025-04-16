using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
using postmottak_arkivering_dotnet.Services;

namespace postmottak_arkivering_dotnet.Functions;

public class Archive
{
    private readonly IGraphService _graphService;
    private readonly ILogger<Archive> _logger;
    
    private readonly string _mailFolderInboxId;
    private readonly string _mailFolderManualHandlingId;
    private readonly string _mailFolderFinishedId;
    private readonly string[] _mailKnownSubjects;
    private readonly string _postboxUpn;

    public Archive(IConfiguration configuration, ILogger<Archive> logger, IGraphService graphService)
    {
        _logger = logger;
        _graphService = graphService;
        
        var knownSubjects = configuration["Postmottak_MailKnownSubjects"] ?? "";
        
        _mailFolderInboxId = configuration["Postmottak_MailFolder_Inbox_Id"] ?? throw new NullReferenceException();
        _mailFolderManualHandlingId = configuration["Postmottak_MailFolder_ManualHandling_Id"] ?? throw new NullReferenceException();
        _mailFolderFinishedId = configuration["Postmottak_MailFolder_Finished_Id"] ?? throw new NullReferenceException();
        _mailKnownSubjects = knownSubjects.Split(",");
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
        
        // get all messages from _mailFolderInboxId (extended with attachments)
        var mailMessages = await _graphService.GetMailMessages(_postboxUpn, _mailFolderInboxId, ["attachments"]);
        if (mailMessages.Count == 0)
        {
            _logger.LogInformation("No messages found in FolderId {FolderId}", _mailFolderInboxId);
            return new OkResult();
        }
        
        List<Message> unhandledMessages = await HandleKnownSubjects(mailMessages);

        await HandleUnhandledMessages(unhandledMessages);
        
        return new OkResult();
    }

    private async Task<List<Message>> HandleKnownSubjects(List<Message> messages)
    {
        var unhandledMessages = new List<Message>();

        foreach (var message in messages)
        {
            if (string.IsNullOrEmpty(message.Subject) || string.IsNullOrEmpty(message.Id))
            {
                _logger.LogWarning("MessageId {MessageId} is missing required properties and will not be handled", message.Id);
                unhandledMessages.Add(message);
                continue;
            }
            
            if (!IsKnownSubject(message.Subject))
            {
                // TODO: Remove this log before production
                _logger.LogWarning("MessageId {MessageId} has unknown subject {Subject}", message.Id, message.Subject);
                unhandledMessages.Add(message);
                continue;
            }
            
            // TODO: Archive message and any attachments and log successful archiving
            
            if (!await _graphService.MoveMailMessage(_postboxUpn, message.Id, _mailFolderFinishedId))
            {
                unhandledMessages.Add(message);
                continue;
            }
            
            _logger.LogInformation("Successfully moved MessageId {MessageId} to finished folder", message.Id);
        }

        return unhandledMessages;
    }

    private async Task HandleUnhandledMessages(List<Message> messages)
    {
        foreach (var message in messages)
        {
            if (string.IsNullOrEmpty(message.Id))
            {
                _logger.LogWarning("MessageId {MessageId} is missing required property and can not be moved to manual handling folder", message.Id);
                continue;
            }
            
            if (!await _graphService.MoveMailMessage(_postboxUpn, message.Id, _mailFolderManualHandlingId))
            {
                _logger.LogWarning("Failed to move MessageId {MessageId} to manual handling folder", message.Id);
                continue;
            }
            
            _logger.LogInformation("Successfully moved MessageId {MessageId} to manual handling folder", message.Id);
        }
    }

    private bool IsKnownSubject(string subject) => _mailKnownSubjects.Contains(subject, StringComparer.OrdinalIgnoreCase);
}