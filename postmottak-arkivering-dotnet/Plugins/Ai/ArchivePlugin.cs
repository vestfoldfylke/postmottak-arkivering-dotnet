using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Serilog;
using Vestfold.Extensions.Archive.Services;

namespace postmottak_arkivering_dotnet.Plugins.Ai;

public class ArchivePlugin
{
    private readonly IArchiveService _archiveService;

    public ArchivePlugin(IArchiveService archiveService)
    {
        _archiveService = archiveService;
    }
    
    [KernelFunction("get_cases")]
    [Description("Get cases from the archive based on given input")]
    public async Task<JsonArray> GetCasesAsync(string title)
    {
        title = $"%{title}%";
        Log.Logger.Information("GetCasesAsync called with title: {Title}", title);
        
        return await _archiveService.GetCases(new
        {
            IncludeFiles = false,
            Title = title
        });
    }
}