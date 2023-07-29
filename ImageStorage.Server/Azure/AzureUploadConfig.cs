namespace ImageStorage.Server.Azure;

public record AzureUploadConfig
{
    public required bool Enabled { get; init; }
    public required string Container { get; init; }
    public required string JwtKey { get; init; }
    public bool AllowDebug { get; init; } = false;

    /// <summary>
    /// Max dimension of the image
    /// </summary>
    public uint MaxSize { get; init; } = 3840;

    public string CacheContainer { get; init; } = "resize-cache";
}