using Azure.Storage.Blobs;

namespace ImageStorage.Server.Azure;

public class AzureBlobServiceOptions
{

    internal readonly List<PrefixMapping> Mappings = new List<PrefixMapping>();
    

    public AzureBlobServiceOptions MapPrefix(string urlPrefix, string container)
        => MapPrefix(urlPrefix, container, "");

    public AzureBlobServiceOptions MapPrefix(string urlPrefix, string container, bool ignorePrefixCase, bool lowercaseBlobPath)
        => MapPrefix(urlPrefix, container, "", ignorePrefixCase, lowercaseBlobPath);
            
    public AzureBlobServiceOptions MapPrefix(string urlPrefix, string container, string blobPrefix)
        => MapPrefix(urlPrefix, container, blobPrefix, false, false);
    public AzureBlobServiceOptions MapPrefix(string urlPrefix, string container, string blobPrefix, bool ignorePrefixCase, bool lowercaseBlobPath)
    {
        var prefix = urlPrefix.TrimStart('/').TrimEnd('/');
        if (prefix.Length == 0)
        {
            throw new ArgumentException("Prefix cannot be /", nameof(prefix));
        }

        prefix = '/' + prefix + '/';

        blobPrefix = blobPrefix.Trim('/');


        Mappings.Add(new PrefixMapping()
        {
            Container = container, 
            BlobPrefix = blobPrefix, 
            UrlPrefix = prefix, 
            IgnorePrefixCase = ignorePrefixCase,
            LowercaseBlobPath = lowercaseBlobPath
        });
        return this;
    }
}