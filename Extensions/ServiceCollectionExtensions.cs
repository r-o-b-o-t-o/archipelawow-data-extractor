using ArchipelaWoW.DataExtractor.Dbc;
using ArchipelaWoW.DataExtractor.Entities;
using ArchipelaWoW.DataExtractor.Services;
using ArchipelaWoW.DataExtractor.Services.Repositories;
using DBDefsLib;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Roboto.Dbc.Reader;

namespace ArchipelaWoW.DataExtractor.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWorldDbContext(this IServiceCollection services)
    {
        // A builder rather than string concatenation: a password holding a ';' or a '=' would
        // otherwise be read as the end of the value and corrupt every setting after it.
        var connStrBuilder = new MySqlConnectionStringBuilder()
        {
            Server = Env.GetString("WORLD_DB_HOST") ?? "localhost",
            Port = (uint)(Env.GetInt("WORLD_DB_PORT") ?? 3306),
            UserID = Env.GetString("WORLD_DB_USER") ?? "acore",
            Password = Env.GetString("WORLD_DB_PASSWORD") ?? "acore",
            Database = Env.GetString("WORLD_DB_DATABASE") ?? "acore_world",
        };
        string worldDbConnStr = connStrBuilder.ConnectionString;

        ServerVersion version;
        try
        {
            version = ServerVersion.AutoDetect(worldDbConnStr);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Could not reach the world database \"{connStrBuilder.Database}\" at " +
                $"{connStrBuilder.Server}:{connStrBuilder.Port} as \"{connStrBuilder.UserID}\". " +
                "Check the WORLD_DB_* environment variables.", ex);
        }

        return services.AddMySql<WorldDbContext>(worldDbConnStr, version);
    }

    private static DbcReader CreateDbcReader()
    {
        string dbcDir = Env.GetString("DBC_DIRECTORY");
        if (string.IsNullOrWhiteSpace(dbcDir) || !Directory.Exists(dbcDir))
        {
            throw new InvalidOperationException("DBC directory not found. Set the DBC_DIRECTORY environment variable to a valid directory.");
        }

        Build dbcBuild = new(Env.GetString("DBC_BUILD") ?? "3.3.5.12340");
        DbcLocale dbcLocale = Enum.Parse<DbcLocale>(Env.GetString("DBC_LOCALE") ?? "EnUS", ignoreCase: true);
        DbcReader dbcReader = new(dbcBuild, dbcLocale, dbcDir);

        return dbcReader;
    }

    public static IServiceCollection AddDbcReader(this IServiceCollection services)
    {
        // Singleton to match the containers below: a scoped reader would be a captive dependency and
        // would throw as soon as the host runs with scope validation on.
        return services.AddSingleton((_) => CreateDbcReader());
    }

    public static IServiceCollection AddDbcContainers(this IServiceCollection services)
    {
        return services
            .AddSingleton<AreaTableContainer>()
            .AddSingleton<ChrClassesContainer>()
            .AddSingleton<ChrRacesContainer>()
            .AddSingleton<QuestInfoContainer>()
            .AddSingleton<QuestSortContainer>()
            .AddSingleton<WorldMapAreaContainer>()
            .AddSingleton<FactionTemplateContainer>()
            .AddSingleton<SpellContainer>()
            .AddSingleton<SkillLineContainer>()
            .AddSingleton<SkillLineAbilityContainer>();
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        return services
            .AddScoped<QuestTemplateRepository>()
            .AddScoped<DisablesRepository>()
            .AddScoped<TrainerRepository>();
    }
}
