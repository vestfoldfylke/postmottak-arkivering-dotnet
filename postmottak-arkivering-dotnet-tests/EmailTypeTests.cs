using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Graph.Models;
using postmottak_arkivering_dotnet.Contracts.Email;
using postmottak_arkivering_dotnet.Services;

namespace postmottak_arkivering_dotnet_tests;

public class EmailTypeTests
{
    private readonly IAiAgentService _service;
    
    public EmailTypeTests()
    {
        _service = NSubstitute.Substitute.For<IAiAgentService>();
    }
    
    [Theory]
    [InlineData("Søknad om rusmidler", typeof(CaseNumberEmailType))]
    [InlineData("Søknad om skudd", typeof(CaseNumberEmailType))]
    public async Task GetEmailType_Should_Return_Correct_Class_On_Known_Subject(string subject, Type expectedType)
    {
        var message = new Message
        {
            Subject = subject
        };

        var emailType = await EmailType.GetEmailType(message, _service);
        
        Assert.IsAssignableFrom(expectedType, emailType);
    }
    
    [Theory]
    [InlineData("Søknad om sprøyter")]
    public async Task GetEmailType_Should_Return_NULL_On_Unknown_Subject(string subject)
    {
        var message = new Message
        {
            Subject = subject
        };

        var emailType = await EmailType.GetEmailType(message, _service);
        
        Assert.Null(emailType);
    }
    
    [Theory]
    [InlineData("@regionaltullball.no", "RF 13.50", typeof(Rf1350EmailType))]
    [InlineData("@regionalTullball.no", "RF 13.50", typeof(Rf1350EmailType))]
    public async Task GetEmailType_Should_Return_Correct_Class_On_Known_Sender(string sender, string subject, Type expectedType)
    {
        var message = new Message
        {
            From = new Recipient
            {
                EmailAddress = new EmailAddress
                {
                    Address = sender
                }
            },
            Subject = subject
        };

        var emailType = await EmailType.GetEmailType(message, _service);
        
        Assert.IsAssignableFrom(expectedType, emailType);
    }
    
    [Theory]
    [InlineData("@example.com", "RF 13.50")]
    [InlineData(null, "RF 13.50")]
    [InlineData("@example.com", null)]
    [InlineData(null, null)]
    public async Task GetEmailType_Should_Return_NULL_On_Unknown_Sender_Or_Subject(string sender, string subject)
    {
        var message = new Message
        {
            From = new Recipient
            {
                EmailAddress = new EmailAddress
                {
                    Address = sender
                }
            },
            Subject = subject
        };

        var emailType = await EmailType.GetEmailType(message, _service);
        
        Assert.Null(emailType);
    }
}