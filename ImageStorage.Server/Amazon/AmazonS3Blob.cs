using Amazon.S3.Model;
using Imazen.Common.Storage;

namespace ImageStorage.Server.Amazon;

internal class AmazonS3Blob : IBlobData
{
    private readonly GetObjectResponse _response;

    internal AmazonS3Blob(GetObjectResponse response)
    {
        _response = response;
    }

    public bool? Exists => _response.HttpStatusCode == System.Net.HttpStatusCode.OK;
    public DateTime? LastModifiedDateUtc => _response.LastModified;

    public Stream OpenRead()
    {
        return _response.ResponseStream;
    }

    public void Dispose()
    {
        _response?.Dispose();
    }
}