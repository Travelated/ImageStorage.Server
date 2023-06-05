namespace ImageStorage.Server.Azure;

internal struct PrefixMapping
{
    internal string UrlPrefix;
    internal string Container;
    internal string BlobPrefix;
    internal bool IgnorePrefixCase;
    internal bool LowercaseBlobPath; 
}