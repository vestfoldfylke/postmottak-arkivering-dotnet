using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
//using Microsoft.SemanticKernel.Connectors.OpenAI;
using postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;
using postmottak_arkivering_dotnet.Utils;
using ChatMessageContent = Microsoft.SemanticKernel.ChatMessageContent;

namespace postmottak_arkivering_dotnet.Services;

public interface IAiAgentService
{
    Task<(ChatHistory, PengetransportenChatResult?)> Pengetransporten(string prompt, ChatHistory? chatHistory = null);
    Task<(ChatHistory, Rf1350ChatResult?)> Rf1350(string prompt, ChatHistory? chatHistory = null);
}

public class AiAgentService : IAiAgentService
{
    private readonly ILogger<AiAgentService> _logger;

    private readonly Kernel _kernel;
    private readonly Dictionary<string, ChatCompletionAgent> _agents = new();
    
    #region AgentNames
    private const string Rf1350AgentName = "RF1350";
    private const string PengetransportenAgentName = "Pengetransporten";
    #endregion
    
    #region Instructions
    private const string Rf1350Instructions = """
                                               Du er en arkiveringsekspert. Du har stålkontroll på arkivering av dokumenter og e-poster.
                                                                            Svarene skal alltid være i JSON format,
                                                                            og du skal alltid være minst 90% sikker før du setter en verdi.
                                                                            Type propertien skal alltid være en av følgende og du må selv finne ut hvilken som stemmer ut fra input:
                                                                            - 'Anmodning om Sluttutbetaling'
                                                                            - 'Automatisk kvittering på innsendt søknad'
                                                                            - 'Overføring av mottatt søknad'
                                                                            Et organisasjonsnummer er 9 siffer langt og kan inneholde mellomrom.
                                                                            Et referansenummer ser slikt ut: 0000-0000.
                                                                            Et prosjektnummer ser slik ut: 00-0000.
                                               """;
    
    private const string PengetransportenInstructions = """
                                                        Du sier om en epost er enten:
                                                        - Faktura
                                                        - Spørsmål om faktura
                                                        - Regning
                                                        - Inkassovarsel
                                                        - Purring på faktura eller regning
                                                        - Spørsmål om betalinger i fylkeskommunen.
                                                        
                                                        Du skal være minst 90% sikker på at det er en av kategoriene nevnt over før du setter invoiceProperty = true.
                                                        
                                                        Du svarer alltid i json format. Det skal også ALLTID inneholde en property hvor det er en ny Chuck Norris vits
                                                        """;
    #endregion
    
    #region ResponseFormats
    private readonly Type _pengetransportenResponseFormat = typeof(PengetransportenChatResult);
    private readonly Type _rf1350ResponseFormat = typeof(Rf1350ChatResult);
    #endregion

    public AiAgentService(IConfiguration config, ILogger<AiAgentService> logger)
    {
        _logger = logger;

        // var modelId = config["OpenAI_Model_Id"] ?? throw new NullReferenceException("OpenAI_Model_Id missing in configuration");
        // var openAiKey = config["OpenAI_API_Key"] ?? throw new NullReferenceException("OpenAI_API_Key missing in configuration");
        var azureOpenAiModelName = config["AzureOpenAI_Model_Name"] ?? throw new NullReferenceException("AzureOpenAI_Model_Name missing in configuration");
        var azureOpenAiKey = config["AzureOpenAI_API_Key"] ?? throw new NullReferenceException("AzureOpenAI_API_Key missing in configuration");
        var azureOpenAiEndpoint = config["AzureOpenAI_Endpoint"] ?? throw new NullReferenceException("AzureOpenAI_Endpoint missing in configuration");
        
        /* For OpenAI 
        var kernelBuilder = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(modelId, openAiKey);
        */

        var kernelBuilder = Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion(azureOpenAiModelName, azureOpenAiEndpoint, azureOpenAiKey);

        kernelBuilder.Services.AddLogging(configure =>
        {
            configure
                .AddConsole()
                .SetMinimumLevel(LogLevel.Information);
        });
        
        _kernel = kernelBuilder.Build();
    }
    
    public async Task<(ChatHistory, PengetransportenChatResult?)> Pengetransporten(string prompt, ChatHistory? chatHistory = null)
    {
        _logger.LogInformation("{AgentName} question: {Prompt}", PengetransportenAgentName, prompt);
        
        var history = chatHistory ?? [];

        AgentThread agentThread = new ChatHistoryAgentThread(history);
        
        var agent = GetOrCreateChatCompletionAgent(PengetransportenAgentName, _pengetransportenResponseFormat, PengetransportenInstructions);

        await foreach (ChatMessageContent response in agent.InvokeAsync(new ChatMessageContent(AuthorRole.User, prompt),
                           agentThread))
        {
            var resultContent = response.Content ?? string.Empty;
           
            _logger.LogInformation("{AgentName} answer: {Result}", PengetransportenAgentName, resultContent);
        }
        
        return (history, AiHelper.GetLatestAnswer<PengetransportenChatResult>(history));
    }
    
    public async Task<(ChatHistory, Rf1350ChatResult?)> Rf1350(string prompt, ChatHistory? chatHistory = null)
    {
        _logger.LogInformation("{AgentName} question: {Prompt}", Rf1350AgentName, prompt);
        
        var history = chatHistory ?? [];

        AgentThread agentThread = new ChatHistoryAgentThread(history);
        
        var agent = GetOrCreateChatCompletionAgent(Rf1350AgentName, _rf1350ResponseFormat, Rf1350Instructions);

        await foreach (ChatMessageContent response in agent.InvokeAsync(new ChatMessageContent(AuthorRole.User, prompt),
                           agentThread))
        {
            var resultContent = response.Content ?? string.Empty;
           
            _logger.LogInformation("{AgentName} answer: {Result}", Rf1350AgentName, resultContent);
        }
        
        return (history, AiHelper.GetLatestAnswer<Rf1350ChatResult>(history));
    }

    private ChatCompletionAgent GetOrCreateChatCompletionAgent(string agentName, Type responseFormat, string instructions)
    {
        if (_agents.TryGetValue(agentName, out var agent))
        {
            _logger.LogInformation("Agent {AgentName} already exists with ExecutionSettings: {ExecutionSettings}", agentName, agent.Arguments.ExecutionSettings);
            return agent;
        }

        /*        
        var openAiPromptExecutionSettings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            Store = false,
            ResponseFormat = responseFormat
        };
        */

        var azureOpenAiPromptExecutionSettings = new AzureOpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            Store = false,
            ResponseFormat = responseFormat,
        };
        
        agent = new ChatCompletionAgent
        {
            Name = agentName,
            Instructions = instructions,
            Kernel = _kernel,
            Arguments = new KernelArguments(azureOpenAiPromptExecutionSettings)
        };
        
        _logger.LogInformation("Agent {AgentName} created with ExecutionSettings: {ExecutionSettings}", agentName, agent.Arguments.ExecutionSettings);
        
        _agents[agentName] = agent;

        return agent;
    }
}