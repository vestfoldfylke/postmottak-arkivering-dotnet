using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;
using postmottak_arkivering_dotnet.Contracts.Email;
using postmottak_arkivering_dotnet.Utils;
using Vestfold.Extensions.Metrics.Services;

namespace postmottak_arkivering_dotnet.Services;

public interface IEmailTypeService
{
    IEmailType CreateEmailTypeInstance(string emailType);
    Task<(IEmailType?, UnknownMessage?)> GetEmailType(Message message);
}

public class EmailTypeService : IEmailTypeService
{
    private readonly ILogger<EmailTypeService> _logger;
    private readonly IMetricsService _metricsService;
    private readonly IServiceProvider _serviceProvider;

    private readonly List<Type> _emailTypes;
    
    private const string EmailTypeNamespace = "postmottak_arkivering_dotnet.EmailTypes";
    
    public EmailTypeService(ILogger<EmailTypeService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

        _metricsService = serviceProvider.GetRequiredService<IMetricsService>();

        _emailTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(IEmailType).IsAssignableFrom(t) && !t.IsAbstract && CreateEmailTypeInstance(t).Enabled)
            .ToList();
    }
    
    public async Task<(IEmailType?, UnknownMessage?)> GetEmailType(Message message)
    {
        if (string.IsNullOrEmpty(message.Body?.Content) || string.IsNullOrEmpty(message.Subject))
        {
            _logger.LogInformation("Cant determine email type, since body and/or subject is empty, for MessageId {MessageId}.", message.Id);
            return (null, new UnknownMessage
            {
                Message = message,
                Result = HelperTools.GenerateHtmlBox("E-posten mangler innhold og/eller emne er tomt"),
                PartialMatch = false
            });
        }

        var partialText = "";
        var noMatchText = "";
        
        _logger.LogInformation("Determining email type for MessageId {MessageId}", message.Id);
        foreach (var emailType in _emailTypes)
        {
            var emailTypeInstance = CreateEmailTypeInstance(emailType);

            _logger.LogDebug("Starting {EmailType}.MatchCriteria for MessageId {MessageId}", emailType.Name, message.Id);
            var matchResult = await emailTypeInstance.MatchCriteria(message);
            if (matchResult.Matched == EmailTypeMatched.Yes)
            {
                _logger.LogInformation("Matched {EmailType} for MessageId {MessageId}", emailType.Name, message.Id);
                _metricsService.Count($"{Constants.MetricsPrefix}_GetEmailType", "Determine which email type to use", ("EmailType", emailType.Name), ("Result", "Yes"));
                return (emailTypeInstance, null);
            }
            
            if (matchResult.Matched == EmailTypeMatched.Maybe)
            {
                _logger.LogDebug("Partially matched {EmailType} for MessageId {MessageId}", emailType.Name, message.Id);
                _metricsService.Count($"{Constants.MetricsPrefix}_GetEmailType", "Determine which email type to use", ("EmailType", emailType.Name), ("Result", "Maybe"));
                if (matchResult.Result != null)
                {
                    partialText += $"<b>{emailType.Name}</b>: {matchResult.Result}<br /><br />";
                }
                continue;
            }
            
            _logger.LogDebug("Not matched {EmailType} for MessageId {MessageId}", emailType.Name, message.Id);
            if (matchResult.Result != null)
            {
                noMatchText += $"<b>{emailType.Name}</b>: {matchResult.Result}<br /><br />";
            }
        }
        
        _logger.LogInformation("No matching email type found for MessageId {MessageId}", message.Id);
        if (!string.IsNullOrEmpty(partialText))
        {
            return (null, new UnknownMessage
            {
                PartialMatch = true,
                Message = message,
                Result = HelperTools.GenerateHtmlBox(partialText)
            });
        }
        
        return (null, new UnknownMessage
        {
            PartialMatch = false,
            Message = message,
            Result = !string.IsNullOrEmpty(noMatchText) ? HelperTools.GenerateHtmlBox(noMatchText) : "Ingen data"
        });
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