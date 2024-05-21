using Azure;
using Azure.Storage.Blobs;
using Imazen.Common.Storage;

namespace ImageStorage.Server.Azure;

public class AzureBlobService : IBlobProvider
{
    private readonly List<PrefixMapping> mappings = new List<PrefixMapping>();

    private readonly BlobServiceClient _client;

    public AzureBlobService(BlobServiceClient client, AzureBlobServiceOptions options)
    {
        _client = client;
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
        var mapping = mappings.FirstOrDefault(s => virtualPath.StartsWith(s.UrlPrefix,
            s.IgnorePrefixCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
        if (mapping.UrlPrefix == null)
        {
            throw new BlobMissingException($"Azure Blob object \"{virtualPath}\" not found.");
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
            var blobClient = _client.GetBlobContainerClient(mapping.Container).GetBlobClient(key);

            var s = await blobClient.DownloadAsync();
            return new AzureBlob(s);
        }
        catch (RequestFailedException e)
        {
            if (e.Status == 404)
            {
                throw new BlobMissingException($"Azure blob \"{key}\" not found.\n({e.Message})", e);
            }

            throw;
        }
    }
}