using System.Text.RegularExpressions;
using Imazen.Common.Storage;

namespace ImageStorage.Server.RemoteReader;

public class RemoteReaderService : IBlobProvider
{
    private readonly RemoteReaderServiceOptions _options;
    private readonly ILogger<RemoteReaderService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public RemoteReaderService(RemoteReaderServiceOptions options,
        ILogger<RemoteReaderService> logger, IHttpClientFactory httpClientFactory)
    {
        _options = options;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    string? DecodeUrl(string virtualPath)
    {
        virtualPath = RemovePrefix(virtualPath);
        string? whitelisted = GetUrlFromWhitelistedDomains(virtualPath);
        if (whitelisted != null)
        {
            return whitelisted;
        }
        
        return RemoteReaderUrlBuilder.DecodeAndVerifyUrl(virtualPath, _options.SigningKey);
    }
    
    string? GetUrlFromWhitelistedDomains(string signedUrl)
    {
        if (_options.WhitelistedDomains == null)
        {
            return null;
        }
        
        var parts = signedUrl.Substring(1).Split(new[] { '/' }, 3);
        if (parts.Length != 3)
        {
            return null; // Invalid format
        }
        
        string prefix = _options.UseHttp ? "http://" : "https://";

        // Extract the host name
        var uri = new Uri(prefix + parts[1]);
        var host = uri.Host;

        // Check if the domain is in the allowed list
        foreach (var domain in _options.WhitelistedDomains)
        {
            if (domain.StartsWith("*.")) // Wildcard domain
            {
                // Convert the wildcard domain to a regex pattern
                var pattern = "^" + Regex.Escape(domain).Replace("\\*", ".*") + "$";
                if (Regex.IsMatch(host, pattern, RegexOptions.IgnoreCase))
                {
                    return $"{prefix}{parts[1]}/{parts[2]}";
                }
            }
            else // Regular domain
            {
                if (string.Equals(host, domain, StringComparison.OrdinalIgnoreCase))
                {
                    return $"{prefix}{parts[1]}/{parts[2]}";
                }
            }
        }

        // If none of the allowed domains matched, return null
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
            using var httpClient = _httpClientFactory.CreateClient(nameof(RemoteReaderService));
            var resp = await httpClient.GetAsync(url);

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
    
    string RemovePrefix(string virtualPath)
    {
        return virtualPath.Substring(_options.Prefix.Length);
    }

    public bool SupportsPath(string virtualPath)
    {
        // Check if path starts from the configured prefix
        if (!virtualPath.StartsWith(_options.Prefix))
        {
            return false;
        }

        virtualPath = RemovePrefix(virtualPath);
        
        // Split the virtual path into segments
        var segments = virtualPath.Substring(1).Split('/');

        // Verify that there are at least 3 segments (signature, domain, file)
        if (segments.Length < 3)
        {
            return false;
        }

        // Verify the format of the signature, it should be a 32-character hexadecimal string
        if (!Regex.IsMatch(segments[0], @"^[a-zA-Z0-9-_]{32}$"))
        {
            return false;
        }

        // // Verify the format of the domain name
        // UriHostNameType hostNameType = Uri.CheckHostName(segments[1]);
        // if (hostNameType != UriHostNameType.Dns)
        // {
        //     return false;
        // }

        return true;
    }

}