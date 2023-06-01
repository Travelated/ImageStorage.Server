namespace ImageStorage.Server.RemoteReader;

public record RemoteReaderServiceOptions
{
    public required string SigningKey { get; init; }
    public required string Prefix { get; init; }
    public bool UseHttp { get; init; } = false;
    
    public List<string>? WhitelistedDomains { get; init; }
}