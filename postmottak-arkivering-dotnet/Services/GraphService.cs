using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
//using Azure.Core.Diagnostics;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Graph.Users.Item.MailFolders.Item.Messages;
using Microsoft.Graph.Users.Item.Messages.Item.Move;
using Microsoft.Graph.Users.Item.Messages.Item.Reply;
using Microsoft.Kiota.Abstractions;

namespace postmottak_arkivering_dotnet.Services;

public interface IGraphService
{
    Task<List<MailFolder>> GetMailFolders(string userPrincipalName);
    Task<List<MailFolder>> GetMailChildFolders(string userPrincipalName, string folderId);
    Task<List<Attachment>> GetMailMessageAttachments(string userPrincipalName, string messageId);
    Task<List<Message>> GetMailMessages(string userPrincipalName, string folderId, string[]? expandedProperties = null);
    Task<bool> MoveMailMessage(string userPrincipalName, string messageId, string destinationFolderId);
    Task<bool> ReplyMailMessage(string userPrincipalName, string messageId,
        EmailAddress fromAddress, List<EmailAddress> toAddresses, string subject, string replyBody,
        bool isHtml = false);
}

public class GraphService : IGraphService
{
    private readonly GraphServiceClient _graphClient;
    private readonly ILogger<GraphService> _logger;

    public GraphService(ILogger<GraphService> logger)
    {
        _logger = logger;
        _graphClient = CreateGraphClient();
    }

    public async Task<List<MailFolder>> GetMailFolders(string userPrincipalName)
    {
        var mailFolders = await _graphClient.Users[userPrincipalName].MailFolders.GetAsync();
        if (mailFolders?.Value is null)
        {
            return [];
        }

        return mailFolders.Value;
    }
    
    public async Task<List<MailFolder>> GetMailChildFolders(string userPrincipalName, string folderId)
    {
        //using AzureEventSourceListener listener = AzureEventSourceListener.CreateConsoleLogger();
        var childFolders = await _graphClient.Users[userPrincipalName].MailFolders[folderId].ChildFolders.GetAsync();
        if (childFolders?.Value is null)
        {
            return [];
        }

        return childFolders.Value;
    }

    public async Task<List<Attachment>> GetMailMessageAttachments(string userPrincipalName, string messageId)
    {
        var attachments = await _graphClient.Users[userPrincipalName].Messages[messageId].Attachments.GetAsync();
        if (attachments?.Value is null)
        {
            return [];
        }

        return attachments.Value;
    }

    public async Task<List<Message>> GetMailMessages(string userPrincipalName, string folderId, string[]? expandedProperties = null)
    {
        Action<RequestConfiguration<MessagesRequestBuilder.MessagesRequestBuilderGetQueryParameters>>? options =
            expandedProperties == null || expandedProperties.Length == 0
                ? null
                : options => options.QueryParameters.Expand = expandedProperties;
        
        var mailMessages = await _graphClient.Users[userPrincipalName].MailFolders[folderId].Messages.GetAsync(options);
        if (mailMessages?.Value is null)
        {
            return [];
        }

        return mailMessages.Value;
    }

    public async Task<bool> MoveMailMessage(string userPrincipalName, string messageId, string destinationFolderId)
    {
        try
        {
            await _graphClient.Users[userPrincipalName].Messages[messageId].Move.PostAsync(new MovePostRequestBody
            {
                DestinationId = destinationFolderId
            });

            return true;
        }
        catch (ODataError ex)
        {
            _logger.LogError(ex, "MessageId {MessageId} failed to be moved to FolderId {DestinationFolderId} in {UserPrincipalName}", messageId, destinationFolderId, userPrincipalName);
            return false;
        }
    }
    
    public async Task<bool> ReplyMailMessage(string userPrincipalName, string messageId,
        EmailAddress fromAddress, List<EmailAddress> toAddresses, string subject, string replyBody, bool isHtml = false)
    {
        var replyRequestBody = new ReplyPostRequestBody
        {
            Message = new Message
            {
                From = new Recipient
                {
                    EmailAddress = fromAddress
                },
                ReplyTo = toAddresses.Select(toAddress => new Recipient{ EmailAddress = toAddress }).ToList(),
                Subject = subject,
                IsRead = true,
                SentDateTime = DateTimeOffset.Now,
                Body = new ItemBody
                {
                    Content = replyBody,
                    ContentType = isHtml ? BodyType.Html : BodyType.Text
                }
            },
            Comment = "Detta er en kommentar, men hvor dukkær denna opp da \ud83e\udd37\u200d\u2642\ufe0f"
        };
        
        try
        {
            await _graphClient.Users[userPrincipalName].Messages[messageId].Reply.PostAsync(replyRequestBody);
            _logger.LogInformation("MessageId {MessageId} successfully replied to in {UserPrincipalName}. ReplyBody: {@ReplyBody}", messageId, userPrincipalName, replyRequestBody);

            return true;
        }
        catch (ODataError ex)
        {
            _logger.LogError(ex, "MessageId {MessageId} failed to be replied to in {UserPrincipalName}. ReplyBody: {@ReplyBody}", messageId, userPrincipalName, replyRequestBody);
            return false;
        }
    }
    
    private static GraphServiceClient CreateGraphClient()
    {
        string[] scopes =
        [
            "https://graph.microsoft.com/.default"
        ];
        
        string baseUrl = "https://graph.microsoft.com/v1.0";

        DefaultAzureCredentialOptions defaultAzureOptions = new DefaultAzureCredentialOptions
        {
            /*Diagnostics =
            {
                IsLoggingEnabled = true,
                IsLoggingContentEnabled = true,
                LoggedHeaderNames = { "x-ms-request-id" },
                LoggedQueryParameters = { "api-version" },
                IsAccountIdentifierLoggingEnabled = true
            }*/
        };

        var credential = new DefaultAzureCredential(defaultAzureOptions);
        
        return new GraphServiceClient(credential, scopes, baseUrl);
    }
}