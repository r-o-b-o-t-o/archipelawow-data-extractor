using System.Text.Encodings.Web;
using System.Text.Json;
using ArchipelaWoW.DataExtractor.Dbc;
using ArchipelaWoW.DataExtractor.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArchipelaWoW.DataExtractor.Services;

public class ExtractedLeaderData
{
    /// <summary>The creature entry, which the server module checks the location on when the character greets it.</summary>
    public int Id { get; set; }
    public string Name { get; set; }

    /// <summary>The capital the leader stands in.</summary>
    public ExtractedArea Zone { get; set; }
}

/// <summary>Builds the faction leader table archipelawow ships: the leaders the character greets, and their capitals.</summary>
public class LeaderExtractorService(
        ILogger<LeaderExtractorService> logger,
        WorldDbContext db,
        AreaTableContainer areas
    )
{
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // By creature entry, the Alliance's then the Horde's
    private static readonly uint[] LEADERS = [29611, 2784, 7999, 17468, 4949, 3057, 10181, 16802];

    public async Task ExtractLeaders()
    {
        string outDir = OutputDirectory.Prepare();

        var names = await db.CreatureTemplates.Where(c => LEADERS.Contains(c.Entry)).ToDictionaryAsync(c => c.Entry, c => c.Name);
        var zones = (await db.Creatures.Where(c => LEADERS.Contains(c.Id) && c.ZoneId != 0).Select(c => new { c.Id, c.ZoneId }).ToListAsync())
            .ToLookup(c => c.Id, c => (int)c.ZoneId);

        List<ExtractedLeaderData> leaders = [];
        foreach (uint leader in LEADERS)
        {
            if (!zones[leader].Any())
            {
                logger.LogWarning("Found no spawn with a zone for the faction leader {id}.", leader);
                continue;
            }

            leaders.Add(new ExtractedLeaderData()
            {
                Id = (int)leader,
                Name = names.GetValueOrDefault(leader),
                Zone = ExtractedArea.FromAreaTable(areas.Get(zones[leader].First())),
            });
        }

        string outFile = Path.Combine(outDir, "leaders.json");
        await File.WriteAllTextAsync(outFile, JsonSerializer.Serialize(leaders, jsonOptions));

        logger.LogInformation("Wrote {count} faction leaders to {file}.", leaders.Count, outFile);
    }
}
