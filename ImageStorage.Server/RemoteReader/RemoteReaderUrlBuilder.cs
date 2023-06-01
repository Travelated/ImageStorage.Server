using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace ImageStorage.Server.RemoteReader;

public static class RemoteReaderUrlBuilder
{
    /// <summary>
    /// Url -> "https://domain.com/path/path2/image.png" to "9e107d9d372bb6821bd91d3542a419d6/domain.com/path/path2/image.png"
    /// </summary>
    /// <param name="url"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public static string EncodeAndSignUrl(string url, string key)
    {
        // Parse the URL
        var uri = new Uri(url);

        // Extract the host name
        var host = uri.Host;

        // Sign the host name
        var sig = SignString(host, key, 8);

        // Combine the signature, host and path to form a new URL
        string transformedUrl = $"{sig}/{uri.Host}{uri.AbsolutePath}";

        return transformedUrl;
    }
    
    private static readonly ConcurrentDictionary<string, string> SignatureCache =
        new ConcurrentDictionary<string, string>();
    
    public static string? DecodeAndVerifyUrl(string signedUrl, string key)
    {
        var parts = signedUrl.Split(new[] { '/' }, 2);
        if (parts.Length != 2)
        {
            return null; // Invalid format
        }

        var signature = parts[0];
        var hostnameAndPath = parts[1];

        // Extract the host name
        var uri = new Uri("http://" + hostnameAndPath);
        var host = uri.Host;

        // Check if the signature is already in the cache
        if (SignatureCache.TryGetValue(signature, out var cachedHost))
        {
            // If the host matches the cached one, return the original URL
            if (cachedHost == host)
            {
                return $"https://{hostnameAndPath}";
            }
        }

        // Generate the signature based on the host name and provided key
        var expectedSignature = SignString(host, key, 8);

        // If the provided signature matches the expected one, store it in the cache and return the original URL
        if (signature == expectedSignature)
        {
            SignatureCache.TryAdd(signature, host);
            return $"https://{hostnameAndPath}";
        }
        else
        {
            return null;
        }
    }
    
    public static string SignString(string data, string key, int signatureLengthInBytes)
    {
        if (signatureLengthInBytes < 1 || signatureLengthInBytes > 32) throw new ArgumentOutOfRangeException(nameof(signatureLengthInBytes));
        HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        //32-byte hash is a bit overkill. Truncation only marginally weakens the algorithm integrity.
        byte[] shorterHash = new byte[signatureLengthInBytes];
        Array.Copy(hash, shorterHash, signatureLengthInBytes);
        return ToBase64U(shorterHash);
    }

    static string ToBase64U(byte[] data)
    {
        return Convert.ToBase64String(data).Replace("=", String.Empty).Replace('+', '-').Replace('/', '_');
    }
}