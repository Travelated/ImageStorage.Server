namespace ImageStorage.Server;

public record ImageServerConfig
{
    public string? DashboardPassword { get; init; }
    
    public int RamCacheSizeMb { get; init; } = 100;
    public int DiskCacheSizeMb { get; init; } = 1000;
}