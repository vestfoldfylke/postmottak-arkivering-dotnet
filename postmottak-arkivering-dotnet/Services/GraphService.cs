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
using Microsoft.Graph.Users.Item.Messages.Item.Forward;
using Microsoft.Graph.Users.Item.Messages.Item.Move;
using Microsoft.Graph.Users.Item.Messages.Item.Reply;
using Microsoft.Kiota.Abstractions;

namespace postmottak_arkivering_dotnet.Services;

public interface IGraphService
{
    Task ForwardMailMessage(string userPrincipalName, string messageId, List<string> recipients);
    Task<List<MailFolder>> GetMailFolders(string userPrincipalName);
    Task<List<MailFolder>> GetMailChildFolders(string userPrincipalName, string folderId);
    Task<List<Attachment>> GetMailMessageAttachments(string userPrincipalName, string messageId);
    Task<List<Message>> GetMailMessages(string userPrincipalName, string folderId, string[]? expandedProperties = null);
    Task<Message?> MoveMailMessage(string userPrincipalName, string messageId, string destinationFolderId);
    Task<bool> PatchMailMessage(string userPrincipalName, string messageId, Message message);
    Task<bool> ReplyMailMessage(string userPrincipalName, string messageId,
        string fromAddress, List<string> toAddresses, string replyBody, string conversationId, string parentFolderId);
}

public class GraphService : IGraphService
{
    private readonly GraphServiceClient _graphClient;
    private readonly ILogger<GraphService> _logger;
    
    private const string ImmutableIdHeader = "Prefer";
    private const string ImmutableIdHeaderValue = "IdType=\"ImmutableId\"";

    public GraphService(ILogger<GraphService> logger, IAuthenticationService authenticationService)
    {
        _logger = logger;
        _graphClient = authenticationService.CreateGraphClient();
    }
    
    public async Task ForwardMailMessage(string userPrincipalName, string messageId, List<string> recipients)
    {
        var toRecipients = GetRecipients(recipients);
        if (toRecipients is null || toRecipients.Count == 0)
        {
            throw new ArgumentException("Recipients cannot be null or empty", nameof(recipients));
        }

        await _graphClient.Users[userPrincipalName].Messages[messageId].Forward.PostAsync(new ForwardPostRequestBody
        {
            ToRecipients = toRecipients
        }, configuration => configuration.Headers.Add(ImmutableIdHeader, ImmutableIdHeaderValue));
    }

    public async Task<List<MailFolder>> GetMailFolders(string userPrincipalName)
    {
        var mailFolders = await _graphClient.Users[userPrincipalName].MailFolders.GetAsync(
            configuration => configuration.Headers.Add(ImmutableIdHeader, ImmutableIdHeaderValue));
        if (mailFolders?.Value is null)
        {
            return [];
        }

        return mailFolders.Value;
    }
    
    public async Task<List<MailFolder>> GetMailChildFolders(string userPrincipalName, string folderId)
    {
        //using AzureEventSourceListener listener = AzureEventSourceListener.CreateConsoleLogger();
        var childFolders = await _graphClient.Users[userPrincipalName].MailFolders[folderId].ChildFolders.GetAsync(
            configuration => configuration.Headers.Add(ImmutableIdHeader, ImmutableIdHeaderValue));
        if (childFolders?.Value is null)
        {
            return [];
        }

        return childFolders.Value;
    }

    public async Task<List<Attachment>> GetMailMessageAttachments(string userPrincipalName, string messageId)
    {
        var attachments = await _graphClient.Users[userPrincipalName].Messages[messageId].Attachments.GetAsync(
            configuration => configuration.Headers.Add(ImmutableIdHeader, ImmutableIdHeaderValue));
        if (attachments?.Value is null)
        {
            return [];
        }

        return attachments.Value;
    }

    public async Task<List<Message>> GetMailMessages(string userPrincipalName, string folderId, string[]? expandedProperties = null)
    {
        /*using AzureEventSourceListener listener = AzureEventSourceListener.CreateConsoleLogger();*/
        Action<RequestConfiguration<MessagesRequestBuilder.MessagesRequestBuilderGetQueryParameters>> options = config =>
        {
            config.Headers.Add(ImmutableIdHeader, ImmutableIdHeaderValue);
            config.QueryParameters.Expand = expandedProperties;
            config.QueryParameters.Orderby = ["receivedDateTime desc"];
        };
        
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
            }, configuration => configuration.Headers.Add(ImmutableIdHeader, ImmutableIdHeaderValue));

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
    
    public async Task<bool> PatchMailMessage(string userPrincipalName, string messageId, Message message)
    {
        try
        {
            await _graphClient.Users[userPrincipalName].Messages[messageId].PatchAsync(message,
                configuration => configuration.Headers.Add(ImmutableIdHeader, ImmutableIdHeaderValue));

            return true;
        }
        catch (ODataError ex)
        {
            _logger.LogError(ex, "MessageId {MessageId} failed to be patched in {UserPrincipalName}. Message: {@Message}", messageId, userPrincipalName, message);
            throw;
        }
    }
    
    public async Task<bool> ReplyMailMessage(string userPrincipalName, string messageId,
        string fromAddress, List<string> toAddresses, string replyBody, string conversationId, string parentFolderId)
    {
        var replyRequestBody = new ReplyPostRequestBody
        {
            Message = new Message
            {
                From = new Recipient
                {
                    EmailAddress = new EmailAddress
                    {
                        Address = fromAddress
                    }
                },
                ReplyTo = GetRecipients(toAddresses),
                ToRecipients = GetRecipients(toAddresses),
                ConversationId = conversationId,
                ParentFolderId = parentFolderId,
                IsRead = true,
                SentDateTime = DateTimeOffset.Now,
            },
            Comment = replyBody
        };
        
        try
        {
            await _graphClient.Users[userPrincipalName].Messages[messageId].Reply.PostAsync(replyRequestBody,
                configuration => configuration.Headers.Add(ImmutableIdHeader, ImmutableIdHeaderValue));

            return true;
        }
        catch (ODataError ex)
        {
            _logger.LogError(ex, "MessageId {MessageId} failed to be replied to in {UserPrincipalName}. ReplyBody: {@ReplyBody}", messageId, userPrincipalName, replyRequestBody);
            return false;
        }
    }

    private static List<Recipient>? GetRecipients(List<string>? emailAddresses) =>
        emailAddresses?.Select(toAddress => new Recipient
        {
            EmailAddress = new EmailAddress
            {
                Address = toAddress
            }
        }).ToList();
}