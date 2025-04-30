using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph.Models;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;
using postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;
using postmottak_arkivering_dotnet.Contracts.Email.EmailTypes;
using postmottak_arkivering_dotnet.Services;

namespace postmottak_arkivering_dotnet_tests;

public class EmailTypeTests
{
    private readonly IAiAgentService _aiAgentService;
    private readonly IArchiveService _archiveService;
    private readonly IConfiguration _configuration;
    
    private readonly IServiceProvider _serviceProvider;
    
    private readonly EmailTypeService _emailTypeService;
    
    public EmailTypeTests()
    {
        _aiAgentService = Substitute.For<IAiAgentService>();
        _archiveService = Substitute.For<IArchiveService>();
        
        _configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();
        
        _serviceProvider = Substitute.For<IServiceProvider>();
        _serviceProvider.GetService(typeof(IAiAgentService)).Returns(_aiAgentService);
        _serviceProvider.GetService(typeof(IArchiveService)).Returns(_archiveService);
        _serviceProvider.GetService(typeof(IConfiguration)).Returns(_configuration);
        
        _emailTypeService = new EmailTypeService(_serviceProvider);
    }
    
    [Fact]
    public async Task GetEmailType_Should_Return_Null_When_Message_Has_Empty_Body_AndOr_Subject()
    {
        var messageWithoutBody = GenerateMessage();
        messageWithoutBody.Body = null;
        
        var messageWithoutSubject = GenerateMessage();
        messageWithoutSubject.Subject = null;

        var missingBody = await _emailTypeService.GetEmailType(messageWithoutBody);
        
        Assert.Null(missingBody);
        
        var missingSubject = await _emailTypeService.GetEmailType(messageWithoutSubject);
        
        Assert.Null(missingSubject);
    }
    
    [Theory]
    [InlineData("Søknad om rusmidler")]
    [InlineData("Søknad om skudd")]
    public async Task GetEmailType_Should_Return_CaseNumberEmailType(string subject)
    {
        var message = GenerateMessage(subject);

        var emailType = await _emailTypeService.GetEmailType(message);
        
        Assert.IsAssignableFrom<CaseNumberEmailType>(emailType);
    }
    
    [Theory]
    [InlineData("Mail angående debetnota", "Debetnota er vedlagt")]
    [InlineData("Jeg har fått et inkassobrev", "Inkassovarsel er sendt i posten")]
    [InlineData("Faktura for betaling", "Faktura må betales snarest")]
    public async Task GetEmailType_Should_Return_PengetransportenEmailType(string subject, string body)
    {
        _aiAgentService.Pengetransporten(Arg.Any<string>(), Arg.Any<ChatHistory>())
            .Returns(([], new PengetransportenChatResult
            {
                IsInvoiceRelated = true,
                Description = "Whatever",
                Attachments = []
            }));
        
        var message = GenerateMessage(subject, body);

        var emailType = await _emailTypeService.GetEmailType(message);
        
        Assert.IsAssignableFrom<PengetransportenEmailType>(emailType);
    }
    
    [Theory]
    [InlineData("RF13.50 - Automatisk kvittering på innsendt søknad", "00-1", "0000-0000")]
    [InlineData("RF13.50 - Automatisk kvittering på innsendt søknad", "00-12", "0000-0000")]
    [InlineData("RF13.50 - Automatisk kvittering på innsendt søknad", "00-123", "0000-0000")]
    [InlineData("RF13.50 - Automatisk kvittering på innsendt søknad", "00-1234", "0000-0000")]
    [InlineData("RF13.50 - Automatisk kvittering på innsendt søknad", "00-12345", "0000-0000")]
    [InlineData("RF13.50 - Automatisk kvittering på innsendt søknad", "00-123456", "0000-0000")]
    [InlineData("RF13.50 - Automatisk epost til arkiv", "00-1", "0000-0000")]
    [InlineData("RF13.50 - Automatisk epost til arkiv", "00-12", "0000-0000")]
    [InlineData("RF13.50 - Automatisk epost til arkiv", "00-123", "0000-0000")]
    [InlineData("RF13.50 - Automatisk epost til arkiv", "00-1234", "0000-0000")]
    [InlineData("RF13.50 - Automatisk epost til arkiv", "00-12345", "0000-0000")]
    [InlineData("RF13.50 - Automatisk epost til arkiv", "00-123456", "0000-0000")]
    public async Task GetEmailType_Should_Return_Rf1350EmailType(string subject, string projectNumber, string referenceNumber)
    {
        _aiAgentService.Rf1350(Arg.Any<string>(), Arg.Any<ChatHistory>())
            .Returns(([], new Rf1350ChatResult
            {
                ProjectNumber = projectNumber,
                ReferenceNumber = referenceNumber,
                Type = "RF13.50"
            }));

        const string fromAddress = "ikkesvar@regionalforvaltning.no";
        
        var message = GenerateMessage(subject, fromAddress: fromAddress);

        var emailType = await _emailTypeService.GetEmailType(message);
        
        Assert.IsAssignableFrom<Rf1350EmailType>(emailType);
    }

    private static Message GenerateMessage(string? subject = null, string? body = null, string? fromAddress = null) =>
        new Message
        {
            Body = new ItemBody
            {
                Content = body ?? "Mailen må ha en body"
            },
            Subject = subject ?? "Mailen må ha et subject",
            From = new Recipient
            {
                EmailAddress = new EmailAddress
                {
                    Address = fromAddress ?? "whatever@whoever.no"
                }
            }
        };
}