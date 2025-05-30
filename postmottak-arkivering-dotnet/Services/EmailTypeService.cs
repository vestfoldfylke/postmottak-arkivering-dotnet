using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;
using postmottak_arkivering_dotnet.Contracts.Email;
using postmottak_arkivering_dotnet.Utils;

namespace postmottak_arkivering_dotnet.Services;

public interface IEmailTypeService
{
    IEmailType CreateEmailTypeInstance(string emailType);
    Task<IEmailType?> GetEmailType(Message message);
}

public class EmailTypeService : IEmailTypeService
{
    private readonly ILogger<EmailTypeService> _logger;
    private readonly IGraphService _graphService;
    private readonly IServiceProvider _serviceProvider;

    private readonly List<Type> _emailTypes;

    private readonly string _postmottakUpn;
    private readonly string _robotLogDestinationId;
    
    private const string EmailTypeNamespace = "postmottak_arkivering_dotnet.EmailTypes";
    
    public EmailTypeService(ILogger<EmailTypeService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _graphService = serviceProvider.GetService<IGraphService>()!;
        _serviceProvider = serviceProvider;

        IConfiguration configuration = serviceProvider.GetService<IConfiguration>()!;
        _postmottakUpn = configuration["POSTMOTTAK_UPN"] ?? throw new NullReferenceException();
        _robotLogDestinationId = configuration["POSTMOTTAK_MAIL_FOLDER_ROBOT_LOG_ID"] ?? throw new NullReferenceException();

        _emailTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(IEmailType).IsAssignableFrom(t) && !t.IsAbstract && CreateEmailTypeInstance(t).Enabled)
            .ToList();
    }
    
    public async Task<IEmailType?> GetEmailType(Message message)
    {
        if (string.IsNullOrEmpty(message.Body?.Content) || string.IsNullOrEmpty(message.Subject))
        {
            return null;
        }

        string resultText = "";
        
        _logger.LogInformation("Determining email type for MessageId {MessageId}", message.Id);
        foreach (var emailType in _emailTypes)
        {
            var emailTypeInstance = CreateEmailTypeInstance(emailType);

            _logger.LogDebug("Starting {EmailType}.MatchCriteria for MessageId {MessageId}", emailType.Name, message.Id);
            var (matched, result) = await emailTypeInstance.MatchCriteria(message);
            if (matched)
            {
                _logger.LogInformation("Matched {EmailType} for MessageId {MessageId}", emailType.Name, message.Id);
                return emailTypeInstance;
            }
            
            if (result != null)
            {
                resultText += $"<b>{emailType.Name}</b>: {result}<br /><br />";
            }
        }

        if (!string.IsNullOrEmpty(resultText))
        {
            resultText = HelperTools.GenerateHtmlBox(resultText);
        }
        
        var newMessage = await _graphService.CopyMailMessage(_postmottakUpn, message.Id!, _robotLogDestinationId);
        if (newMessage != null)
        {
            newMessage.Body!.Content = $"{resultText}{message.Body!.Content}";
            newMessage.Body!.ContentType = BodyType.Html;

            try
            {
                await _graphService.PatchMailMessage(_postmottakUpn, newMessage.Id!, newMessage);
            }
            catch (Exception)
            {
                _logger.LogWarning("Hahaha. Hit kommer vi aldri 😬");
            }
        }

        _logger.LogInformation("No matching email type found for MessageId {MessageId}", message.Id);
        return null;
    }
    
    public IEmailType CreateEmailTypeInstance(string emailType)
    {
        var type = Type.GetType($"{EmailTypeNamespace}.{emailType}");
        if (type == null)
        {
            throw new InvalidOperationException($"Type {emailType} not found in namespace {EmailTypeNamespace}");
        }

        return CreateEmailTypeInstance(type);
    }
    
    private IEmailType CreateEmailTypeInstance(Type emailType)
    {
        var typeInstance = CreateInstance(emailType);
        if (typeInstance == null)
        {
            throw new InvalidOperationException($"Failed to create instance of type {emailType.Name}");
        }
        
        IEmailType? emailTypeInstance = typeInstance as IEmailType;
        if (emailTypeInstance == null)
        {
            throw new InvalidOperationException($"Type {emailType.Name} does not implement IEmailType");
        }

        return emailTypeInstance;
    }

    private object? CreateInstance(Type emailType)
    {
        try
        {
            return Activator.CreateInstance(emailType, _serviceProvider);
        }
        catch (Exception)
        {
            try
            {
                return Activator.CreateInstance(emailType);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create instance of type {emailType.Name} with and without IServiceProvider param", ex);
            } 
        }
    }
}