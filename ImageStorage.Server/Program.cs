using Azure.Identity;
using Imageflow.Fluent;
using Imageflow.Server;
using ImageStorage.Server;
using ImageStorage.Server.Azure;
using ImageStorage.Server.Controllers;
using ImageStorage.Server.Extensions;
using ImageStorage.Server.Middleware;
using ImageStorage.Server.RemoteReader;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.Azure;
using Microsoft.OpenApi.Models;
using Sentry.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddAppSettingsLocal(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter your token in the text input below.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer", // Lowercase 'bearer' to follow the convention
        BearerFormat = "JWT" // This property will instruct Swagger UI to append the 'Bearer' prefix
    });
    c.OperationFilter<SwaggerFileOperationFilter>();
    
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header
            },
            new List<string>()
        }
    });
});

RemoteReaderServiceOptions options =
    builder.Configuration
        .GetSection("RemoteReader").Get<RemoteReaderServiceOptions>() 
    ?? throw new Exception("RemoteReader not configured");
var imageServerConfig = builder.Configuration.GetSection("ImageServerConfig").Get<ImageServerConfig>()
                        ?? throw new Exception("ImageServerConfig not configured");

var seoConfig = builder.Configuration.GetSection("SeoConfig").Get<SeoConfig>()
                ?? throw new Exception("SeoConfig not configured");

var azureConfig = builder.Configuration.GetSection("AzureUpload").Get<AzureUploadConfig>();

if (azureConfig != null)
{
    builder.Services.AddSingleton(azureConfig);
}

if (azureConfig?.Enabled == true)
{
    Console.WriteLine($"Azure storage: {builder.Configuration.GetSection("AzureStorage")["ServiceUri"]}");
    builder.Services.AddAzureClients(clientBuilder =>
    {
        var config = builder.Configuration.GetSection("AzureStorage");
        
        // Add a Storage account client
        clientBuilder.AddBlobServiceClient(config);

        // Use DefaultAzureCredential by default
        clientBuilder.UseCredential(new DefaultAzureCredential(new DefaultAzureCredentialOptions()
        {
            ExcludeManagedIdentityCredential = false, 
            ExcludeVisualStudioCredential = true,
            ExcludeAzurePowerShellCredential = true,
            ExcludeSharedTokenCacheCredential = true,
            ExcludeAzureCliCredential = false,
            ExcludeEnvironmentCredential = false,
        }));
    });
    
    builder.Services.AddImageflowAzureBlobService(
        new AzureBlobServiceOptions()
            .MapPrefix("/storage", azureConfig.Container));
}


Console.WriteLine($"Domain signature 'localhost': {RemoteReaderUrlBuilder.GetDomainSignature("localhost", options.SigningKey)}");

string nextJsAccessKey = builder.Configuration["NextJsAccessKey"] 
                         ?? throw new Exception("NextJsAccessKey not configured");

builder.Services.AddImageflowRemoteReaderService(options, c =>
{
    c.DefaultRequestHeaders.Add("X-Auth-Access-Key", nextJsAccessKey);
});


void sentryOptions(SentryAspNetCoreOptions o)
{
    o.Dsn = builder.Configuration["Sentry"] ?? "";
    o.MinimumEventLevel = LogLevel.Error;
    o.Debug = false;
}

builder.WebHost.UseSentry(sentryOptions);


var app = builder.Build();


var imageflow = new ImageflowMiddlewareOptions()
    .SetMapWebRoot(false)
    .SetMyOpenSourceProjectUrl("https://github.com/Chaika-Tech/ImageStorage.Server")
    // Cache publicly (including on shared proxies and CDNs) for 30 days
    .SetDefaultCacheControlString("public, max-age=2592000")
    .SetJobSecurityOptions(new SecurityOptions()
        .SetMaxDecodeSize(new FrameSizeLimit(10000,10000, 100))
        .SetMaxFrameSize(new FrameSizeLimit(10000,10000, 100))
        .SetMaxEncodeSize(new FrameSizeLimit(4000,4000, 20)));

if (!string.IsNullOrEmpty(imageServerConfig.DashboardPassword))
{
    imageflow.SetDiagnosticsPageAccess(AccessDiagnosticsFrom.AnyHost)
        .SetDiagnosticsPagePassword(imageServerConfig.DashboardPassword);
}
else
{
    imageflow.SetDiagnosticsPageAccess(AccessDiagnosticsFrom.None);
}

if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<RobotsTxtMiddleware>(seoConfig);

var rewriteOptions = new RewriteOptions()
    .AddRedirect("/", $"{seoConfig.HostName}", 301);

app.UseRewriter(rewriteOptions);

app.UseAuthorization();
app.MapControllers();

app.UseImageflow(imageflow);

app.Run();