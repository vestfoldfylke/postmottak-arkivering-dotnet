using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;

namespace postmottak_arkivering_dotnet.Services;

public interface IBlobService
{
    Task<T?> DownloadBlobContent<T>(string blobName, CancellationToken? stoppingToken = null);
    Task<string?> DownloadBlobContentAsString(string blobName, CancellationToken? stoppingToken = null);
    Task<List<BlobItem>> ListBlobs(string blobPath, CancellationToken? stoppingToken = null);
    Task RemoveBlobs(string blobPath, CancellationToken? stoppingToken = null);
    Task UploadBlob(string blobName, string content, CancellationToken? stoppingToken = null);
    Task UploadBlobFromStream(string blobName, byte[] bytes, CancellationToken? stoppingToken = null);
}

public class BlobService : IBlobService
{
    private readonly BlobServiceClient _blobServiceClient;

    private readonly string _containerName;
    
    public BlobService(IConfiguration config)
    {
        _blobServiceClient = new BlobServiceClient(config["BlobStorageConnectionString"] ?? throw new NullReferenceException());
        
        _containerName = config["BlobStorageContainerName"] ?? throw new NullReferenceException();
    }
    
    public Task<string?> DownloadBlobContentAsString(string blobName, CancellationToken? stoppingToken = null)
        => GetBlobContentAsString(blobName, stoppingToken);
    
    public async Task<T?> DownloadBlobContent<T>(string blobName, CancellationToken? stoppingToken = null)
    {
        var content = await GetBlobContentAsString(blobName, stoppingToken);
        if (string.IsNullOrEmpty(content))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T?>(content) ?? throw new InvalidOperationException("Failed to deserialize blob content"); 
    }

    public async Task<List<BlobItem>> ListBlobs(string blobPath, CancellationToken? stoppingToken = null)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        List<BlobItem> blobItems = [];
        
        var blobs = containerClient.GetBlobsAsync(prefix: blobPath, cancellationToken: stoppingToken ?? CancellationToken.None);
        await foreach (var blob in blobs)
        {
            blobItems.Add(blob);
        }

        return blobItems;
    }

    public async Task RemoveBlobs(string blobPath, CancellationToken? stoppingToken = null)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        
        var blobs = await ListBlobs(blobPath, stoppingToken);
        foreach (var blob in blobs)
        {
            await containerClient.DeleteBlobIfExistsAsync(blob.Name, DeleteSnapshotsOption.IncludeSnapshots, null, stoppingToken ?? CancellationToken.None);
        }
    }

    public async Task UploadBlob(string blobName, string content,
        CancellationToken? stoppingToken = null)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);

        await UploadBlobAsBytes(blobName, bytes, stoppingToken);
    }
    
    public Task UploadBlobFromStream(string blobName, byte[] bytes, CancellationToken? stoppingToken = null) =>
        UploadBlobAsBytes(blobName, bytes, stoppingToken);
    
    private async Task<string?> GetBlobContentAsString(string blobName, CancellationToken? stoppingToken = null)
    {
        var client = _blobServiceClient.GetBlobContainerClient(_containerName);

        try
        {
            var blob = await client.GetBlobClient(blobName).DownloadAsync(stoppingToken ?? CancellationToken.None);

            using var reader = new StreamReader(blob.Value.Content);
            var content = await reader.ReadToEndAsync(stoppingToken ?? CancellationToken.None);

            return content;
        }
        catch (RequestFailedException)
        {
            return null;
        }
    }
    
    private async Task UploadBlobAsBytes(string blobName, byte[] bytes, CancellationToken? stoppingToken = null)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(blobName);
        
        await blobClient.UploadAsync(new BinaryData(bytes), true, stoppingToken ?? CancellationToken.None);
    }
}