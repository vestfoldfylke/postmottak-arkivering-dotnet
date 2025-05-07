using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Serilog;

namespace postmottak_arkivering_dotnet.Utils;

internal static class AiHelper
{
    internal static IConfigurationManager? ConfigurationManager { get; set; }

    internal static IKernelBuilder CreateNewKernelBuilder(LogLevel logLevel)
    {
        if (ConfigurationManager is null)
        {
            throw new NullReferenceException("ConfigurationManager is not set");
        }
        
        var azureOpenAiModelName = ConfigurationManager["AzureOpenAI_Model_Name"] ?? throw new NullReferenceException("AzureOpenAI_Model_Name missing in configuration");
        var azureOpenAiKey = ConfigurationManager["AzureOpenAI_API_Key"] ?? throw new NullReferenceException("AzureOpenAI_API_Key missing in configuration");
        var azureOpenAiEndpoint = ConfigurationManager["AzureOpenAI_Endpoint"] ?? throw new NullReferenceException("AzureOpenAI_Endpoint missing in configuration");
        
        var kernelBuilder = Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion(azureOpenAiModelName, azureOpenAiEndpoint, azureOpenAiKey);

        kernelBuilder.Services.AddLogging(configure =>
        {
            configure
                .AddConsole()
                .SetMinimumLevel(logLevel);
        });
        
        return kernelBuilder;
    }

    internal static ChatCompletionAgent CreateNewAgent(IKernelBuilder kernelBuilder, string agentName, string agentInstructions, Type responseFormat)
    {
        Kernel kernel = kernelBuilder.Build();
        
        var promptExecutionSettings = new AzureOpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            Store = false,
            ResponseFormat = responseFormat,
        };
        
        return new ChatCompletionAgent
        {
            Name = agentName,
            Instructions = agentInstructions,
            Kernel = kernel,
            Arguments = new KernelArguments(promptExecutionSettings)
        };
    }
    
    internal static T? GetLatestAnswer<T>(ChatHistory chatHistory)
    {
        // 0 will be the user input. 1 will be the AI response, and so on...
        if (chatHistory.Count < 2)
        {
            return default;
        }
        
        var content = chatHistory[^1].Content;
        if (string.IsNullOrEmpty(content))
        {
            return default;
        }
        
        return JsonSerializer.Deserialize<T>(content) ?? throw new InvalidOperationException($"Failed to deserialize AI response into type {typeof(T).Name}");
    }
    
    internal static async Task<ChatHistory> InvokeAgent(this ChatCompletionAgent agent, string prompt, string responseType, ChatHistory? chatHistory = null)
    {
        Log.Logger.Debug("{AgentName} of type {ResponseType} with question: {Prompt}", agent.Name, responseType, prompt);
        
        var history = chatHistory ?? [];

        AgentThread agentThread = new ChatHistoryAgentThread(history);

        await foreach (ChatMessageContent response in agent.InvokeAsync(new ChatMessageContent(AuthorRole.User, prompt),
                           agentThread))
        {
            var resultContent = response.Content ?? string.Empty;
           
            Log.Logger.Debug("{AgentName} of type {ResponseType} with answer: {Result}", agent.Name, responseType, resultContent);
        }
        
        return history;
    }
}