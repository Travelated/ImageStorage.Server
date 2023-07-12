using Microsoft.Extensions.Options;

namespace ImageStorage.Server.Middleware;

public class RobotsTxtMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SeoConfig _seoConfig;

    public RobotsTxtMiddleware(RequestDelegate next, SeoConfig seoConfig)
    {
        _next = next;
        _seoConfig = seoConfig;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path == "/robots.txt")
        {
            string output = $@"User-agent: *
Allow: /

Sitemap: {_seoConfig.SiteMap}
Host: {_seoConfig.HostName}";
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(output);
        }
        else await _next(context);
    }
}