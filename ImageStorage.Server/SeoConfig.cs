namespace ImageStorage.Server;

public record SeoConfig
{
    public required string HostName { get; init; }
    public required string SiteMap { get; init; }
}