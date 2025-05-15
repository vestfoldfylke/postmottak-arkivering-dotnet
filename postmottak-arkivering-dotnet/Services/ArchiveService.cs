using System;
using System.IO;
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
    Task<JsonNode?> ArchiveCustom(object payload, string route);
    Task<JsonNode> CreateCase(object parameter);
    Task<JsonNode> CreateDocument(object parameter);
    Task<JsonArray> GetCases(object parameter);
    Task<JsonArray> GetDocuments(object parameter);
    (string, string) GetFileExtension(string input);
    Task<JsonArray> GetProjects(object parameter);
    Task<JsonNode> SignOff(object parameter);

    Task<JsonNode> SyncEnterprise(string organizationNr);

    Task<JsonNode> SyncPrivatePerson(object privatePerson);
}

public class ArchiveService : IArchiveService
{
    private readonly IAuthenticationService _authService;
    private readonly ILogger<ArchiveService> _logger;

    private readonly HttpClient _archiveClient;
    private readonly string[] _scopes;

    private readonly string[] _fileExtensionsToConvert =
    [
        "PDF",
        "JPG",
        "EML",
        "JPEG",
        "XLSX",
        "XLS",
        "RTF",
        "MSG",
        "PPT",
        "PPTX",
        "DOCX",
        "DOC",
        "HTML",
        "HTM",
        "TIFF"
    ];

    private readonly string[] _validFileExtensions =
    [
        "UF",
        "DOC",
        "XLS",
        "PPT",
        "MPP",
        "RTF",
        "TIF",
        "PDF",
        "TXT",
        "HTM",
        "JPG",
        "MSG",
        "DWF",
        "ZIP",
        "DWG",
        "ODT",
        "ODS",
        "ODG",
        "XML",
        "DOCX",
        "EML",
        "MHT",
        "XLSX",
        "PPTX",
        "GIF",
        "ONE",
        "DOCM",
        "SOI",
        "MPEG-2",
        "MP3",
        "XLSB",
        "PPTM",
        "VSD",
        "VSDX",
        "XLSM",
        "SOS",
        "HTML",
        "PNG",
        "MOV",
        "PPSX",
        "WMV",
        "XPS",
        "JPEG",
        "TIFF",
        "MP4",
        "WAV",
        "PUB",
        "BMP",
        "IFC",
        "KOF",
        "VGT",
        "GSI",
        "GML",
        "cfb",
        "26",
        "2",
        "hiec",
        "md"
    ];

    private readonly JsonSerializerOptions _indentedSerializer = new JsonSerializerOptions { WriteIndented = true };

