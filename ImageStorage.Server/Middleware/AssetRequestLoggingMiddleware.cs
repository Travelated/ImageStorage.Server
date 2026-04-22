namespace ImageStorage.Server.Middleware;

public class AssetRequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AssetRequestLoggingMiddleware> _logger;

    public AssetRequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<AssetRequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;

        try
        {
            await _next(context);
        }
        finally
        {
            _logger.LogInformation(
                "Request {Method} {Path} responded {StatusCode}. RequesterHost: {RequesterHost}. RequestHost: {RequestHost}. UserAgent: {UserAgent}",
                context.Request.Method,
                path.Value ?? "/",
                context.Response.StatusCode,
                GetRequesterHost(context.Request),
                GetRequestHost(context.Request),
                GetUserAgent(context.Request));
        }
    }

    private static string GetRequesterHost(HttpRequest request)
    {
        if (TryGetHostFromHeader(request.Headers["Referer"].ToString(), out var refererHost))
        {
            return refererHost;
        }

        if (TryGetHostFromHeader(request.Headers["Origin"].ToString(), out var originHost))
        {
            return originHost;
        }

        return "unknown";
    }

    private static string GetRequestHost(HttpRequest request)
    {
        if (request.Host.HasValue)
        {
            return request.Host.Value;
        }

        var forwardedHost = request.Headers["X-Forwarded-Host"].ToString();
        return string.IsNullOrWhiteSpace(forwardedHost) ? "unknown" : forwardedHost;
    }

    private static string GetUserAgent(HttpRequest request)
    {
        var userAgent = request.Headers["User-Agent"].ToString();
        return string.IsNullOrWhiteSpace(userAgent) ? "unknown" : userAgent;
    }

    private static bool TryGetHostFromHeader(string headerValue, out string host)
    {
        host = string.Empty;
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return false;
        }

        if (!Uri.TryCreate(headerValue, UriKind.Absolute, out var uri))
        {
            host = headerValue;
            return true;
        }

        host = uri.IsDefaultPort
            ? uri.Host
            : $"{uri.Host}:{uri.Port}";
        return true;
    }
}
