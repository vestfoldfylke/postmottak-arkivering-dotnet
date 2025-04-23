using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
//using Azure.Core.Diagnostics;
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
    Task<Message?> MoveMailMessage(string userPrincipalName, string messageId, string destinationFolderId);
    Task<bool> ReplyMailMessage(string userPrincipalName, string messageId,
        EmailAddress fromAddress, List<EmailAddress> toAddresses, string replyBody, string conversationId, string parentFolderId);
}

public class GraphService : IGraphService
{
    private readonly GraphServiceClient _graphClient;
    private readonly ILogger<GraphService> _logger;

    public GraphService(ILogger<GraphService> logger, IAuthenticationService authenticationService)
    {
        _logger = logger;
        _graphClient = authenticationService.CreateGraphClient();
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
        /*using AzureEventSourceListener listener = AzureEventSourceListener.CreateConsoleLogger();*/
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

    public async Task<Message?> MoveMailMessage(string userPrincipalName, string messageId, string destinationFolderId)
    {
        try
        {
            var message = await _graphClient.Users[userPrincipalName].Messages[messageId].Move.PostAsync(new MovePostRequestBody
            {
                DestinationId = destinationFolderId
            });

            if (message is null)
            {
                throw new InvalidOperationException("Message not moved");
            }

            return message;
        }
        catch (ODataError ex)
        {
            _logger.LogError(ex, "MessageId {MessageId} failed to be moved to FolderId {DestinationFolderId} in {UserPrincipalName}", messageId, destinationFolderId, userPrincipalName);
            return null;
        }
    }
    
    public async Task<bool> ReplyMailMessage(string userPrincipalName, string messageId,
        EmailAddress fromAddress, List<EmailAddress> toAddresses, string replyBody, string conversationId, string parentFolderId)
    {
        var replyRequestBody = new ReplyPostRequestBody
        {
            Message = new Message
            {
                From = new Recipient
                {
                    EmailAddress = fromAddress
                },
                ReplyTo = toAddresses.Select(toAddress => new Recipient{ EmailAddress = new EmailAddress
                {
                    Address = toAddress.Address,
                    Name = toAddress.Name
                }}).ToList(),
                ToRecipients = toAddresses.Select(toAddress => new Recipient{ EmailAddress = new EmailAddress
                {
                    Address = toAddress.Address,
                    Name = toAddress.Name
                }}).ToList(),
                ConversationId = conversationId,
                ParentFolderId = parentFolderId,
                IsRead = true,
                SentDateTime = DateTimeOffset.Now,
            },
            Comment = replyBody
        };
        
        try
        {
            await _graphClient.Users[userPrincipalName].Messages[messageId].Reply.PostAsync(replyRequestBody);

            return true;
        }
        catch (ODataError ex)
        {
            _logger.LogError(ex, "MessageId {MessageId} failed to be replied to in {UserPrincipalName}. ReplyBody: {@ReplyBody}", messageId, userPrincipalName, replyRequestBody);
            return false;
        }
    }
}