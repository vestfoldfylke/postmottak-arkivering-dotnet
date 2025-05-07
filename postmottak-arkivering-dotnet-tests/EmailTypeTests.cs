using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph.Models;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;
using postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;
using postmottak_arkivering_dotnet.Contracts.Email.EmailTypes;
using postmottak_arkivering_dotnet.Services;
using postmottak_arkivering_dotnet.Services.Ai;

namespace postmottak_arkivering_dotnet_tests;

public class EmailTypeTests
{
    private readonly IAiArntIvanService _aiArntIvanService;
    private readonly IAiPluginTestService _aiPluginTestService;
    private readonly IArchiveService _archiveService;

    private readonly EmailTypeService _emailTypeService;
    
    public EmailTypeTests()
    {
        _aiArntIvanService = Substitute.For<IAiArntIvanService>();
        _aiPluginTestService = Substitute.For<IAiPluginTestService>();
        _archiveService = Substitute.For<IArchiveService>();
        
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();
        
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IAiArntIvanService)).Returns(_aiArntIvanService);
        serviceProvider.GetService(typeof(IAiPluginTestService)).Returns(_aiPluginTestService);
        serviceProvider.GetService(typeof(IArchiveService)).Returns(_archiveService);
        serviceProvider.GetService(typeof(IConfiguration)).Returns(configuration);
        
        _emailTypeService = new EmailTypeService(serviceProvider);
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
        
        Assert.Empty(_aiArntIvanService.ReceivedCalls());
        Assert.Empty(_aiPluginTestService.ReceivedCalls());
        Assert.Empty(_archiveService.ReceivedCalls());
    }
    
    [Theory]
    [InlineData("Mail angående debetnota", "Debetnota er vedlagt")]
    [InlineData("Jeg har fått et inkassobrev", "Inkassovarsel er sendt i posten")]
    [InlineData("Faktura for betaling", "Faktura må betales snarest")]
    public async Task GetEmailType_Should_Return_PengetransportenEmailType(string subject, string body)
    {
        _aiArntIvanService.Ask<PengetransportenChatResult>(Arg.Any<string>(), Arg.Any<ChatHistory>())
            .Returns(([], new PengetransportenChatResult
            {
                IsInvoiceRelated = true,
                Description = "Whatever"
            }));
        
        var message = GenerateMessage(subject, body);

        var emailType = await _emailTypeService.GetEmailType(message);
        
        Assert.IsAssignableFrom<PengetransportenEmailType>(emailType);
        
        await _aiArntIvanService.Received(1).Ask<PengetransportenChatResult>(body);
        
        Assert.Empty(_aiPluginTestService.ReceivedCalls());
        Assert.Empty(_archiveService.ReceivedCalls());
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
        _aiArntIvanService.Ask<Rf1350ChatResult>(Arg.Any<string>(), Arg.Any<ChatHistory>())
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
        
        await _aiArntIvanService.Received(1).Ask<Rf1350ChatResult>(message.Body!.Content!);

        var pengetransportenCallCount = subject.Contains("kvittering") ? 1 : 0;
        await _aiArntIvanService.Received(pengetransportenCallCount).Ask<PengetransportenChatResult>(message.Body!.Content!);
        
        Assert.Empty(_aiPluginTestService.ReceivedCalls());
        Assert.Empty(_archiveService.ReceivedCalls());
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