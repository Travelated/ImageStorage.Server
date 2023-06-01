using Imageflow.Server;
using ImageStorage.Server.Extensions;
using ImageStorage.Server.RemoteReader;

var builder = WebApplication.CreateBuilder(args);
builder.AddAppSettingsLocal(args);

RemoteReaderServiceOptions options =
    builder.Configuration
        .GetSection("RemoteReader").Get<RemoteReaderServiceOptions>() 
    ?? throw new Exception("RemoteReader not configured");

string nextJsAccessKey = builder.Configuration["NextJsAccessKey"] 
                         ?? throw new Exception("NextJsAccessKey not configured");

builder.Services.AddImageflowRemoteReaderService(options, c =>
{
    c.DefaultRequestHeaders.Add("X-Imageflow-Access-Key", nextJsAccessKey);
});

var app = builder.Build();

app.UseImageflow(new ImageflowMiddlewareOptions()
    .SetMapWebRoot(false)
    .SetMyOpenSourceProjectUrl("https://github.com/Chaika-Tech/ImageStorage.Server"));

app.Run();