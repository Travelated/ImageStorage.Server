using System.Text;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Imazen.Common.Extensibility.StreamCache;
using Imazen.Common.Issues;

namespace ImageStorage.Server.Azure;

public class AzureBlobCache : IStreamCache
{
    private readonly ILogger<AzureBlobCache> _logger;
    private readonly BlobContainerClient _containerClient;

    public AzureBlobCache(ILogger<AzureBlobCache> logger, AzureUploadConfig config, BlobServiceClient blobService)
    {
        _logger = logger;
        _containerClient = blobService.GetBlobContainerClient(config.CacheContainer);
    }


    public IEnumerable<IIssue> GetIssues()
    {
        return Enumerable.Empty<IIssue>();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task<IStreamCacheResult> GetOrCreateBytes(byte[] key, AsyncBytesResult dataProviderCallback, CancellationToken cancellationToken,
        bool retrieveContentType)
    {
        string blogName = CreateAzureBlobName(key);
        
        var inCache = await GetBlobStreamAndContentTypeAsync(blogName, cancellationToken);
        if (inCache != null)
        {
            var result = new AsyncCacheResult()
            {
                ContentType = inCache.ContentType,
                Data = inCache.BlobStream,
                Detail = AsyncCacheDetailResult.DiskHit
            };

            return result;
        }
        
        var resizedImage = await dataProviderCallback(cancellationToken);
        var memoryStream = new MemoryStream(resizedImage.Bytes.ToArray());
        memoryStream.Position = 0;
        
        var uploadResult = await UploadBlobAsync(blogName, memoryStream, resizedImage.ContentType, cancellationToken);
        memoryStream.Position = 0;
        
        var resizedResult = new AsyncCacheResult()
        {
            ContentType = resizedImage.ContentType,
            Data = memoryStream,
            Detail = uploadResult ? AsyncCacheDetailResult.WriteSucceeded : AsyncCacheDetailResult.Miss
        };

        _logger.LogInformation("Upload result [{WriteStatus}]: {KeyString}, content-type: {ResultContentType}, Size: {BytesCount}", 
            resizedResult.Detail, blogName, resizedResult.ContentType, resizedImage.Bytes.Count);

        return resizedResult;
    }
    
    async Task<bool> UploadBlobAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken)
    {
        BlobClient blobClient = _containerClient.GetBlobClient(blobName);

        try
        {
            // Upload the blob
            await blobClient.UploadAsync(content, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType
                },
                Conditions = new BlobRequestConditions
                {
                    IfNoneMatch = ETag.All // Upload should only proceed if no blob with the same name already exists
                }
            }, cancellationToken);

            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            _logger.LogWarning("Blob {BlobName} already exists", blobName);
        } 
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading blob {BlobName}", blobName);
        }
        
        // If the blob already exists, return false
        return false;
    }

    
    public record BlobResult(Stream BlobStream, string ContentType);

    public async Task<BlobResult?> GetBlobStreamAndContentTypeAsync(string blobName, CancellationToken cancellationToken)
    {
        BlobClient blobClient = _containerClient.GetBlobClient(blobName);

        try
        {
            // Attempt to open a stream to the blob's content
            BlobDownloadStreamingResult streamingResult =
                await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);

            var result = new BlobStreamWrapper(streamingResult.Content, streamingResult.Details.ContentLength);
            
            // Return the stream and the content type
            return new BlobResult(result, streamingResult.Details.ContentType);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // If the blob does not exist, return null
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading blob {BlobName}", blobName);
            return null;
        }
    }

    
    string CreateAzureBlobName(byte[] key)
    {
        string blobName = Encoding.UTF8.GetString(key);
        
        var sb = new StringBuilder();

        for (int i = 0; i < blobName.Length; i++)
        {
            // Only insert slashes for the first 5 segments of 2 characters each
            if (i > 0 && i < 10 && i % 2 == 0)
            {
                sb.Append("/");
            }

            sb.Append(blobName[i]);
        }

        return sb.ToString();
    }
}