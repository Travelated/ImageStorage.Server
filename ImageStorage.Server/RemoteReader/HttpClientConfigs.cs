using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;

namespace ImageStorage.Server.RemoteReader;

internal static class HttpClientConfigs
{
    static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        var delay = Backoff.DecorrelatedJitterBackoffV2(medianFirstRetryDelay:
            TimeSpan.FromMilliseconds(300), retryCount: 3);
        
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(delay);
    }

    static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .AdvancedCircuitBreakerAsync(
                failureThreshold: 0.5, // Break on >=50% actions result in handled exceptions...
                samplingDuration: TimeSpan.FromSeconds(10), // ... over any 10 second period
                minimumThroughput: 8, // ... provided at least 8 actions in the 10 second period.
                durationOfBreak: TimeSpan.FromSeconds(30) // Break for 30 seconds.
            );
    }
    
    public static IHttpClientBuilder AddRetryPolicies(this IHttpClientBuilder clientBuilder)
    {
        return clientBuilder
            .AddPolicyHandler(GetRetryPolicy());
        //.AddPolicyHandler(GetCircuitBreakerPolicy());
    }
}