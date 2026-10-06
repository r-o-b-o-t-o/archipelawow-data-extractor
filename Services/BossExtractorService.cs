using System.Text.Encodings.Web;
using System.Text.Json;
using ArchipelaWoW.DataExtractor.Dbc;
using ArchipelaWoW.DataExtractor.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArchipelaWoW.DataExtractor.Services;

public class ExtractedBossData
{
    /// <summary>
    /// The DungeonEncounter.dbc id, which is what the core credits when the boss goes down and what the
    /// server module checks the location on.
    /// </summary>
    public int Id { get; set; }
    public string Name { get; set; }
    public ExtractedMap Map { get; set; }
}

public class ExtractedMap
{
    public int Id { get; set; }
    public string Name { get; set; }
}

/// <summary>
/// Builds the dungeon boss table archipelawow ships: the 5-player dungeon encounters of the client's
/// DungeonEncounter.dbc that the core credits through the world database's instance_encounters.
/// </summary>
public class BossExtractorService(
        ILogger<BossExtractorService> logger,
        WorldDbContext db,
        DungeonEncounterContainer encounters,
        MapContainer maps
    )
{
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // Map.InstanceType, see DBCEnums.h
    private const int MAP_INSTANCE = 1;

    // DungeonEncounter.Difficulty, see DBCEnums.h. A heroic encounter is a row of its own with an id of
    // its own, and the locations are only meant for normal mode.
    private const int DUNGEON_DIFFICULTY_NORMAL = 0;

    // Bosses summoned with an item that comes from outside their dungeon, which the dungeon's zone item
    // alone does not lead to.
    private static readonly HashSet<int> SUMMONED_FROM_OUTSIDE_ENCOUNTERS = [
        // Kirtonos the Herald: the Blood of Innocents comes from the quest "Kirtonos the Herald", given in
        // the Western Plaguelands, and only drops in Scholomance once that quest has been turned in
        451,
        // Avatar of Hakkar: the Egg of Hakkar comes from the quest "The God Hakkar", given in Tanaris
        492,
        // Gahz'rilla: the Mallet of Zul'Farrak is made from the Sacred Mallet, dropped in the Hinterlands
        594,
    ];

    // Encounters credited for one boss out of several a run picks from at random.
    private static readonly HashSet<int> RANDOM_BOSS_ENCOUNTERS = [
        // The Violet Hold's First and Second Prisoner: credited for Erekem and Moragg only, two of the six
        // prisoners a run releases two of
        541, 543,
    ];

    // Names DungeonEncounter.dbc misspells, by encounter id
    private static readonly Dictionary<int, string> NAME_FIXES = new()
    {
        [294] = "Salramm the Fleshcrafter",
        [483] = "Ramstein the Gorger",
        [573] = "Skarvald & Dalronn",
        [594] = "Gahz'rilla",
        [837] = "Overlord Tyrannus",
    };

    public async Task ExtractBosses()
    {
        string outDir = OutputDirectory.Prepare();

        var credited = (await db.InstanceEncounters.Select(encounter => encounter.Entry).ToListAsync()).Select(entry => (int)entry).ToHashSet();

        List<ExtractedBossData> bosses = [.. encounters
            .Where(encounter => encounter.Difficulty == DUNGEON_DIFFICULTY_NORMAL && maps.Get(encounter.MapID)?.InstanceType == MAP_INSTANCE)
            .Where(encounter => FilterEncounter(encounter, "never credited", e => credited.Contains(e.ID)))
            .Where(encounter => FilterEncounter(encounter, "summoned from outside the dungeon", e => !SUMMONED_FROM_OUTSIDE_ENCOUNTERS.Contains(e.ID)))
            .Where(encounter => FilterEncounter(encounter, "random boss", e => !RANDOM_BOSS_ENCOUNTERS.Contains(e.ID)))
            .OrderBy(encounter => encounter.MapID)
            .ThenBy(encounter => encounter.OrderIndex)
            .Select(encounter => new ExtractedBossData()
            {
                Id = encounter.ID,
                Name = NAME_FIXES.GetValueOrDefault(encounter.ID, encounter.NameLang),
                Map = new ExtractedMap()
                {
                    Id = encounter.MapID,
                    Name = maps.Get(encounter.MapID).MapNameLang,
                },
            })];

        string outFile = Path.Combine(outDir, "bosses.json");
        await File.WriteAllTextAsync(outFile, JsonSerializer.Serialize(bosses, jsonOptions));

        logger.LogInformation("Wrote {count} bosses to {file}.", bosses.Count, outFile);
    }

    private bool FilterEncounter(DungeonEncounter encounter, string reason, Func<DungeonEncounter, bool> fn)
    {
        bool result = fn(encounter);
        if (!result && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Filtered out \"{name}\" ({id}): {reason}", encounter.NameLang, encounter.ID, reason);
        }
        return result;
    }
}
