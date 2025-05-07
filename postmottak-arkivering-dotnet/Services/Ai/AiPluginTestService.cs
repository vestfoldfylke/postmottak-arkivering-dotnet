using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;
using postmottak_arkivering_dotnet.Plugins.Ai;
using postmottak_arkivering_dotnet.Utils;

namespace postmottak_arkivering_dotnet.Services.Ai;

public interface IAiPluginTestService
{
    Task<(ChatHistory, PluginTestChatResult?)> Ask(string prompt, ChatHistory? chatHistory = null);
}

public class AiPluginTestService : IAiPluginTestService
{
    private readonly ChatCompletionAgent _agent;
    
    private readonly Type _agentResponseFormat = typeof(PluginTestChatResult);
    private const LogLevel AgentLogLevel = LogLevel.Information;
    private const string AgentName = "PluginTestAgent";
    private const string AgentInstructions = "Du er så snill! Alt er blomster og bier og bare velstand! Du skal alltid gi tilbake resultatet i JSON format";

    public AiPluginTestService(IConfiguration config, IArchiveService archiveService, IAuthenticationService authService)
    {
        // get a new kernel builder
        IKernelBuilder kernelBuilder = AiHelper.CreateNewKernelBuilder(AgentLogLevel);
        
        // add services needed by the plugins (if any)
        kernelBuilder.Services.AddSingleton(config);
        kernelBuilder.Services.AddSingleton(authService);
        kernelBuilder.Services.AddSingleton(archiveService);
        
        // add needed plugins (if any)
        kernelBuilder.Plugins.AddFromType<ArchivePlugin>();
        kernelBuilder.Plugins.AddFromType<TimePlugin>();
        
        // build a new agent
        _agent = AiHelper.CreateNewAgent(kernelBuilder, AgentName, AgentInstructions, _agentResponseFormat);
    }
    
    public async Task<(ChatHistory, PluginTestChatResult?)> Ask(string prompt, ChatHistory? chatHistory = null)
    {
        var history = await _agent.InvokeAgent(prompt, nameof(PluginTestChatResult), chatHistory);
        
        return (history, AiHelper.GetLatestAnswer<PluginTestChatResult>(history));
    }
}