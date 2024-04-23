using Amazon.S3;
using Amazon.S3.Model;
using ImageStorage.Server.Azure;
using Imazen.Common.Extensibility.StreamCache;
using Imazen.Common.Issues;

namespace ImageStorage.Server.Amazon;

public class AmazonBlobCache : IStreamCache
{
    private readonly ILogger<AmazonBlobCache> _logger;
    private readonly IAmazonS3 _client;
    private readonly string _cacheBucket;

    public AmazonBlobCache(ILogger<AmazonBlobCache> logger, AzureUploadConfig config, IAmazonS3 client)
    {
        _logger = logger;
        _client = client;
        _cacheBucket = config.CacheContainer;
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

    public async Task<IStreamCacheResult> GetOrCreateBytes(byte[] key, AsyncBytesResult dataProviderCallback,
        CancellationToken cancellationToken,
        bool retrieveContentType)
    {
        string blogName = AzureBlobCache.CreateAzureBlobName(key);

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
        if (resizedImage?.Bytes.Array == null)
        {
            throw new Exception("No bytes returned from data provider");
        }

        // No using as the stream will be disposed by the caller
        MemoryStream memoryStream = new MemoryStream(resizedImage.Bytes.Array,
            resizedImage.Bytes.Offset,
            resizedImage.Bytes.Count);
        memoryStream.Position = 0;

        var uploadResult = await UploadBlobAsync(blogName, memoryStream, resizedImage.ContentType, cancellationToken);
        memoryStream.Position = 0;

        var resizedResult = new AsyncCacheResult()
        {
            ContentType = resizedImage.ContentType,
            Data = memoryStream,
            Detail = uploadResult ? AsyncCacheDetailResult.WriteSucceeded : AsyncCacheDetailResult.Miss
        };

        _logger.LogInformation(
            "Upload result [{WriteStatus}]: {KeyString}, content-type: {ResultContentType}, Size: {BytesCount}",
            resizedResult.Detail, blogName, resizedResult.ContentType, resizedImage.Bytes.Count);

        return resizedResult;
    }

    async Task<bool> UploadBlobAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken)
    {
        try
        {
            var putRequest = new PutObjectRequest
            {
                BucketName = _cacheBucket,
                Key = blobName,
                InputStream = content,
                ContentType = contentType,
                AutoCloseStream = false,
            };

            await _client.PutObjectAsync(putRequest, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogWarning("Blob {BlobName} already exists", blobName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading blob {BlobName}", blobName);
            return false;
        }
    }


    public class BlobResult : IDisposable
    {
        public required GetObjectResponse Response { get; init; }
        public Stream BlobStream => Response.ResponseStream;
        public string ContentType => Response.Headers.ContentType;
        public void Dispose()
        {
            Response.Dispose();
        }
    }

    async Task<BlobResult?> GetBlobStreamAndContentTypeAsync(string blobName,
        CancellationToken cancellationToken)
    {
        try
        {
            var getRequest = new GetObjectRequest
            {
                BucketName = _cacheBucket,
                Key = blobName
            };

            var response = await _client.GetObjectAsync(getRequest, cancellationToken);
            return new BlobResult
            {
                Response = response
            };
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading blob {BlobName}", blobName);
            return null;
        }
    }
}