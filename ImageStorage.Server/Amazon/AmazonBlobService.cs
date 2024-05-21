using System.Diagnostics;
using Amazon.S3;
using Amazon.S3.Model;
using Azure;
using Azure.Storage.Blobs;
using ImageStorage.Server.Azure;
using ImageStorage.Server.Controllers;
using Imazen.Common.Storage;

namespace ImageStorage.Server.Amazon;

public class AmazonBlobService : IBlobProvider, IUploadBlobStorage
{
    private readonly List<PrefixMapping> mappings = new List<PrefixMapping>();

    private readonly IAmazonS3 _client;
    private readonly ILogger<AmazonBlobService> _logger;

    public AmazonBlobService(IAmazonS3 client, AzureBlobServiceOptions options, ILogger<AmazonBlobService> logger)
    {
        _client = client;
        _logger = logger;
        foreach (var m in options.Mappings)
        {
            mappings.Add(m);
        }

        mappings.Sort((a, b) => b.UrlPrefix.Length.CompareTo(a.UrlPrefix.Length));
    }

    public IEnumerable<string> GetPrefixes()
    {
        return mappings.Select(m => m.UrlPrefix);
    }

    public bool SupportsPath(string virtualPath)
    {
        return mappings.Any(s => virtualPath.StartsWith(s.UrlPrefix,
            s.IgnorePrefixCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
    }

    public async Task<IBlobData> Fetch(string virtualPath)
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();
        
        var mapping = mappings.FirstOrDefault(s => virtualPath.StartsWith(s.UrlPrefix,
            s.IgnorePrefixCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
        if (mapping.UrlPrefix == null)
        {
            throw new BlobMissingException($"Amazon S3 object \"{virtualPath}\" not found.");
        }

        var partialKey = virtualPath.Substring(mapping.UrlPrefix.Length).TrimStart('/');

        if (mapping.LowercaseBlobPath)
        {
            partialKey = partialKey.ToLowerInvariant();
        }

        var key = string.IsNullOrEmpty(mapping.BlobPrefix)
            ? partialKey
            : mapping.BlobPrefix + "/" + partialKey;


        try
        {
            var request = new GetObjectRequest
            {
                BucketName = mapping.Container, // In S3, 'Container' is analogous to 'BucketName'
                Key = key
            };

            var response = await _client.GetObjectAsync(request);
            
            // Show how much time passed
            _logger.LogInformation($"Amazon S3 object \"{key}\" fetched in {stopWatch.ElapsedMilliseconds} ms");
            return new AmazonS3Blob(response);
        }
        catch (AmazonS3Exception e)
        {
            if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new BlobMissingException($"Amazon S3 object \"{key}\" not found.\n({e.Message})", e);
            }

            throw;
        }
    }

    public async Task<Stream?> OpenReadStream(string bucket, string fileName, CancellationToken cancellationToken)
    {
        fileName = fileName.TrimStart('/');
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = bucket,
                Key = fileName
            };

            var response = await _client.GetObjectAsync(request, cancellationToken);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception e)
        {
            if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            throw;
        }
        
    }

    public async Task UploadStream(string bucket, string fileName, Stream stream, CancellationToken cancellationToken)
    {
        fileName = fileName.TrimStart('/');
        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = fileName,
            InputStream = stream,
            AutoCloseStream = true
        };

        await _client.PutObjectAsync(request, cancellationToken);
        
    }
}