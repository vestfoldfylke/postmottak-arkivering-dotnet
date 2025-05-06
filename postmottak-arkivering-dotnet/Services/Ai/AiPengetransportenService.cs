using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;
using postmottak_arkivering_dotnet.Utils;

namespace postmottak_arkivering_dotnet.Services.Ai;

public interface IAiPengetransportenService
{
    Task<(ChatHistory, PengetransportenChatResult?)> Ask(string prompt, ChatHistory? chatHistory = null);
}

public class AiPengetransportenService : IAiPengetransportenService
{
    private readonly ChatCompletionAgent _agent;
    
    private readonly Type _agentResponseFormat = typeof(PengetransportenChatResult);
    private const LogLevel AgentLogLevel = LogLevel.Information;
    private const string AgentName = "PengetransportenAgent";
    private const string AgentInstructions = """
                                             Du sier om en epost er enten:
                                             - Faktura
                                             - Spørsmål om faktura
                                             - Regning
                                             - Inkassovarsel
                                             - Purring på faktura eller regning
                                             - Spørsmål om betalinger i fylkeskommunen.

                                             Du skal være minst 90% sikker på at det er en av kategoriene nevnt over før du setter invoiceProperty = true.

                                             Du svarer alltid i json format
                                             """;

    public AiPengetransportenService()
    {
        // get a new kernel builder
        IKernelBuilder kernelBuilder = AiHelper.CreateNewKernelBuilder(AgentLogLevel);
        
        // add services needed by the plugins (if any)
        
        // add needed plugins (if any)
        
        // build a new agent
        _agent = AiHelper.CreateNewAgent(kernelBuilder, AgentName, AgentInstructions, _agentResponseFormat);
    }
    
    public async Task<(ChatHistory, PengetransportenChatResult?)> Ask(string prompt, ChatHistory? chatHistory = null)
    {
        var history = await _agent.InvokeAgent(prompt, chatHistory);
        
        return (history, AiHelper.GetLatestAnswer<PengetransportenChatResult>(history));
    }
}