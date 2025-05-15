using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;
using postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;
using postmottak_arkivering_dotnet.EmailTypes;
using postmottak_arkivering_dotnet.Services;
using postmottak_arkivering_dotnet.Services.Ai;

namespace postmottak_arkivering_dotnet_tests;

public class EmailTypeTests
{
    private readonly IAiArntIvanService _aiArntIvanService;
    private readonly IAiPluginTestService _aiPluginTestService;
    private readonly IArchiveService _archiveService;
    private readonly IGraphService _graphService;

    private readonly EmailTypeService _emailTypeService;
    
    public EmailTypeTests()
    {
        _aiArntIvanService = Substitute.For<IAiArntIvanService>();
        _aiPluginTestService = Substitute.For<IAiPluginTestService>();
        _archiveService = Substitute.For<IArchiveService>();
        _graphService = Substitute.For<IGraphService>();
        
        var logger = Substitute.For<ILogger<EmailTypeService>>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();
        
        serviceProvider.GetService(typeof(IAiArntIvanService)).Returns(_aiArntIvanService);
        serviceProvider.GetService(typeof(IAiPluginTestService)).Returns(_aiPluginTestService);
        serviceProvider.GetService(typeof(IArchiveService)).Returns(_archiveService);
        serviceProvider.GetService(typeof(IConfiguration)).Returns(configuration);
        serviceProvider.GetService(typeof(IGraphService)).Returns(_graphService);
        
        _emailTypeService = new EmailTypeService(logger, serviceProvider);
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
        Assert.Empty(_graphService.ReceivedCalls());
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
        
        await _aiArntIvanService.DidNotReceive().Ask<FunFactChatResult>(Arg.Any<string>(), Arg.Any<ChatHistory>());
        await _aiArntIvanService.DidNotReceive().Ask<GeneralChatResult>(Arg.Any<string>(), Arg.Any<ChatHistory>());
        await _aiArntIvanService.DidNotReceive().Ask<InnsynChatResult>(Arg.Any<string>(), Arg.Any<ChatHistory>());
        await _aiArntIvanService.DidNotReceive().Ask<LoyvegarantiChatResult>(Arg.Any<string>(), Arg.Any<ChatHistory>());
        await _aiArntIvanService.DidNotReceive().Ask<PluginTestChatResult>(Arg.Any<string>(), Arg.Any<ChatHistory>());
        await _aiArntIvanService.DidNotReceive().Ask<Rf1350ChatResult>(Arg.Any<string>(), Arg.Any<ChatHistory>());
        await _aiArntIvanService.DidNotReceive().FunFact();
        
        Assert.Empty(_aiPluginTestService.ReceivedCalls());
        Assert.Empty(_archiveService.ReceivedCalls());
        Assert.Empty(_graphService.ReceivedCalls());
    }
    
    [Theory]
    [InlineData("Mail angående innsyn i nabokrangel", "Nabokrangel er vedlagt")]
    [InlineData("Mail angående innsyn i super sikkert dokumentet i arkivet", "Gi meg dokument")]
    public async Task GetEmailType_Should_Return_InnsynEmailType(string subject, string body)
    {
        _aiArntIvanService.Ask<InnsynChatResult>(Arg.Any<string>(), Arg.Any<ChatHistory>())
            .Returns(([], new InnsynChatResult
            {
                IsInnsyn = true,
                Description = "Whatever"
            }));
        
        var message = GenerateMessage(subject, body);

        var emailType = await _emailTypeService.GetEmailType(message);
        
        Assert.IsAssignableFrom<InnsynEmailType>(emailType);
        
        await _aiArntIvanService.Received(1).Ask<InnsynChatResult>(body);
        
        await _aiArntIvanService.DidNotReceive().Ask<FunFactChatResult>(Arg.Any<string>(), Arg.Any<ChatHistory>());
        await _aiArntIvanService.DidNotReceive().Ask<GeneralChatResult>(Arg.Any<string>(), Arg.Any<ChatHistory>());
        await _aiArntIvanService.DidNotReceive().Ask<LoyvegarantiChatResult>(Arg.Any<string>(), Arg.Any<ChatHistory>());
        await _aiArntIvanService.DidNotReceive().Ask<PengetransportenChatResult>(Arg.Any<string>(), Arg.Any<ChatHistory>());
        await _aiArntIvanService.DidNotReceive().Ask<PluginTestChatResult>(Arg.Any<string>(), Arg.Any<ChatHistory>());
        await _aiArntIvanService.DidNotReceive().Ask<Rf1350ChatResult>(Arg.Any<string>(), Arg.Any<ChatHistory>());
        await _aiArntIvanService.DidNotReceive().FunFact();
        
        Assert.Empty(_aiPluginTestService.ReceivedCalls());
        Assert.Empty(_archiveService.ReceivedCalls());
        Assert.Empty(_graphService.ReceivedCalls());
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
        
        await _aiArntIvanService.DidNotReceive().Ask<FunFactChatResult>(Arg.Any<string>(), Arg.Any<ChatHistory>());
        await _aiArntIvanService.DidNotReceive().Ask<GeneralChatResult>(Arg.Any<string>(), Arg.Any<ChatHistory>());
        await _aiArntIvanService.DidNotReceive().Ask<InnsynChatResult>(Arg.Any<string>(), Arg.Any<ChatHistory>());
        await _aiArntIvanService.DidNotReceive().Ask<LoyvegarantiChatResult>(Arg.Any<string>(), Arg.Any<ChatHistory>());
        await _aiArntIvanService.DidNotReceive().Ask<PengetransportenChatResult>(Arg.Any<string>(), Arg.Any<ChatHistory>());
        await _aiArntIvanService.DidNotReceive().Ask<PluginTestChatResult>(Arg.Any<string>(), Arg.Any<ChatHistory>());
        await _aiArntIvanService.DidNotReceive().FunFact();
        
        Assert.Empty(_aiPluginTestService.ReceivedCalls());
        Assert.Empty(_archiveService.ReceivedCalls());
        Assert.Empty(_graphService.ReceivedCalls());
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