namespace ImageStorage.Server.Extensions;

public static class AppSettingsExtensions
{
    public static void AddAppSettingsLocal(this WebApplicationBuilder builder, string[]? commandLineArgs)
    {
        builder.Configuration.Sources.Clear();

        var env = builder.Environment;

        builder.Configuration
            .SetBasePath(env.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true) //load base settings
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true) //load local settings
            .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true) //load environment settings
            .AddEnvironmentVariables();

        if (commandLineArgs != null)
        {
            builder.Configuration.AddCommandLine(commandLineArgs);
        }
    }
}