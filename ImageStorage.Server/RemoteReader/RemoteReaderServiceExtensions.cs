using Imazen.Common.Storage;

namespace ImageStorage.Server.RemoteReader;

public static class RemoteReaderServiceExtensions
{
    public static IServiceCollection AddImageflowRemoteReaderService(this IServiceCollection services, 
        RemoteReaderServiceOptions options, Action<HttpClient> configureClient)
    {
        services.AddSingleton(options);
        services.AddScoped<IBlobProvider>(s => s.GetRequiredService<RemoteReaderService>());
        services.AddHttpClient<RemoteReaderService>(configureClient);

        return services;
    }
}