    public ArchiveService(IConfiguration config, IAuthenticationService authService, ILogger<ArchiveService> logger)
    {
        _authService = authService;
        _logger = logger;
        
        var scopes = config["ARCHIVE_SCOPE"] ?? throw new NullReferenceException();

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
            BaseAddress = new Uri(config["ARCHIVE_BASE_URL"] ?? throw new NullReferenceException())
        };
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
            var errorMessage = JsonSerializer.Deserialize<ArchiveErrorMessage>(resultContent, new JsonSerializerOptions{ PropertyNamingPolicy = JsonNamingPolicy.CamelCase}) ?? throw new InvalidOperationException("Failed to deserialize error message");
            _logger.LogError("Archive error with Payload {@Payload}: {Message} : StatusCode: {StatusCode}. Data: {@Data}", payload, errorMessage.Message, result.StatusCode, errorMessage.Data);
            throw new InvalidOperationException(JsonSerializer.Serialize(
                new { errorMessage.Message, result.StatusCode, errorMessage.Data, Payload = payload },
                _indentedSerializer));
        }

        return JsonNode.Parse(resultContent);
    }
    
    public async Task<JsonNode?> ArchiveCustom(object payload, string route)
    {
        var token = await _authService.GetAccessToken(_scopes);
        
        _archiveClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
        
        var body = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
        
        var result = await _archiveClient.PostAsync(route, body);
        var resultContent = await result.Content.ReadAsStringAsync();
        
        if (!result.IsSuccessStatusCode)
        {
            var errorMessage = JsonSerializer.Deserialize<ArchiveErrorMessage>(resultContent, new JsonSerializerOptions{ PropertyNamingPolicy = JsonNamingPolicy.CamelCase}) ?? throw new InvalidOperationException("Failed to deserialize error message");
            _logger.LogError("Archive error with Payload {@Payload}: {Message} : StatusCode: {StatusCode}. Data: {@Data}", payload, errorMessage.Message, result.StatusCode, errorMessage.Data);
            throw new InvalidOperationException(errorMessage.Message);
        }

        return JsonNode.Parse(resultContent);
    }

    public async Task<JsonNode> CreateCase(object parameter)
    {
        var payload = new ArchivePayload
        {
            service = "CaseService",
            method = "CreateCase",
            parameter = parameter
        };

        var result = await Archive(payload);
        if (result is null)
        {
            _logger.LogError("Failed to create case with Parameter {@Parameter}", parameter);
            throw new InvalidOperationException($"Failed to create case with Parameter {JsonSerializer.Serialize(parameter, _indentedSerializer)}");
        }

        return result;
    }
    
    public async Task<JsonNode> CreateDocument(object parameter)
    {
        var payload = new ArchivePayload
        {
            service = "DocumentService",
            method = "CreateDocument",
            parameter = parameter
        };

        var result = await Archive(payload);
        if (result is null)
        {
            _logger.LogError("Failed to create document with Parameter {@Parameter}", parameter);
            throw new InvalidOperationException($"Failed to create document with Parameter {JsonSerializer.Serialize(parameter, _indentedSerializer)}");
        }

        return result;
    }
    
    public async Task<JsonArray> GetCases(object parameter)
    {
        var payload = new ArchivePayload
        {
            service = "CaseService",
            method = "GetCases",
            parameter = parameter
        };
        
        if (await Archive(payload) is not JsonArray result)
        {
            _logger.LogError("Failed to get cases with Parameter {@Parameter}", parameter);
            throw new InvalidOperationException($"Failed to get cases with Parameter {JsonSerializer.Serialize(parameter, _indentedSerializer)}");
        }

        return result;
    }
    
    public async Task<JsonArray> GetDocuments(object parameter)
    {
        var payload = new ArchivePayload
        {
            service = "DocumentService",
            method = "GetDocuments",
            parameter = parameter
        };
        
        if (await Archive(payload) is not JsonArray result)
        {
            _logger.LogError("Failed to get documents with Parameter {@Parameter}", parameter);
            throw new InvalidOperationException($"Failed to get documents with Parameter {JsonSerializer.Serialize(parameter, _indentedSerializer)}");
        }

        return result;
    }

    public (string, string) GetFileExtension(string input)
    {
        string extension = Path.GetExtension(input);
        if (string.IsNullOrEmpty(extension))
        {
            return ("UF", "A");
        }

        extension = extension.Replace(".", "");

        extension = _validFileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) ? extension : "UF";

        bool convert = _fileExtensionsToConvert.Contains(extension, StringComparer.OrdinalIgnoreCase);
        
        return (extension, convert ? "P" : "A");
    }

    public async Task<JsonArray> GetProjects(object parameter)
    {
        var payload = new ArchivePayload
        {
            service = "ProjectService",
            method = "GetProjects",
            parameter = parameter
        };
        
        if (await Archive(payload) is not JsonArray result)
        {
            _logger.LogError("Failed to get projects with Parameter {@Parameter}", parameter);
            throw new InvalidOperationException($"Failed to get projects with Parameter {JsonSerializer.Serialize(parameter, _indentedSerializer)}");
        }

        return result;
    }
    
    public async Task<JsonNode> SignOff(object parameter)
    {
        var payload = new ArchivePayload
        {
            service = "DocumentService",
            method = "SignOffDocument",
            parameter = parameter
        };

        var result = await Archive(payload);
        if (result is null)
        {
            _logger.LogError("Failed to sign off with Parameter {@Parameter}", parameter);
            throw new InvalidOperationException($"Failed to sign off with Parameter {JsonSerializer.Serialize(parameter, _indentedSerializer)}");
        }

        return result;
    }
    
    public async Task<JsonNode> SyncEnterprise(string organizationNr)
    {
        var payload = new
        {
            orgnr = organizationNr
        };

        var result = await ArchiveCustom(payload, "syncEnterprise");
        if (result is null)
        {
            _logger.LogError("Failed to sync enterprise with Payload {@Payload}", payload);
            throw new InvalidOperationException($"Failed to sync enterprise with Payload {JsonSerializer.Serialize(payload, _indentedSerializer)}");
        }

        return result;
    }
    
    public async Task<JsonNode> SyncPrivatePerson(object privatePerson)
    {
        var result = await ArchiveCustom(privatePerson, "syncPrivatePerson");
        if (result is null)
        {
            _logger.LogError("Failed to sync private person with Payload {@Payload}", privatePerson);
            throw new InvalidOperationException($"Failed to sync private person with Payload {JsonSerializer.Serialize(privatePerson, _indentedSerializer)}");
        }

        return result;
    }
}