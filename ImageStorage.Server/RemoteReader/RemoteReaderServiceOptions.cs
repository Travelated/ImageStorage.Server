namespace ImageStorage.Server.RemoteReader;

public record RemoteReaderServiceOptions
{
    public required string SigningKey { get; init; }
    public required string Prefix { get; init; }
}