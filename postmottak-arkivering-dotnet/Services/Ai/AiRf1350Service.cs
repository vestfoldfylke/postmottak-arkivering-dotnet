using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using postmottak_arkivering_dotnet.Contracts.Ai.ChatResult;
using postmottak_arkivering_dotnet.Utils;

namespace postmottak_arkivering_dotnet.Services.Ai;

public interface IAiRf1350Service
{
    Task<(ChatHistory, Rf1350ChatResult?)> Ask(string prompt, ChatHistory? chatHistory = null);
}

public class AiRf1350Service : IAiRf1350Service
{
    private readonly ILogger<AiRf1350Service> _logger;
    
    private readonly ChatCompletionAgent _agent;
    
    private readonly Type _agentResponseFormat = typeof(Rf1350ChatResult);
    private const LogLevel AgentLogLevel = LogLevel.Information;
    private const string AgentName = "RF1350Agent";
    private const string AgentInstructions = """
                                             Du er en arkiveringsekspert.
                                             Du har stålkontroll på arkivering av dokumenter og e-poster.
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

    public AiRf1350Service(IConfiguration config, ILogger<AiRf1350Service> logger)
    {
        _logger = logger;

        // get a new kernel builder
        IKernelBuilder kernelBuilder = AiHelper.CreateNewKernelBuilder(AgentLogLevel);
        
        // add services needed by the plugins (if any)
        //kernelBuilder.Services.AddSingleton(config);
        
        // add needed plugins (if any)
        
        // build a new agent
        _agent = AiHelper.CreateNewAgent(kernelBuilder, AgentName, AgentInstructions, _agentResponseFormat);
    }
    
    public async Task<(ChatHistory, Rf1350ChatResult?)> Ask(string prompt, ChatHistory? chatHistory = null)
    {
        var history = await _agent.InvokeAgent(prompt, chatHistory);
        
        return (history, AiHelper.GetLatestAnswer<Rf1350ChatResult>(history));
    }
}