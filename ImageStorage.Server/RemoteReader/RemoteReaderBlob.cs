using Imazen.Common.Storage;

namespace ImageStorage.Server.RemoteReader;

public class RemoteReaderBlob : IBlobData
{
    private readonly HttpResponseMessage _response;

    internal RemoteReaderBlob(HttpResponseMessage r)
    {
        _response = r;
    }

    public bool? Exists => true;

    public DateTime? LastModifiedDateUtc => _response.Headers.Date?.UtcDateTime;

    public void Dispose()
    {
        _response.Dispose();
    }

    public Stream OpenRead()
    {
        return _response.Content.ReadAsStreamAsync().Result;
    }
}