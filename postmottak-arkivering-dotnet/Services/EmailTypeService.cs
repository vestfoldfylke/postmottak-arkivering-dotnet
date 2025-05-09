using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Graph.Models;
using postmottak_arkivering_dotnet.Contracts.Email;

namespace postmottak_arkivering_dotnet.Services;

public interface IEmailTypeService
{
    IEmailType CreateEmailTypeInstance(string emailType);
    Task<IEmailType?> GetEmailType(Message message);
}

public class EmailTypeService : IEmailTypeService
{
    private readonly IServiceProvider _serviceProvider;

    private readonly List<Type> _emailTypes;
    
    private const string EmailTypeNamespace = "postmottak_arkivering_dotnet.Contracts.Email.EmailTypes";
    
    public EmailTypeService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        _emailTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(IEmailType).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();
    }
    
    public async Task<IEmailType?> GetEmailType(Message message)
    {
        if (string.IsNullOrEmpty(message.Body?.Content) || string.IsNullOrEmpty(message.Subject))
        {
            return null;
        }
        
        foreach (var emailType in _emailTypes)
        {
            var emailTypeInstance = CreateEmailTypeInstance(emailType);

            if (await emailTypeInstance.MatchCriteria(message))
            {
                return emailTypeInstance;
            }
        }

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