using System;
using System.Net.Http;
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
        
        _appName = configuration["AppName"] ?? throw new NullReferenceException();
        _version = configuration["Version"] ?? throw new NullReferenceException();
        string statisticsBaseUrl = configuration["Statistics_BaseUrl"] ?? throw new NullReferenceException();
        string statisticsKey = configuration["Statistics_Key"] ?? throw new NullReferenceException();
        
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
}