using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using postmottak_arkivering_dotnet.Contracts.Archive;

namespace postmottak_arkivering_dotnet.Services;

public interface IArchiveService
{
    Task<JsonNode?> Archive(ArchivePayload payload);
    Task<JsonNode> CreateCase(object parameter);
    // CreateDocument
    // SignOff
    // SyncEnterprise
    // SyncPrivatePerson
    // UploadFiles
    Task<JsonArray> GetCases(object parameter);
}

public class ArchiveService : IArchiveService
{
    private readonly IAuthenticationService _authService;
    private readonly ILogger<ArchiveService> _logger;

    private readonly HttpClient _archiveClient;

    private readonly string[] _scopes;

    public ArchiveService(IConfiguration config, IAuthenticationService authService, ILogger<ArchiveService> logger)
    {
        _authService = authService;
        _logger = logger;
        
        var scopes = config["ARCHIVE_SCOPE"] ?? throw new NullReferenceException("Archive scope cannot be null");

        if (string.IsNullOrEmpty(scopes))
        {
            throw new NullReferenceException("Scopes cannot be empty");
        }
        
        _scopes = scopes.Split(",");

        if (_scopes.Any(scope => !scope.Contains("https://", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Archive scope must start with 'https://'");
        }
        
        _archiveClient = new HttpClient
        {
            BaseAddress = new Uri(config["ARCHIVE_BASE_URL"] ?? throw new NullReferenceException("Archive base URL cannot be null"))
        };
    }

    public async Task<JsonArray> GetCases(object parameter)
    {
        var payload = new ArchivePayload
        {
            method = "GetCases",
            service = "CaseService",
            parameter = parameter
        };
        
        if (await Archive(payload) is not JsonArray result)
        {
            _logger.LogError("Failed to get cases with Parameter {@Parameter}", parameter);
            throw new InvalidOperationException($"Failed to get cases with Parameter {JsonSerializer.Serialize(parameter)}");
        }

        return result;
    }
    
    public async Task<JsonNode> CreateCase(object parameter)
    {
        var payload = new ArchivePayload
        {
            method = "CreateCase",
            service = "CaseService",
            parameter = parameter
        };

        var result = await Archive(payload);
        if (result is null)
        {
            _logger.LogError("Failed to create case with Parameter {@Parameter}", parameter);
            throw new InvalidOperationException($"Failed to create case with Parameter {JsonSerializer.Serialize(parameter)}");
        }

        return result;
    }
    
    public async Task<JsonNode?> Archive(ArchivePayload payload)
    {
        var token = await _authService.GetAccessToken(_scopes);
        
        _archiveClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
        
        var body = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
        
        var result = await _archiveClient.PostAsync("archive", body);
        var resultContent = await result.Content.ReadAsStringAsync();
        
        if (!result.IsSuccessStatusCode)
        {
            var errorMessage = JsonSerializer.Deserialize<ArchiveErrorMessage>(resultContent) ?? throw new InvalidOperationException("Failed to deserialize error message");
            _logger.LogError("Archive error: {Message} : StatusCode: {StatusCode}. Data: {@Data}", errorMessage.message, result.StatusCode, errorMessage.data);
            throw new InvalidOperationException(errorMessage.message);
        }

        return JsonNode.Parse(resultContent);
    }
}