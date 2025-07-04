using System;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace postmottak_arkivering_dotnet.Services;

public interface IStatisticsService
{
    Task InsertRuleStatistics(string description, string ruleName, int count);
    Task InsertSystemStatistics(string description, string type, string? messageId = null, string? sender = null);
}

public class StatisticsService : IStatisticsService
{
    private readonly ILogger<StatisticsService> _logger;
    
    private readonly string _engine;
    private readonly HttpClient _httpClient;
    
    private const string System = "postmottak-arkivering";
    private const string Company = "ORG";
    private const string Department = "Dokumentasjon og politisk støtte";
    private const string ProjectId = "11";

    public StatisticsService(IConfiguration configuration, ILogger<StatisticsService> logger)
    {
        _logger = logger;
        
        var appName = configuration["AppName"]
                   ?? Assembly.GetEntryAssembly()?.GetName().Name
                   ?? throw new InvalidOperationException($"Missing AppName in configuration and couldn't get Name from Assembly");
        var version = configuration["Version"]
            ?? GetInformationalVersion()
            ?? throw new InvalidOperationException($"Missing Version in configuration and couldn't get Version from Assembly");
        _engine = $"{appName} {version}";
        
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

    public async Task InsertRuleStatistics(string description, string ruleName, int count)
    {
        var payload = new
        {
            system = System,
            engine = _engine,
            company = Company,
            department = Department,
            description,
            projectId = ProjectId,
            type = ruleName,
            count
        };
        
        await InsertStatistics(payload);
    }
    
    public async Task InsertSystemStatistics(string description, string type, string? messageId = null, string? sender = null)
    {
        var payload = new
        {
            system = System,
            engine = _engine,
            company = Company,
            department = Department,
            description,
            projectId = ProjectId,
            type,
            sender,
            externalId = messageId
        };

        await InsertStatistics(payload);
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

    private async Task InsertStatistics(object payload)
    {
        try
        {
            var body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8,
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