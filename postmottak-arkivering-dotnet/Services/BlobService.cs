using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace postmottak_arkivering_dotnet.Services;

public interface IBlobService
{
    Task<string?> GetBlobs(string containerName, string blobName);
}

public class BlobService : IBlobService
{
    private readonly ILogger<BlobService> _logger;
    private readonly BlobServiceClient _blobServiceClient;
    
    public BlobService(IConfiguration configuration, ILogger<BlobService> logger)
    {
        _logger = logger;
        
        string connectionString = configuration["BlobStorageConnectionString"] ?? throw new NullReferenceException();
        _blobServiceClient = new BlobServiceClient(connectionString);
    }

    public async Task<string?> GetBlobs(string containerName, string blobName)
    {
        BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

        try
        {
            var blob = await containerClient.GetBlobClient(blobName).DownloadContentAsync();

            using var reader = new StreamReader(blob.Value.Content.ToStream());
            string content = await reader.ReadToEndAsync();

            return content;
        }
        catch (Azure.RequestFailedException ex)
        {
            _logger.LogError(ex, "Error downloading blob {BlobName} from container {ContainerName}", blobName, containerName);
            return null;
        }
    }
}