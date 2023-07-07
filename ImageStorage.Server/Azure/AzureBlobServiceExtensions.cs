using Imazen.Common.Extensibility.StreamCache;
using Imazen.Common.Storage;

namespace ImageStorage.Server.Azure;

public static class AzureBlobServiceExtensions
{

    public static IServiceCollection AddImageflowAzureBlobService(this IServiceCollection services,
        AzureBlobServiceOptions options)
    {
        services.AddSingleton<IBlobProvider, AzureBlobService>();
        services.AddSingleton<IStreamCache, AzureBlobCache>();
        services.AddSingleton(options);

        return services;
    }


}