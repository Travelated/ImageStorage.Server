namespace ImageStorage.Server.Middleware;

public class RedirectMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SeoConfig _seoConfig;
    
    public RedirectMiddleware(RequestDelegate next, SeoConfig seoConfig)
    {
        _next = next;
        _seoConfig = seoConfig;
    }
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path == "/")
        {
            context.Response.Redirect(_seoConfig.HostName, permanent: true, preserveMethod: false);
            return;
        }
        
        await _next(context);
    }
}