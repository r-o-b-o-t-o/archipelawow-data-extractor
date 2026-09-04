using ArchipelaWoW.DataExtractor.Extensions;
using ArchipelaWoW.DataExtractor.Services;
using dotenv.net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ArchipelaWoW.DataExtractor;

public static class Program
{
    public static async Task Main(string[] args)
    {
        string envFile = FindEnvFile();
        if (envFile != null)
        {
            DotEnv.Load(new DotEnvOptions(envFilePaths: [envFile]));
        }

        using var host = Host
            .CreateDefaultBuilder(args)
            .ConfigureServices(ConfigureServices)
            .Build();

        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<QuestExtractorService>().ExtractQuests();
        await scope.ServiceProvider.GetRequiredService<SpellExtractorService>().ExtractSpells();
    }

    /// <summary>
    /// Walks up from the working directory, then from the build output, looking for a .env file.
    /// A debug build runs out of bin/Debug/net10.0, several levels below the file it needs.
    /// </summary>
    private static string FindEnvFile()
    {
        return SearchUpwards(Directory.GetCurrentDirectory()) ?? SearchUpwards(AppContext.BaseDirectory);

        static string SearchUpwards(string start)
        {
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, ".env");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            return null;
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services
            .AddLogging(builder =>
            {
                builder.AddSimpleConsole(console =>
                {
                    console.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ";
                    console.IncludeScopes = true;
                    console.SingleLine = true;
                });

                builder.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
            })
            .AddDbcReader()
            .AddDbcContainers()
            .AddWorldDbContext()
            .AddRepositories()
            .AddTransient<QuestExtractorService>()
            .AddTransient<SpellExtractorService>();
    }
}
