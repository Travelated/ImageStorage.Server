namespace ImageStorage.Server.Middleware;

public class RobotsTxtMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public RobotsTxtMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var seoConfig = _configuration.GetSection("SeoConfig").Get<SeoConfig>()
                        ?? throw new Exception("SeoConfig not configured");

        if (context.Request.Path.StartsWithSegments("/robots.txt"))
        {
            string output = $@"User-agent: *
Allow: /

Sitemap: {seoConfig.SiteMap}
Host: {seoConfig.HostName}";
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(output);
        }
        else await _next(context);
    }
}