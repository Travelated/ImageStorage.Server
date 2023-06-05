using Azure;
using Azure.Storage.Blobs.Models;
using Imazen.Common.Storage;

namespace ImageStorage.Server.Azure;

internal class AzureBlob :IBlobData
{
    private readonly Response<BlobDownloadInfo> _response;

    internal AzureBlob(Response<BlobDownloadInfo> r)
    {
        _response = r;
    }

    public bool? Exists => true;
    public DateTime? LastModifiedDateUtc => _response.Value.Details.LastModified.UtcDateTime;
    public Stream OpenRead()
    {
        return _response.Value.Content;
    }

    public void Dispose()
    {
        _response?.Value?.Dispose();
    }
}