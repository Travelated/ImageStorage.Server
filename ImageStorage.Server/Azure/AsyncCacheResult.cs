using Imazen.Common.Extensibility.StreamCache;

namespace ImageStorage.Server.Azure;

public enum AsyncCacheDetailResult
{
    Unknown = 0,
    MemoryHit,
    DiskHit,
    WriteSucceeded,
    QueueLockTimeoutAndCreated,
    FileAlreadyExists,
    Miss,
    CacheEvictionFailed,
    WriteTimedOut,
    QueueLockTimeoutAndFailed,
    EvictAndWriteLockTimedOut,
    ContendedDiskHit
}
public class AsyncCacheResult : IStreamCacheResult
{
    public required Stream Data { get; set; }
    public required string ContentType { get; set; }
    public string Status => Detail.ToString();

    public required AsyncCacheDetailResult Detail { get; set; } = AsyncCacheDetailResult.Miss;
}