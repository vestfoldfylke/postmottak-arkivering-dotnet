using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using OpenAI.Chat;
using Serilog;
using Serilog.Context;
using ChatMessageContent = Microsoft.SemanticKernel.ChatMessageContent;

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
        
        var azureOpenAiModelName = ConfigurationManager["AZURE_OPENAI_MODEL_NAME"] ?? throw new NullReferenceException();
        var azureOpenAiKey = ConfigurationManager["AZURE_OPENAI_API_KEY"] ?? throw new NullReferenceException();
        var azureOpenAiEndpoint = ConfigurationManager["AZURE_OPENAI_ENDPOINT"] ?? throw new NullReferenceException();

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
        if (ConfigurationManager is null)
        {
            throw new NullReferenceException("ConfigurationManager is not set");
        }
        
        int maxCompletionTokens = int.TryParse(ConfigurationManager["AZURE_OPENAI_MAX_COMPLETION_TOKENS"], out int maxTokens)
            ? maxTokens
            : 10000;
        
        Kernel kernel = kernelBuilder.Build();
        
        var promptExecutionSettings = new AzureOpenAIPromptExecutionSettings
        {
            MaxTokens = maxCompletionTokens,
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
    
    [SuppressMessage("ReSharper", "StructuredMessageTemplateProblem")]
    internal static async Task<ChatHistory> InvokeAgent(this ChatCompletionAgent agent, string prompt, string responseType, ChatHistory? chatHistory = null)
    {
        using (GlobalLogContext.PushProperty("AgentName", agent.Name))
        using (GlobalLogContext.PushProperty("ResponseType", responseType))
        {
            Log.Logger.Information("Asking {AgentName} for response type {ResponseType}");
            Log.Logger.Debug("{Prompt}", prompt);

            var history = chatHistory ?? [];

            AgentThread agentThread = new ChatHistoryAgentThread(history);

            await foreach (ChatMessageContent response in agent.InvokeAsync(
                               new ChatMessageContent(AuthorRole.User, prompt),
                               agentThread))
            {
                var resultContent = response.Content ?? string.Empty;

                ChatTokenUsage? usage = (ChatTokenUsage?)response.Metadata?["Usage"];
                Log.Logger.Information("Got {ResponseType} response from {AgentName}. InputTokenCount: {InputTokenCount}. OutputTokenCount: {OutputTokenCount}",
                    responseType, agent.Name, usage?.InputTokenCount, usage?.OutputTokenCount);
                Log.Logger.Debug("{Result}", resultContent);
            }

            return history;
        }
    }
}