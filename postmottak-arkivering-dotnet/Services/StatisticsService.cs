using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace postmottak_arkivering_dotnet.Services;

public interface IStatisticsService
{
    Task InsertStatistics(string description, string messageId, string type, string? sender = null);
}

public class StatisticsService : IStatisticsService
{
    private readonly ILogger<StatisticsService> _logger;
    
    private readonly string _appName;
    private readonly string _version;
    private readonly HttpClient _httpClient;

    public StatisticsService(IConfiguration configuration, ILogger<StatisticsService> logger)
    {
        _logger = logger;
        
        _appName = configuration["AppName"]
                   ?? Assembly.GetEntryAssembly()?.GetName().Name
                   ?? throw new InvalidOperationException($"Missing AppName in configuration and couldn't get Name from Assembly");
        _version = configuration["Version"]
            ?? GetInformationalVersion()
            ?? throw new InvalidOperationException($"Missing Version in configuration and couldn't get Version from Assembly");
        
        var statisticsBaseUrl = configuration["STATISTICS_BASE_URL"] ?? throw new NullReferenceException();
        var statisticsKey = configuration["STATISTICS_KEY"] ?? throw new NullReferenceException();
        
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(statisticsBaseUrl),
            DefaultRequestHeaders =
            {
                {
                    "x-functions-key", statisticsKey
                }
            }
        };
    }
    
    public async Task InsertStatistics(string description, string messageId, string type, string? sender = null)
    {
        var payload = new
        {
            system = "postmottak-arkivering",
            engine = $"{_appName} {_version}",
            company = "ORG",
            department = "Dokumentasjon og politisk støtte",
            description,
            projectId = "11",
            type,
            sender,
            externalId = messageId,
        };

        try
        {
            var body = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8,
                "application/json");

            var result = await _httpClient.PostAsync("stats", body);

            if (result.IsSuccessStatusCode)
            {
                return;
            }

            var resultContent = await result.Content.ReadAsStringAsync();
            _logger.LogWarning(
                "Statistics error with Payload {@Payload}: {Message} : StatusCode: {StatusCode}",
                payload,
                resultContent,
                result.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Statistics error with Payload {@Payload}", payload);
        }
    }
    
    private static string? GetInformationalVersion()
    {
        var informationalVersion = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (informationalVersion is null)
        {
            return informationalVersion;
        }
        
        var versionParts = informationalVersion.Split("+");
        if (versionParts.Length > 1)
        {
            informationalVersion = versionParts[0];
        }

        return informationalVersion;
    }
}