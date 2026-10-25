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
    /// server module checks the location on. For an encounter the server module credits itself, a made-up
    /// id past every DBC id.
    /// </summary>
    public int Id { get; set; }
    public string Name { get; set; }
    public ExtractedMap Map { get; set; }
    /// <summary>
    /// The one team that can fight the boss: <c>1</c> Alliance, <c>2</c> Horde, <c>0</c> when both can.
    /// </summary>
    public int Factions { get; set; }
    /// <summary>
    /// The highest level among the creatures fought in the encounter.
    /// </summary>
    public int Level { get; set; }
}

public class ExtractedMap
{
    public int Id { get; set; }
    public string Name { get; set; }
}

/// <summary>
/// Builds the dungeon boss table archipelawow ships: the 5-player dungeon encounters of the client's
/// DungeonEncounter.dbc that the core credits through the world database's instance_encounters, and the
/// few the server module credits itself.
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
    ];

    // Encounters credited for one boss out of several a run picks from at random.
    private static readonly HashSet<int> RANDOM_BOSS_ENCOUNTERS = [
        // The Violet Hold's First and Second Prisoner: credited for Erekem and Moragg only, two of the six
        // prisoners a run releases two of
        541, 543,
    ];

    // Upper Blackrock Spire's encounters, which share Blackrock Spire's map with Lower Blackrock Spire's. It is
    // a 10-player raid in 3.3.5, and no dungeon achievement asks for it.
    private static readonly HashSet<int> UPPER_BLACKROCK_SPIRE_ENCOUNTERS = [276, 277, 278, 279, 280];

    // instance_encounters.creditType, EncounterCreditType in Map.h
    private const byte ENCOUNTER_CREDIT_KILL_CREATURE = 0;

    // The creatures fought in the encounters the creature the core credits does not stand for: the ones it
    // credits through a spell a script casts, the groups whose members are not all of the same level, and
    // the ones the server module credits itself.
    private static readonly Dictionary<int, int[]> FOUGHT_CREATURES = new()
    {
        // The Seven: Anger'rel, the one credited, and Hate'rel are a level below the five others
        [243] = [9034, 9035, 9036, 9037, 9038, 9039, 9040],
        [296] = [26533], // Mal'Ganis
        [375] = [26632], // The Prophet Tharon'ja
        // Tribunal of Ages: the Dark Rune adds the event sends. The three faces and Brann cannot be attacked.
        [567] = [27983, 27984, 27985],
        // Sergeant Bly and his crew
        [100000] = [7604, 7605, 7606, 7607, 7608],
        [100001] = [7023], // Obsidian Sentinel
        // Atal'ai Defenders: Zolo, Gasher, Loro, Hukku, Zul'Lor and Mijan
        [100002] = [5712, 5713, 5714, 5715, 5716, 5717],
        // Trial of the Champion and Halls of Reflection, which ArchipelaWoW does not use, all credited through a spell.
        // Grand Champions: the Horde champions the Alliance faces, and the Alliance champions the Horde faces
        [334] = [35569, 35570, 35571, 35572, 35617, 34657, 34701, 34702, 34703, 34705],
        [338] = [34928, 35119], // Argent Champion: Argent Confessor Paletress or Eadric the Pure
        [340] = [35451], // The Black Knight
        // Escaped from Arthas: the undead the Lich King sends after the party
        [843] = [36940, 36941, 37069],
    };

    // ExtractedBossData.Factions
    private const int FACTION_ANY = 0;
    private const int FACTION_HORDE = 2;

    // Encounters only one team can fight, as the other is friendly with the boss.
    private static readonly Dictionary<int, int> ONE_FACTION_ENCOUNTERS = new()
    {
        // The Lost Dwarves: Baelog, Eric and Olaf are Ironforge dwarves
        [548] = FACTION_HORDE,
    };

    // Encounters DungeonEncounter.dbc has no row for, which the server module credits itself under an id
    // past every DBC id (the highest is 894). The OrderIndex places one among its dungeon's encounters.
    private static readonly List<(int Id, string Name, int MapID, int OrderIndex)> SYNTHETIC_ENCOUNTERS = [
        // Sergeant Bly: once the Zul'Farrak pyramid event is over, between Shadowpriest Sezz'ziz and Chief
        // Ukorz Sandscalp, his crew turns on the party when a player tells him to. The module credits it
        // when Bly (7604) dies.
        (100000, "Sergeant Bly", 209, 6500),
        // Obsidian Sentinel: between Ironaya and the Ancient Stone Keeper in Uldaman. The module credits it
        // when the Sentinel (7023) dies.
        (100001, "Obsidian Sentinel", 70, 3000),
        // Atal'ai Defenders: the six trolls on the Sunken Temple's balconies, whose deaths open the forcefield
        // before Jammal'an the Prophet, so right before him. The module credits it when the last of them dies.
        (100002, "Atal'ai Defenders", 109, 250),
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

        var instanceEncounters = await db.InstanceEncounters.ToListAsync();
        var credited = instanceEncounters.Select(encounter => (int)encounter.Entry).ToHashSet();
        var creditedCreatures = instanceEncounters
            .Where(encounter => encounter.CreditType == ENCOUNTER_CREDIT_KILL_CREATURE)
            .ToDictionary(encounter => (int)encounter.Entry, encounter => (int)encounter.CreditEntry);
        var creatureLevels = await db.CreatureTemplates
            .Select(creature => new { creature.Entry, creature.Maxlevel })
            .ToDictionaryAsync(creature => (int)creature.Entry, creature => (int)creature.Maxlevel);

        int EncounterLevel(int encounterId, string name)
        {
            int[] creatures = FOUGHT_CREATURES.TryGetValue(encounterId, out var fought) ? fought
                : creditedCreatures.TryGetValue(encounterId, out int credit) ? [credit]
                : [];
            int level = creatures.Select(creature => creatureLevels.GetValueOrDefault(creature)).DefaultIfEmpty().Max();
            if (level == 0)
            {
                logger.LogWarning("No level for \"{name}\" ({id}): list the creatures fought in FOUGHT_CREATURES.", name, encounterId);
            }
            return level;
        }

        var dbcEncounters = encounters
            .Where(encounter => encounter.Difficulty == DUNGEON_DIFFICULTY_NORMAL && maps.Get(encounter.MapID)?.InstanceType == MAP_INSTANCE)
            .Where(encounter => FilterEncounter(encounter, "never credited", e => credited.Contains(e.ID)))
            .Where(encounter => FilterEncounter(encounter, "summoned from outside the dungeon", e => !SUMMONED_FROM_OUTSIDE_ENCOUNTERS.Contains(e.ID)))
            .Where(encounter => FilterEncounter(encounter, "random boss", e => !RANDOM_BOSS_ENCOUNTERS.Contains(e.ID)))
            .Where(encounter => FilterEncounter(encounter, "raid", e => !UPPER_BLACKROCK_SPIRE_ENCOUNTERS.Contains(e.ID)))
            .Select(encounter => (Id: encounter.ID, Name: NAME_FIXES.GetValueOrDefault(encounter.ID, encounter.NameLang), encounter.MapID, encounter.OrderIndex));

        List<ExtractedBossData> bosses = [.. dbcEncounters
            .Concat(SYNTHETIC_ENCOUNTERS)
            .OrderBy(encounter => encounter.MapID)
            .ThenBy(encounter => encounter.OrderIndex)
            .Select(encounter => new ExtractedBossData()
            {
                Id = encounter.Id,
                Name = encounter.Name,
                Map = new ExtractedMap()
                {
                    Id = encounter.MapID,
                    Name = maps.Get(encounter.MapID).MapNameLang,
                },
                Factions = ONE_FACTION_ENCOUNTERS.GetValueOrDefault(encounter.Id, FACTION_ANY),
                Level = EncounterLevel(encounter.Id, encounter.Name),
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
