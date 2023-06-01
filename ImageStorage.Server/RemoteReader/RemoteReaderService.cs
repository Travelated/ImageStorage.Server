using Imazen.Common.Storage;

namespace ImageStorage.Server.RemoteReader;

public class RemoteReaderService : IBlobProvider
{
    private readonly RemoteReaderServiceOptions _options;
    private readonly ILogger<RemoteReaderService> _logger;
    private readonly HttpClient _httpClient;

    public RemoteReaderService(RemoteReaderServiceOptions options,
        ILogger<RemoteReaderService> logger, HttpClient httpClient)
    {
        _options = options;
        _logger = logger;
        _httpClient = httpClient;
    }

    string? DecodeUrl(string virtualPath)
    {
        foreach (var key in _options.SigningKeys)
        {
            var decoded = RemoteReaderUrlBuilder.DecodeAndVerifyUrl(virtualPath, key);
            if (decoded != null)
            {
                return decoded;
            }
        }

        return null;
    }

    /// <summary>
    /// The remote URL and signature are encoded in the "file" part
    /// of the virtualPath parameter as follows:
    /// path/path/.../path/url_b64.hmac.ext
    /// </summary>
    /// <param name="virtualPath"></param>
    /// <returns></returns>
    public async Task<IBlobData> Fetch(string virtualPath)
    {
        var url = DecodeUrl(virtualPath);

        if (url == null)
        {
            _logger.LogWarning("RemoteReader blob {VirtualPath} not found. Invalid signature", virtualPath);
            throw new BlobMissingException($"RemoteReader blob \"{virtualPath}\" not found. Invalid signature.");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            _logger.LogWarning("RemoteReader blob {VirtualPath} not found. Invalid Uri: {Url}", virtualPath, url);
            throw new BlobMissingException($"RemoteReader blob \"{virtualPath}\" not found. Invalid Uri: {url}");
        }


        try
        {
            var resp = await _httpClient.GetAsync(url);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "RemoteReader blob {VirtualPath} not found. The remote {Url} responded with status: {StatusCode}",
                    virtualPath, url, resp.StatusCode);
                throw new BlobMissingException(
                    $"RemoteReader blob \"{virtualPath}\" not found. The remote \"{url}\" responded with status: {resp.StatusCode}");
            }

            return new RemoteReaderBlob(resp);
        }
        catch (BlobMissingException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "RemoteReader blob error retrieving {Url} for {VirtualPath}", url,
                virtualPath);
            throw new BlobMissingException(
                $"RemoteReader blob error retrieving \"{url}\" for \"{virtualPath}\".", ex);
        }
    }


    public IEnumerable<string> GetPrefixes()
    {
        return new[] { _options.Prefix };
    }

    public bool SupportsPath(string virtualPath)
    {
        // Split the virtual path into segments
        var segments = virtualPath.Split('/');

        // Verify that there are at least 3 segments (signature, domain, file)
        if (segments.Length < 3)
        {
            return false;
        }

        // Verify the format of the signature, it should be a 32-character hexadecimal string
        if (!System.Text.RegularExpressions.Regex.IsMatch(segments[0], @"^[a-fA-F0-9]{32}$"))
        {
            return false;
        }

        // Verify the format of the domain name
        UriHostNameType hostNameType = Uri.CheckHostName(segments[1]);
        if (hostNameType != UriHostNameType.Dns)
        {
            return false;
        }

        return true;
    }

}