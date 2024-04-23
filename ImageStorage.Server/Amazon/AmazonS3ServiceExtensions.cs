using Amazon.S3;
using ImageStorage.Server.Azure;
using ImageStorage.Server.Controllers;
using Imazen.Common.Extensibility.StreamCache;
using Imazen.Common.Storage;

namespace ImageStorage.Server.Amazon;

public static class AmazonS3ServiceExtensions
{
    public static IServiceCollection AddImageflowAmasonBlobService(this IServiceCollection services,
        AzureBlobServiceOptions options, AmazonS3Credentials credentials)
    {
        IAmazonS3 client = new AmazonS3Client(credentials.AccessKey, credentials.SecretKey, new AmazonS3Config
        {
            ServiceURL = credentials.HostName
        });
        services.AddSingleton(client);
        services.AddSingleton<AmazonBlobService>();
        services.AddSingleton<IBlobProvider>(s => s.GetRequiredService<AmazonBlobService>());
        services.AddSingleton<IUploadBlobStorage>(s => s.GetRequiredService<AmazonBlobService>());
        services.AddSingleton<IStreamCache, AmazonBlobCache>();
        services.AddSingleton(options);

        return services;
    }
}