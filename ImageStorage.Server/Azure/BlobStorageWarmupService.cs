using Azure.Storage.Blobs;

namespace ImageStorage.Server.Azure;

public class BlobStorageWarmupService : IHostedService, IDisposable
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<BlobStorageWarmupService> _logger;
    private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

    public BlobStorageWarmupService(BlobServiceClient blobServiceClient, ILogger<BlobStorageWarmupService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Fetch the service properties initially
            await FetchFirstBlobContainerAsync(cancellationToken);
            _logger.LogInformation("Blob Storage credentials updated");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Blob Storage service properties");
        }

        // Then fetch them every 10 minutes
        _ = RepeatEvery(TimeSpan.FromMinutes(10), cancellationToken);
    }

    private async Task RepeatEvery(TimeSpan delay, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(delay, cancellationToken);
                await FetchFirstBlobContainerAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch Blob Storage service properties");
            }
        }
    }
    
    private async Task FetchFirstBlobContainerAsync(CancellationToken cancellationToken)
    {
        await foreach (var _ in _blobServiceClient.GetBlobContainersAsync(cancellationToken: cancellationToken))
        {
            // Simply break after fetching the first container
            break;
        }
    }


    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource.Cancel();

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _cancellationTokenSource.Dispose();
    }
}