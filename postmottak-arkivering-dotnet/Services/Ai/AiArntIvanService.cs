using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;
using postmottak_arkivering_dotnet.Utils;

namespace postmottak_arkivering_dotnet.Services.Ai;

public interface IAiArntIvanService
{
    Task<(ChatHistory, T?)> Ask<T>(string prompt, ChatHistory? chatHistory = null);
    Task<string> FunFact();
}

public class AiArntIvanService : IAiArntIvanService
{
    private readonly Dictionary<string, ChatCompletionAgent> _agents = [];
    
    private const LogLevel AgentLogLevel = LogLevel.Information;
    private const string AgentName = "ArntIvanAgent";
    private const string AgentInstructions = """
                                             Du jobber med arkivering og uthenting av relevante data fra en epost.
                                             Du responderer alltid i json format.
                                             
                                             Dersom du ikke finner en sannsynlig verdi for en property, setter du den til null
                                             """;
    
    public async Task<(ChatHistory, T?)> Ask<T>(string prompt, ChatHistory? chatHistory = null)
    {
        ChatCompletionAgent agent = GetOrCreateAgent<T>();
        
        var history = await agent.InvokeAgent(prompt, typeof(T).Name, chatHistory);
        
        return (history, AiHelper.GetLatestAnswer<T>(history));
    }

    public async Task<string> FunFact()
    {
        ChatCompletionAgent agent = GetOrCreateAgent<FunFactChatResult>();
        
        var history = await agent.InvokeAgent("Gi meg en fun fact", nameof(FunFactChatResult));
        
        return AiHelper.GetLatestAnswer<FunFactChatResult>(history)?.Message ?? string.Empty;
    }

    private ChatCompletionAgent GetOrCreateAgent<T>()
    {
        string typeName = typeof(T).Name;
        
        if (_agents.TryGetValue(typeName, out var agent))
        {
            return agent;
        }
        
        // get a new kernel builder
        IKernelBuilder kernelBuilder = AiHelper.CreateNewKernelBuilder(AgentLogLevel);
        
        // build a new agent
        agent = AiHelper.CreateNewAgent(kernelBuilder, AgentName, AgentInstructions, typeof(T));
        _agents.Add(typeName, agent);
        
        return agent;
    }
}