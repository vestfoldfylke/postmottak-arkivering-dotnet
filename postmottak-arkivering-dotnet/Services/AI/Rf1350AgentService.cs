using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using postmottak_arkivering_dotnet.Contracts.Ai;
using ChatMessageContent = Microsoft.SemanticKernel.ChatMessageContent;

namespace postmottak_arkivering_dotnet.Services.AI;

public interface IRf1350AgentService
{
    Task<ChatHistory> Ask(string prompt, ChatHistory? chatHistory = null);
}

public class Rf1350AgentService : IRf1350AgentService
{
    private readonly ILogger<Rf1350AgentService> _logger;
    
    private readonly ChatCompletionAgent _agent;

    public Rf1350AgentService(IConfiguration config, ILogger<Rf1350AgentService> logger)
    {
        _logger = logger;

        var modelId = config["OpenAI_Model_Id"] ?? throw new NullReferenceException("OpenAI_Model_Id missing in configuration");
        var openAiKey = config["OpenAI_API_Key"] ?? throw new NullReferenceException("OpenAI_API_Key missing in configuration");
        
        var kernelBuilder = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(modelId, openAiKey);

        kernelBuilder.Services.AddLogging(configure =>
        {
            configure
                .AddConsole()
                .SetMinimumLevel(LogLevel.Information);
        });
        
        var kernel = kernelBuilder.Build();
        
        var openAiPromptExecutionSettings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            Store = false,
            ResponseFormat = typeof(Rf1350ChatResult)
        };

        _agent = new ChatCompletionAgent
        {
            Name = "RF13-50",
            Instructions = """
                           Du er en arkiveringsekspert. Du har stålkontroll på arkivering av dokumenter og e-poster.
                                                        Svarene skal alltid være i JSON format,
                                                        og du skal alltid være minst 90% sikker før du setter en verdi.
                                                        Et organisasjonsnummer er 9 siffer langt og kan inneholde mellomrom.
                                                        Et referansenummer ser slikt ut: 0000-0000.
                                                        Et prosjektnummer ser slik ut: 00-0000.
                           """,
            Kernel = kernel,
            Arguments = new KernelArguments(openAiPromptExecutionSettings)
        };
    }
    
    public async Task<ChatHistory> Ask(string prompt, ChatHistory? chatHistory = null)
    {
        _logger.LogInformation("RF13.50 question: {Prompt}", prompt);
        
        var history = chatHistory ?? [];

        AgentThread agentThread = new ChatHistoryAgentThread(history);

        await foreach (ChatMessageContent response in _agent.InvokeAsync(new ChatMessageContent(AuthorRole.User, prompt),
                           agentThread))
        {
            var resultContent = response.Content ?? string.Empty;
           
            _logger.LogInformation("RF13.50 answer: {Result}", resultContent);
        }
        
        return history;
    }
}