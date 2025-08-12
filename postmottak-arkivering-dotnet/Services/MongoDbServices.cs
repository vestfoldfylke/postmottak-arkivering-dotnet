using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using postmottak_arkivering_dotnet.Utils;
using Vestfold.Extensions.MongoDb.Services;

namespace postmottak_arkivering_dotnet.Services;

public interface IMongoDbServices
{
    Task<DateTimeOffset> GetLastRegisteredDateTime(string ruleName);
    Task UpdateLastRegisteredDateTime(string ruleName, DateTimeOffset lastRegisteredDateTime);
}

public class MongoDbServices : IMongoDbServices
{
    private readonly ILogger<MongoDbServices> _logger;
    private readonly IMongoDbService _mongoDbService;

    private readonly string _databaseName;
    private readonly string _collectionName;
    
    private readonly FilterDefinition<BsonDocument> _emptyFilter = Builders<BsonDocument>.Filter.Empty;
    
    public MongoDbServices(IConfiguration configuration, ILogger<MongoDbServices> logger, IMongoDbService mongoDbService)
    {
        _databaseName = configuration["MONGODB_DATABASE_NAME"] ?? throw new InvalidOperationException("Missing MONGODB_DATABASE_NAME in configuration");
        _collectionName = configuration["MONGODB_COLLECTION_NAME"] ?? throw new InvalidOperationException("Missing MONGODB_COLLECTION_NAME in configuration");
        
        _logger = logger;
        _mongoDbService = mongoDbService;
    }

    public async Task<DateTimeOffset> GetLastRegisteredDateTime(string ruleName)
    {
        var collection = await _mongoDbService.GetMongoCollection<BsonDocument>(_databaseName, _collectionName);
        
        var rules = (await collection.FindAsync(_emptyFilter)).ToList();
        
        var ruleDocument = rules.FirstOrDefault();
        if (ruleDocument is null)
        {
            throw new InvalidOperationException($"No rules found in collection '{_collectionName}' in database '{_databaseName}'");
        }
        
        if (ruleDocument["rules"].AsBsonDocument.TryGetValue(ruleName, out var ruleNameElement))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(ruleNameElement.AsBsonDateTime.MillisecondsSinceEpoch);
        }
        
        _logger.LogInformation("Rule '{RuleName}' not found in collection '{CollectionName}' in database '{DatabaseName}'",
            ruleName, _collectionName, _databaseName);
        return DateTimeOffset.MinValue;
    }

    public async Task UpdateLastRegisteredDateTime(string ruleName, DateTimeOffset lastRegisteredDateTime)
    {
        var collection = await _mongoDbService.GetMongoCollection<BsonDocument>(_databaseName, _collectionName);
        
        var update = Builders<BsonDocument>.Update.Set(rules => rules["rules"][ruleName], new BsonDateTime(lastRegisteredDateTime.LocalDateTime));
        
        // NOTE: Update ruleName with the last registered date time, or create ruleName it if it does not exist
        var result = await collection.UpdateOneAsync(_emptyFilter, update, new UpdateOptions { IsUpsert = true });
        if (!result.IsAcknowledged)
        {
            _logger.LogWarning("Failed to upsert last registered date time for rule '{RuleName}' in collection '{CollectionName}' in database '{DatabaseName}'",
                ruleName, _collectionName, _databaseName);
        }
        
        _logger.LogInformation("Upserted RuleName '{RuleName}' to {LastRegisteredDateTime} in collection '{CollectionName}' in database '{DatabaseName}'",
            ruleName, HelperTools.GetUtcDateTimeString(lastRegisteredDateTime), _collectionName, _databaseName);
    }
}