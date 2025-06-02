using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;
using postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;
using postmottak_arkivering_dotnet.EmailTypes;
using postmottak_arkivering_dotnet.Services;
using postmottak_arkivering_dotnet.Services.Ai;
using Vestfold.Extensions.Archive.Services;

namespace postmottak_arkivering_dotnet_tests;

public class EmailTypeTests
{
    private readonly IAiArntIvanService _aiArntIvanService;
    private readonly IAiPluginTestService _aiPluginTestService;
    private readonly IArchiveService _archiveService;
    private readonly IGraphService _graphService;
    private readonly IServiceProvider _serviceProvider;

    private readonly EmailTypeService _emailTypeService;
    
    public EmailTypeTests()
    {
        _aiArntIvanService = Substitute.For<IAiArntIvanService>();
        _aiPluginTestService = Substitute.For<IAiPluginTestService>();
        _archiveService = Substitute.For<IArchiveService>();
        _graphService = Substitute.For<IGraphService>();
        _serviceProvider = Substitute.For<IServiceProvider>();
        
        var logger = Substitute.For<ILogger<EmailTypeService>>();
        
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();
        
        _serviceProvider.GetService(typeof(IAiArntIvanService)).Returns(_aiArntIvanService);
        _serviceProvider.GetService(typeof(IAiPluginTestService)).Returns(_aiPluginTestService);
        _serviceProvider.GetService(typeof(IArchiveService)).Returns(_archiveService);
        _serviceProvider.GetService(typeof(IConfiguration)).Returns(configuration);
        _serviceProvider.GetService(typeof(IGraphService)).Returns(_graphService);
        
        _emailTypeService = new EmailTypeService(logger, _serviceProvider);
    }
    
    [Fact]
    public async Task GetEmailType_Should_Return_Null_When_Message_Has_Empty_Body_AndOr_Subject()
    {
        var messageWithoutBody = GenerateMessage();
        messageWithoutBody.Body = null;
        
        var messageWithoutSubject = GenerateMessage();
        messageWithoutSubject.Subject = null;

        var (emailTypeBody, unknownMessageBody) = await _emailTypeService.GetEmailType(messageWithoutBody);
        
        Assert.Null(emailTypeBody);
        Assert.Null(unknownMessageBody);
        
        var (emailTypeSubject, unknownMessageSubject) = await _emailTypeService.GetEmailType(messageWithoutSubject);
        
        Assert.Null(emailTypeSubject);
        Assert.Null(unknownMessageSubject);
        
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
        if (!new PengetransportenEmailType(_serviceProvider).Enabled)
        {
            Assert.True(true);
            return;
        }
        
        _aiArntIvanService.Ask<PengetransportenChatResult>(Arg.Any<string>(), Arg.Any<ChatHistory>())
            .Returns(([], new PengetransportenChatResult
            {
                IsInvoiceRelated = true,
                Description = "Whatever"
            }));
        
        var message = GenerateMessage(subject, body);
        
        var (emailType, unknownMessage) = await _emailTypeService.GetEmailType(message);
        
        Assert.IsAssignableFrom<PengetransportenEmailType>(emailType);
        Assert.Null(unknownMessage);
        
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
        if (!new InnsynEmailType(_serviceProvider).Enabled)
        {
            Assert.True(true);
            return;
        }
        
        _aiArntIvanService.Ask<InnsynChatResult>(Arg.Any<string>(), Arg.Any<ChatHistory>())
            .Returns(([], new InnsynChatResult
            {
                IsInnsyn = true,
                Description = "Whatever"
            }));
        
        var message = GenerateMessage(subject, body);
        
        var (emailType, unknownMessage) = await _emailTypeService.GetEmailType(message);
        
        Assert.IsAssignableFrom<InnsynEmailType>(emailType);
        Assert.Null(unknownMessage);
        
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
        if (!new Rf1350EmailType(_serviceProvider).Enabled)
        {
            Assert.True(true);
            return;
        }
        
        _aiArntIvanService.Ask<Rf1350ChatResult>(Arg.Any<string>(), Arg.Any<ChatHistory>())
            .Returns(([], new Rf1350ChatResult
            {
                ProjectNumber = projectNumber,
                ReferenceNumber = referenceNumber,
                Type = "RF13.50"
            }));

        const string fromAddress = "ikkesvar@regionalforvaltning.no";
        
        var message = GenerateMessage(subject, fromAddress: fromAddress);
        
        var (emailType, unknownMessage) = await _emailTypeService.GetEmailType(message);
        
        Assert.IsAssignableFrom<Rf1350EmailType>(emailType);
        Assert.Null(unknownMessage);
        
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