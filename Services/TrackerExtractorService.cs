using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ArchipelaWoW.DataExtractor.Dbc;
using ArchipelaWoW.DataExtractor.Entities;
using ArchipelaWoW.DataExtractor.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace ArchipelaWoW.DataExtractor.Services;

/// <summary>
/// Writes what the launcher's tracker shows that archipelawow doesn't need: where its checks are on the world
/// maps, and the icons and images to draw them with. Covers the quests, spells, skills, bosses, subzones and faction
/// leaders of the extracts in OUT_DIR, which are therefore written first.
/// </summary>
public partial class TrackerExtractorService(
        ILogger<TrackerExtractorService> logger,
        WorldDbContext db,
        ClientTextures client,
        TrackerMapExtractorService mapExtractor,
        WorldMapGeometry geometry,
        MapContainer maps,
        AreaTriggerContainer areaTriggers,
        TaxiNodesContainer taxiNodes,
        DungeonEncounterContainer encounters,
        LFGDungeonsContainer lfgDungeons,
        SpellContainer spells,
        SpellIconContainer spellIcons,
        AchievementContainer achievements,
        AchievementCategoryContainer achievementCategories,
        AchievementCriteriaContainer achievementCriteria,
        WorldMapOverlayContainer overlays,
        ItemDisplayInfoContainer itemDisplays,
        ChrRacesContainer races,
        ChrClassesContainer classes,
        FactionTemplateContainer factionTemplates
    )
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private const int ICON_QUALITY = 85;

    // Map.InstanceType, see DBCEnums.h
    private const int MAP_INSTANCE = 1;

    // LFGDungeons.TypeID and .Flags, see LFGMgr.h. Heroic entries have a type of their own.
    private const int LFG_TYPE_DUNGEON = 1;
    private const int LFG_FLAG_SEASONAL = 0x4;

    // Achievement_Criteria.Type, see DBCEnums.h
    private const int ACHIEVEMENT_CRITERIA_TYPE_KILL_CREATURE = 0;

    // instance_encounters.creditType, EncounterCreditType in Map.h
    private const byte ENCOUNTER_CREDIT_KILL_CREATURE = 0;

    // creature_template.npcflag, UNIT_NPC_FLAG_FLIGHTMASTER
    private const uint NPC_FLAG_FLIGHT_MASTER = 0x2000;

    // How far from its taxi node a flight master may stand: a few yards for most, 23 at Amber Ledge
    private const float FLIGHT_MASTER_RANGE = 50;

    // ChrRaces.Flags, and ChrRaces.Alliance
    private const int CHR_RACE_FLAG_NOT_PLAYABLE = 0x1;
    private const int RACE_ALLIANCE = 0;
    private const int RACE_HORDE = 1;

    // The sides of a quest giver, when only one of them can talk to it
    private const int SIDE_ALLIANCE = 1;
    private const int SIDE_HORDE = 2;

    /// <summary>The most spots a quest lists, across its givers: one standing in dozens of places is a mob, and a few of them are enough.</summary>
    private const int MAX_GIVER_SPOTS = 12;

    // Achievement_Category.dbc: General, Quests, Exploration, Dungeons & Raids. archipelawow's achievements
    // come from these, and the exploration and dungeon ones lend their icons to its zone items.
    private static readonly int[] ACHIEVEMENT_CATEGORIES = [92, 96, 97, 168];

    // item_template.class and .Quality: glyphs, bags and heirlooms, the items archipelawow hands out
    private const byte ITEM_CLASS_CONTAINER = 1;
    private const byte ITEM_SUBCLASS_BAG = 0;
    private const byte ITEM_CLASS_GLYPH = 16;
    private const byte ITEM_QUALITY_HEIRLOOM = 7;

    // The consumables mod-i-found-your-sword repurposes for archipelawow's filler: the Scroll of Divination,
    // the Tome of Insight and the Enduring Soulstone. They keep their Item.dbc display, so the database's
    // rows give their icons whether or not the module's updates are applied.
    private static readonly uint[] CONSUMABLE_ITEMS = [4427, 1335, 2767];

    /// <summary>
    /// Icons for what the extracts carry none for: the tracker's progressive items, gold, random gear, world buffs, traps,
    /// Archipelago Stone unlocks and the Stone Shop, and the zone items whose icon no extracted achievement has (the zones of
    /// archipelawow's items/zones.py).
    /// </summary>
    private static readonly string[] TRACKER_ICONS = [
        "Ability_Rogue_Sprint",
        "Spell_Holy_BorrowedTime",
        "INV_Misc_Food_15",
        "INV_Drink_07",
        "Ability_Mount_RidingHorse",
        "INV_Misc_Coin_01",
        "INV_Sword_04",
        "INV_Chest_Chain_05",
        "INV_Jewelry_Ring_03",
        "INV_Misc_Head_Dragon_01",
        "Spell_Frost_Stun",
        "Spell_Nature_Slow",
        "Spell_Nature_Polymorph",
        "Spell_Nature_CorrosiveBreath",
        "Spell_Arcane_Blink",
        "Ability_Ambush",
        "Ability_Warrior_Disarm",
        "INV_Misc_Bomb_01",
        "INV_Misc_QuestionMark",
        "Achievement_Reputation_01",
        "INV_Misc_Coin_02",
        "Spell_Arcane_TeleportDarnassus",
        "Spell_Arcane_TeleportExodar",
        "Spell_Arcane_TeleportIronForge",
        "Spell_Arcane_TeleportSilvermoon",
        "Spell_Arcane_TeleportStormWind",
        "Spell_Arcane_TeleportThunderBluff",
        "Spell_Arcane_TeleportUnderCity",
        "Achievement_Zone_DragonBlight_02",
        "Achievement_Zone_ZulDrak_02",
        "Achievement_Boss_Murmur_01",
    ];

    /// <summary>
    /// The icons of the skills no weapon master sells, by SkillLine.dbc id: Defense. SkillLine.dbc gives every skill the
    /// same placeholder, and Defense's own spell wears Shadowmeld's.
    /// </summary>
    private static readonly Dictionary<int, string> SKILL_ICONS = new()
    {
        [95] = "Ability_Defend",
    };

    /// <summary>Interface textures the tracker draws its markers with, and the names they're written under.</summary>
    private static readonly Dictionary<string, string> INTERFACE_IMAGES = new()
    {
        ["quest"] = "Interface/GossipFrame/AvailableQuestIcon",
        ["flightmaster"] = "Interface/Minimap/Tracking/FlightMaster",
    };

    /// <summary>
    /// Where to show the dungeons whose area triggers mislead, by Map.dbc id: the spot of the door players expect on its
    /// zone's map. A trigger deep inside a cave or a mountain sits far from the door, or under another zone, one by a
    /// zone's border can take the zone across it, and the first of a few isn't the main door.
    /// </summary>
    private static readonly Dictionary<int, MapPosition> ENTRANCES = new()
    {
        // The Deadmines: the Moonbrook mine, in Westfall
        [36] = new(39, 0.425f, 0.717f),
        // Uldaman: its front door, in the Badlands, whose trigger lies under Loch Modan
        [70] = new(17, 0.444f, 0.12f),
        // Gnomeregan: its gate, in Dun Morogh
        [90] = new(27, 0.244f, 0.398f),
        // The Temple of Atal'Hakkar, in the Swamp of Sorrows
        [109] = new(38, 0.697f, 0.535f),
        // Razorfen Downs, in the Barrens, closer to Dustwallow Marsh's spawns
        [129] = new(11, 0.5091f, 0.9288f),
        // Blackrock Spire and Blackrock Depths: Blackrock Mountain's gate in the Burning Steppes, the one most players take
        [229] = new(29, 0.296f, 0.374f),
        [230] = new(29, 0.296f, 0.374f),
        // The Caverns of Time's: the caverns' mouth, in Tanaris
        [269] = new(161, 0.65f, 0.495f),
        [560] = new(161, 0.65f, 0.495f),
        [595] = new(161, 0.65f, 0.495f),
        // Stratholme: its meeting stone, by the main gate rather than the service entrance, in the Eastern Plaguelands
        [329] = new(23, 0.2709f, 0.1234f),
        // Maraudon: the cave in the Valley of Spears, in Desolace
        [349] = new(101, 0.29f, 0.625f),
        // Dire Maul: before its gate, rather than the wings' portals, in Feralas
        [429] = new(121, 0.591f, 0.4496f),
    };

    private sealed record Spawn(int Map, int Zone, int Area, float X, float Y);

    /// <summary>A spawn on a continent, and the map of the zone or city it stands in.</summary>
    private sealed record PlacedSpawn(Spawn Spawn, int WorldMapArea);

    public async Task ExtractTracker()
    {
        string outDir = Env.GetString("TRACKER_OUT_DIR");
        if (outDir == null)
        {
            logger.LogInformation("TRACKER_OUT_DIR is not set, skipping the tracker extracts.");
            return;
        }
        Directory.CreateDirectory(outDir);
        string dataDir = OutputDirectory.Prepare();

        mapExtractor.ExtractMaps(outDir);

        var spawns = await GetContinentSpawns();
        var icons = new HashSet<string>(TRACKER_ICONS.Select(IconName));

        var entrances = await ExtractDungeons(outDir, dataDir, spawns);
        await ExtractQuests(outDir, dataDir, entrances, spawns);
        await ExtractFlightPaths(outDir, spawns);
        await ExtractLeaders(outDir, dataDir, spawns, icons);
        ExtractSpells(outDir, dataDir, icons);
        ExtractSkills(outDir, dataDir, icons);
        ExtractExplorations(outDir, dataDir, icons);
        await ExtractAchievements(outDir, icons);
        await ExtractItems(outDir, icons);
        WriteIcons(outDir, icons);
        WriteInterfaceImages(outDir);
    }

    /// <summary>
    /// Every spawn on the continents placed on a map, to tell which zone a spot with no zone of its own is in by
    /// its closest one. The spawns around an instance's entrance are filed under the instance's zone, and left
    /// out: their zone has no map on the continent.
    /// </summary>
    private async Task<ILookup<int, PlacedSpawn>> GetContinentSpawns()
    {
        var creatures = await db.Creatures
            .Where(c => WorldMapGeometry.CONTINENT_MAPS.Contains(c.Map) && c.ZoneId != 0)
            .Select(c => new Spawn(c.Map, c.ZoneId, c.AreaId, c.PositionX, c.PositionY))
            .ToListAsync();
        var gameObjects = await db.Gameobjects
            .Where(g => WorldMapGeometry.CONTINENT_MAPS.Contains(g.Map) && g.ZoneId != 0)
            .Select(g => new Spawn(g.Map, g.ZoneId, g.AreaId, g.PositionX, g.PositionY))
            .ToListAsync();
        return creatures.Concat(gameObjects)
            .Select(spawn => (Spawn: spawn, Position: LocateSpawn(spawn)))
            .Where(placed => placed.Position != null)
            .Select(placed => new PlacedSpawn(placed.Spawn, placed.Position.Value.Area))
            .ToLookup(placed => placed.Spawn.Map);
    }

    private MapPosition? LocateSpawn(Spawn spawn) =>
        geometry.Locate(spawn.Map, spawn.Area, spawn.X, spawn.Y) ?? geometry.Locate(spawn.Map, spawn.Zone, spawn.X, spawn.Y);

    /// <summary>Where a point sits, on the map of the closest spawn's zone.</summary>
    private MapPosition? LocateNear(ILookup<int, PlacedSpawn> spawns, int map, float x, float y)
    {
        var closest = spawns[map].MinBy(placed => (placed.Spawn.X - x) * (placed.Spawn.X - x) + (placed.Spawn.Y - y) * (placed.Spawn.Y - y));
        return closest == null ? null : geometry.Project(closest.WorldMapArea, x, y);
    }

    /// <summary>
    /// The 5-player dungeons: their entrances, where the area triggers leading inside sit, their
    /// encounters, and the level the Dungeon Finder lets a character in from. Returns the entrances, for
    /// what stands inside to be shown there.
    /// </summary>
    private async Task<Dictionary<int, MapPosition>> ExtractDungeons(string outDir, string dataDir, ILookup<int, PlacedSpawn> spawns)
    {
        var teleports = await db.AreatriggerTeleports.ToListAsync();
        // Read from bosses.json rather than DungeonEncounter.dbc, which lacks the encounters the server module
        // credits itself. It is already in the dungeons' encounter order.
        var bossesByMap = ReadArray(dataDir, "bosses.json").ToLookup(node => node["map"]["id"].GetValue<int>(), node => node["id"].GetValue<int>());
        // The seasonal bosses' entries share their dungeon's map. A dungeon split into wings has an entry for each:
        // it takes the lowest.
        var minLevels = lfgDungeons
            .Where(d => d.TypeID == LFG_TYPE_DUNGEON && (d.Flags & LFG_FLAG_SEASONAL) == 0)
            .GroupBy(d => d.MapID)
            .ToDictionary(g => g.Key, g => g.Min(d => d.MinLevel));

        var result = new SortedDictionary<int, object>();
        var firstEntrances = new Dictionary<int, MapPosition>();
        foreach (var map in maps.Where(m => m.InstanceType == MAP_INSTANCE))
        {
            List<MapPosition> entrances = ENTRANCES.TryGetValue(map.ID, out var door) ? [door] : [.. teleports
                .Where(t => t.TargetMap == map.ID)
                .Select(t => areaTriggers.Get((int)t.Id))
                .Where(trigger => trigger != null && WorldMapGeometry.CONTINENT_MAPS.Contains(trigger.ContinentID))
                .Select(trigger => LocateNear(spawns, trigger.ContinentID, trigger.Pos[0], trigger.Pos[1]))
                .OfType<MapPosition>()];

            // A few instances have no trigger on a continent, the Caverns of Time's for one, which sit in
            // another instance: the corpse position is where a dead player is sent back to, by the entrance
            if (entrances.Count == 0 && WorldMapGeometry.CONTINENT_MAPS.Contains(map.CorpseMapID)
                && LocateNear(spawns, map.CorpseMapID, map.Corpse[0], map.Corpse[1]) is { } corpse)
            {
                entrances.Add(corpse);
            }

            if (entrances.Count == 0)
            {
                logger.LogWarning("Found no entrance for {dungeon} ({id}).", map.MapNameLang, map.ID);
                continue;
            }

            firstEntrances[map.ID] = entrances[0];
            result[map.ID] = new
            {
                Entrances = entrances.Select(e => e.ToJson()),
                Encounters = bossesByMap[map.ID],
                MinLevel = minLevels.TryGetValue(map.ID, out int minLevel) ? minLevel : (int?)null,
            };
        }

        Write(outDir, "dungeons.json", result);
        logger.LogInformation("Wrote {count} dungeons.", result.Count);
        return firstEntrances;
    }

    /// <summary>
    /// Where the quests of quests.json are picked up: their quest givers' spawns, or where they're turned in
    /// for the quests that only start from an item. And the level they can be taken on from.
    /// </summary>
    private async Task ExtractQuests(string outDir, string dataDir, Dictionary<int, MapPosition> entrances, ILookup<int, PlacedSpawn> spawns)
    {
        var quests = ReadArray(dataDir, "quests.json");
        var questIds = quests.Select(q => (uint)q["id"].GetValue<int>()).ToHashSet();

        var creatureStarters = (await db.CreatureQueststarters.Where(s => questIds.Contains(s.Quest)).Select(s => new { s.Id, s.Quest }).ToListAsync())
            .ToLookup(s => s.Quest, s => s.Id);
        var gameObjectStarters = (await db.GameobjectQueststarters.Where(s => questIds.Contains(s.Quest)).Select(s => new { s.Id, s.Quest }).ToListAsync())
            .ToLookup(s => s.Quest, s => s.Id);
        var creatureEnders = (await db.CreatureQuestenders.Where(s => questIds.Contains(s.Quest)).Select(s => new { s.Id, s.Quest }).ToListAsync())
            .ToLookup(s => s.Quest, s => s.Id);
        var gameObjectEnders = (await db.GameobjectQuestenders.Where(s => questIds.Contains(s.Quest)).Select(s => new { s.Id, s.Quest }).ToListAsync())
            .ToLookup(s => s.Quest, s => s.Id);

        var creatureIds = creatureStarters.Concat(creatureEnders).SelectMany(g => g).ToHashSet();
        var gameObjectIds = gameObjectStarters.Concat(gameObjectEnders).SelectMany(g => g).ToHashSet();
        var creatureSpawns = (await db.Creatures.Where(c => creatureIds.Contains(c.Id))
            .Select(c => new { c.Id, Spawn = new Spawn(c.Map, c.ZoneId, c.AreaId, c.PositionX, c.PositionY) }).ToListAsync())
            .ToLookup(c => c.Id, c => c.Spawn);
        var gameObjectSpawns = (await db.Gameobjects.Where(g => gameObjectIds.Contains(g.Id))
            .Select(g => new { g.Id, Spawn = new Spawn(g.Map, g.ZoneId, g.AreaId, g.PositionX, g.PositionY) }).ToListAsync())
            .ToLookup(g => g.Id, g => g.Spawn);
        var creatureSides = await GetCreatureSides(creatureIds);
        // The level the quest log shows a quest orange from (GetQuestDifficultyColor in FrameXML's UIParent.lua),
        // as archipelawow only expects it at its recommended level, two levels later. A quest whose level follows
        // the player's is never orange: it can be taken on from its minimum level.
        var ownLevels = (await db.QuestTemplates.Where(q => questIds.Contains(q.Id))
            .Select(q => new { q.Id, q.QuestLevel, q.MinLevel }).ToListAsync())
            .ToDictionary(q => q.Id, q => q.QuestLevel > 0 ? Math.Max(q.QuestLevel - 4, q.MinLevel) : q.MinLevel);
        var prerequisites = quests.ToDictionary(
            q => (uint)q["id"].GetValue<int>(),
            q => (All: Ids(q["requiresAll"]), Any: Ids(q["requiresAny"])));
        var doableLevels = new Dictionary<uint, int>();

        var mapsByArea = geometry.Zones.ToDictionary(area => area.AreaID, area => area.ID);

        var result = new SortedDictionary<int, object>();
        int unplaced = 0;
        foreach (var quest in quests)
        {
            uint id = (uint)quest["id"].GetValue<int>();
            int doableLevel = DoableLevel(id, []);
            var givers = Spots(creatureStarters[id], gameObjectStarters[id]);
            bool atEnder = givers.Count == 0;
            if (atEnder)
            {
                givers = Spots(creatureEnders[id], gameObjectEnders[id]);
            }

            if (givers.Count > 0)
            {
                result[(int)id] = new { Givers = givers, AtEnder = atEnder ? true : (bool?)null, DoableLevel = doableLevel };
                continue;
            }

            // Left to the zone the quest log files it under, if it has a map
            unplaced++;
            int? zoneMap = quest["questSortArea"]?["id"]?.GetValue<int>() is int zone && mapsByArea.TryGetValue(zone, out int mapId) ? mapId : null;
            result[(int)id] = new { Map = zoneMap, DoableLevel = doableLevel };
        }

        Write(outDir, "quests.json", result);
        logger.LogInformation("Wrote {count} quests, {unplaced} of them without a spot.", result.Count, unplaced);

        static List<uint> Ids(JsonNode node) => node?.AsArray().Select(id => (uint)id.GetValue<int>()).ToList() ?? [];

        // A quest can only be taken on once its chain can: like archipelawow's levels, this is the highest level of
        // the prerequisites that are all needed, and of the cheapest of those of which one is enough
        int DoableLevel(uint id, HashSet<uint> chain)
        {
            if (doableLevels.TryGetValue(id, out int cached))
            {
                return cached;
            }

            if (!chain.Add(id) || !ownLevels.TryGetValue(id, out int level))
            {
                return 0;
            }

            var (all, any) = prerequisites.GetValueOrDefault(id, ([], []));
            level = Math.Max(level, all.Select(p => DoableLevel(p, chain)).DefaultIfEmpty(0).Max());
            level = Math.Max(level, any.Select(p => DoableLevel(p, chain)).DefaultIfEmpty(0).Min());
            chain.Remove(id);
            return doableLevels[id] = level;
        }

        List<object[]> Spots(IEnumerable<uint> creatures, IEnumerable<uint> gameObjects)
        {
            var spots = creatures.SelectMany(c => creatureSpawns[c].Select(s => (Spawn: s, Side: creatureSides.GetValueOrDefault(c))))
                .Concat(gameObjects.SelectMany(g => gameObjectSpawns[g].Select(s => (Spawn: s, Side: 0))))
                .Select(spot => (Position: Place(spot.Spawn), spot.Side))
                .Where(spot => spot.Position != null)
                .DistinctBy(spot => (spot.Position.Value.Area, MathF.Round(spot.Position.Value.X, 3), MathF.Round(spot.Position.Value.Y, 3), spot.Side))
                .Take(MAX_GIVER_SPOTS)
                .Select(spot => spot.Side == 0 ? spot.Position.Value.ToJson() : [.. spot.Position.Value.ToJson(), spot.Side])
                .ToList();
            return spots;
        }

        MapPosition? Place(Spawn spawn)
        {
            if (!WorldMapGeometry.CONTINENT_MAPS.Contains(spawn.Map))
            {
                return entrances.TryGetValue(spawn.Map, out var entrance) ? entrance : null;
            }
            return LocateSpawn(spawn) ?? LocateNear(spawns, spawn.Map, spawn.X, spawn.Y);
        }
    }

    /// <summary>
    /// The side only one of which a creature talks to, by the faction relations of the playable races, or 0
    /// when both can.
    /// </summary>
    private async Task<Dictionary<uint, int>> GetCreatureSides(HashSet<uint> creatureIds)
    {
        var playable = races.Where(r => (r.Flags & CHR_RACE_FLAG_NOT_PLAYABLE) == 0 && (r.Alliance == RACE_ALLIANCE || r.Alliance == RACE_HORDE)).ToList();
        var factions = await db.CreatureTemplates.Where(c => creatureIds.Contains(c.Entry)).Select(c => new { c.Entry, c.Faction }).ToListAsync();

        var result = new Dictionary<uint, int>();
        foreach (var creature in factions)
        {
            var template = factionTemplates.Get(creature.Faction);
            if (template == null)
            {
                continue;
            }

            var talking = playable.Where(race => factionTemplates.Get(race.FactionID) is { } raceFaction && FactionRelations.WillTalkTo(template, raceFaction)).ToList();
            if (talking.Count > 0 && talking.All(race => race.Alliance == RACE_ALLIANCE))
            {
                result[creature.Entry] = SIDE_ALLIANCE;
            }
            else if (talking.Count > 0 && talking.All(race => race.Alliance == RACE_HORDE))
            {
                result[creature.Entry] = SIDE_HORDE;
            }
        }
        return result;
    }

    /// <summary>
    /// The flight masters, at their taxi nodes. A node no flight master stands by is a quest flight, a
    /// transport's or a leftover, which no flight path location uses.
    /// </summary>
    private async Task ExtractFlightPaths(string outDir, ILookup<int, PlacedSpawn> spawns)
    {
        var flightMasters = (await db.Creatures
            .Where(c => WorldMapGeometry.CONTINENT_MAPS.Contains(c.Map))
            .Join(db.CreatureTemplates, c => c.Id, t => t.Entry, (c, t) => new { c.Map, c.PositionX, c.PositionY, Flags = c.Npcflag | t.Npcflag })
            .Where(c => (c.Flags & NPC_FLAG_FLIGHT_MASTER) != 0)
            .Select(c => new { c.Map, c.PositionX, c.PositionY })
            .ToListAsync())
            .ToLookup(c => (int)c.Map);

        var result = new SortedDictionary<int, object>();
        foreach (var node in taxiNodes.Where(n => WorldMapGeometry.CONTINENT_MAPS.Contains(n.ContinentID)))
        {
            bool served = flightMasters[node.ContinentID].Any(c => float.Hypot(c.PositionX - node.Pos[0], c.PositionY - node.Pos[1]) <= FLIGHT_MASTER_RANGE);
            if (served && LocateNear(spawns, node.ContinentID, node.Pos[0], node.Pos[1]) is { } position)
            {
                result[node.ID] = new { Position = position.ToJson() };
            }
        }

        Write(outDir, "flightpaths.json", result);
        logger.LogInformation("Wrote {count} flight paths.", result.Count);
    }

    /// <summary>Where the faction leaders of leaders.json stand, with the icon of the achievement for killing them.</summary>
    private async Task ExtractLeaders(string outDir, string dataDir, ILookup<int, PlacedSpawn> spawns, HashSet<string> icons)
    {
        var leaders = ReadIds(dataDir, "leaders.json").Select(id => (uint)id).ToList();
        var leaderSpawns = (await db.Creatures.Where(c => leaders.Contains(c.Id))
            .Select(c => new { c.Id, Spawn = new Spawn(c.Map, c.ZoneId, c.AreaId, c.PositionX, c.PositionY) }).ToListAsync())
            .ToLookup(c => c.Id, c => c.Spawn);
        var killAchievements = achievementCriteria
            .Where(c => c.Type == ACHIEVEMENT_CRITERIA_TYPE_KILL_CREATURE)
            .ToLookup(c => (uint)c.AssetID, c => c.AchievementID);

        var result = new SortedDictionary<int, object>();
        foreach (uint leader in leaders)
        {
            var position = leaderSpawns[leader]
                .Where(spawn => WorldMapGeometry.CONTINENT_MAPS.Contains(spawn.Map))
                .Select(spawn => LocateSpawn(spawn) ?? LocateNear(spawns, spawn.Map, spawn.X, spawn.Y))
                .FirstOrDefault(p => p != null);
            if (position == null)
            {
                logger.LogWarning("Found no spot for the faction leader {id}.", leader);
                continue;
            }

            int achievement = killAchievements[leader].DefaultIfEmpty(0).Min();
            result[(int)leader] = new
            {
                Position = position.Value.ToJson(),
                Icon = Icon(spellIcons.Get(achievements.Get(achievement)?.IconID ?? 0)?.TextureFilename, icons),
            };
        }

        Write(outDir, "leaders.json", result);
        logger.LogInformation("Wrote {count} faction leaders.", result.Count);
    }

    private void ExtractSpells(string outDir, string dataDir, HashSet<string> icons)
    {
        var result = new SortedDictionary<int, object>();
        foreach (var spell in ReadArray(dataDir, "spells.json"))
        {
            int id = spell["id"].GetValue<int>();
            if (Icon(spellIcons.Get(spells.Get(id)?.SpellIconID ?? 0)?.TextureFilename, icons) is { } icon)
            {
                result[id] = new { Icon = icon };
            }
        }

        Write(outDir, "spells.json", result);
        logger.LogInformation("Wrote the icons of {count} spells.", result.Count);
    }

    /// <summary>The skills of skills.json, with the icon of the weapon proficiency that teaches them.</summary>
    private void ExtractSkills(string outDir, string dataDir, HashSet<string> icons)
    {
        var result = new SortedDictionary<int, object>();
        foreach (var skill in ReadArray(dataDir, "skills.json"))
        {
            int id = skill["id"].GetValue<int>();
            string icon = SKILL_ICONS.GetValueOrDefault(id)
                ?? spellIcons.Get(spells.Get(skill["spell"].GetValue<int>())?.SpellIconID ?? 0)?.TextureFilename;
            result[id] = new { Name = skill["name"].GetValue<string>(), Icon = Icon(icon, icons) };
        }

        Write(outDir, "skills.json", result);
        logger.LogInformation("Wrote {count} skills.", result.Count);
    }

    /// <summary>The subzones of explorations.json, in the middle of their explored area, with their achievement's icon.</summary>
    private void ExtractExplorations(string outDir, string dataDir, HashSet<string> icons)
    {
        var result = new SortedDictionary<int, object>();
        foreach (int id in ReadIds(dataDir, "explorations.json"))
        {
            var criteria = achievementCriteria.Get(id);
            if (mapExtractor.ExploredAreaPosition(overlays.Get(criteria.AssetID)) is not { } position)
            {
                logger.LogWarning("Found no spot for the subzone {id}.", id);
                continue;
            }

            result[id] = new
            {
                Position = position.ToJson(),
                Icon = Icon(spellIcons.Get(achievements.Get(criteria.AchievementID)?.IconID ?? 0)?.TextureFilename, icons),
            };
        }

        Write(outDir, "explorations.json", result);
        logger.LogInformation("Wrote {count} subzones.", result.Count);
    }

    /// <summary>
    /// The achievements, with the dungeon of those about one. Achievement.dbc names it for few of the classic and
    /// Outland dungeons' own: theirs is the dungeon of the encounter that killing the boss they ask for credits.
    /// </summary>
    private async Task ExtractAchievements(string outDir, HashSet<string> icons)
    {
        var encounterMaps = encounters.ToDictionary(e => e.ID, e => e.MapID);
        var bossMaps = (await db.InstanceEncounters
            .Where(e => e.CreditType == ENCOUNTER_CREDIT_KILL_CREATURE)
            .Select(e => new { e.Entry, e.CreditEntry })
            .ToListAsync())
            .Where(e => encounterMaps.ContainsKey((int)e.Entry))
            .ToLookup(e => (int)e.CreditEntry, e => encounterMaps[(int)e.Entry]);
        var bossKills = achievementCriteria
            .Where(c => c.Type == ACHIEVEMENT_CRITERIA_TYPE_KILL_CREATURE)
            .ToLookup(c => c.AchievementID, c => c.AssetID);
        int? Dungeon(int achievement)
        {
            var dungeons = bossKills[achievement].SelectMany(boss => bossMaps[boss]).Distinct().ToList();
            return dungeons.Count == 1 ? dungeons[0] : null;
        }

        var result = new SortedDictionary<int, object>();
        foreach (var achievement in achievements.Where(a => achievementCategories.WithParents(a.Category).Any(ACHIEVEMENT_CATEGORIES.Contains)))
        {
            result[achievement.ID] = new
            {
                Name = achievement.TitleLang,
                Icon = Icon(spellIcons.Get(achievement.IconID)?.TextureFilename, icons),
                Map = achievement.InstanceID > 0 ? achievement.InstanceID : Dungeon(achievement.ID),
            };
        }

        Write(outDir, "achievements.json", result);
        logger.LogInformation("Wrote {count} achievements.", result.Count);
    }

    private async Task ExtractItems(string outDir, HashSet<string> icons)
    {
        var items = await db.ItemTemplates
            .Where(i => i.Class == ITEM_CLASS_GLYPH || i.Quality == ITEM_QUALITY_HEIRLOOM || (i.Class == ITEM_CLASS_CONTAINER && i.Subclass == ITEM_SUBCLASS_BAG)
                || CONSUMABLE_ITEMS.Contains(i.Entry))
            .Select(i => new { i.Entry, i.Displayid })
            .ToListAsync();

        var result = new SortedDictionary<int, object>();
        foreach (var item in items)
        {
            if (Icon(itemDisplays.Get((int)item.Displayid)?.InventoryIcon[0], icons) is { } icon)
            {
                result[(int)item.Entry] = new { Icon = icon };
            }
        }

        Write(outDir, "items.json", result);
        logger.LogInformation("Wrote {count} items.", result.Count);
    }

    /// <summary>"Interface\Icons\Spell_Fire_FlameBolt" as the name the icon is written under, spell_fire_flamebolt.</summary>
    public static string IconName(string path) => path.Replace('\\', '/').Split('/')[^1].Trim().ToLowerInvariant();

    private static string Icon(string path, HashSet<string> icons)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }
        string name = IconName(path);
        icons.Add(name);
        return name;
    }

    private void WriteIcons(string outDir, HashSet<string> icons)
    {
        int written = 0;
        foreach (string icon in icons.Order())
        {
            using var bitmap = client.Load($"Interface/Icons/{icon}");
            if (bitmap == null)
            {
                logger.LogWarning("Icon {icon} is missing from the client directory.", icon);
                continue;
            }
            ClientTextures.SaveWebp(bitmap, Path.Combine(outDir, "icons", icon + ".webp"), ICON_QUALITY);
            written++;
        }
        logger.LogInformation("Wrote {count} icons.", written);
    }

    /// <summary>
    /// The marker images, and the class and race icons cut out of the character creation screen's, with the
    /// names of those classes and races.
    /// </summary>
    private void WriteInterfaceImages(string outDir)
    {
        foreach (var (name, texture) in INTERFACE_IMAGES)
        {
            using var bitmap = client.Load(texture) ?? throw new FileNotFoundException($"{texture} is missing from the client directory.");
            ClientTextures.SaveWebp(bitmap, Path.Combine(outDir, "ui", name + ".webp"), ICON_QUALITY);
        }

        var classNames = new SortedDictionary<int, string>();
        var classCoords = ReadTexCoords(client, "Interface/FrameXML/Constants.lua", "CLASS_ICON_TCOORDS");
        using (var atlas = client.Load("Interface/Glues/CharacterCreate/UI-CharacterCreate-Classes"))
        {
            foreach (var chrClass in classes.Where(c => classCoords.ContainsKey(c.Filename)))
            {
                WriteAtlasPart(atlas, classCoords[chrClass.Filename], Path.Combine(outDir, "ui", $"class_{chrClass.ID}.webp"));
                classNames[chrClass.ID] = chrClass.NameLang;
            }
        }

        var raceNames = new SortedDictionary<int, object>();
        var raceCoords = ReadTexCoords(client, "Interface/GlueXML/CharacterCreate.lua", "RACE_ICON_TCOORDS");
        using (var atlas = client.Load("Interface/Glues/CharacterCreate/UI-CharacterCreate-Races"))
        {
            foreach (var race in races)
            {
                string key = race.ClientFileString.ToUpperInvariant();
                if (raceCoords.TryGetValue(key + "_MALE", out var male) && raceCoords.TryGetValue(key + "_FEMALE", out var female))
                {
                    WriteAtlasPart(atlas, male, Path.Combine(outDir, "ui", $"race_{race.ID}_male.webp"));
                    WriteAtlasPart(atlas, female, Path.Combine(outDir, "ui", $"race_{race.ID}_female.webp"));
                    raceNames[race.ID] = new { Name = race.NameLang, Side = race.Alliance == RACE_ALLIANCE ? SIDE_ALLIANCE : SIDE_HORDE };
                }
            }
        }

        Write(outDir, "characters.json", new { Races = raceNames, Classes = classNames });
    }

    private static void WriteAtlasPart(SKBitmap atlas, float[] coords, string file)
    {
        var rect = SKRectI.Round(new SKRect(coords[0] * atlas.Width, coords[2] * atlas.Height, coords[1] * atlas.Width, coords[3] * atlas.Height));
        using var part = ClientTextures.Crop(atlas, rect, 64, 64);
        ClientTextures.SaveWebp(part, file, ICON_QUALITY);
    }

    /// <summary>A Lua table of texture coordinates (left, right, top, bottom) keyed by name, out of FrameXML.</summary>
    private static Dictionary<string, float[]> ReadTexCoords(ClientTextures client, string file, string table)
    {
        string lua = client.ReadText(file) ?? throw new FileNotFoundException($"{file} is missing from the client directory.");
        int start = lua.IndexOf(table + " = {", StringComparison.Ordinal);
        int end = lua.IndexOf("};", start, StringComparison.Ordinal);
        return TexCoordRegex().Matches(lua[start..end]).ToDictionary(
            m => m.Groups["key"].Value,
            m => m.Groups["value"].Captures.Select(c => float.Parse(c.Value, CultureInfo.InvariantCulture)).ToArray());
    }

    [GeneratedRegex(@"\[""(?<key>\w+)""\]\s*=\s*\{\s*(?:(?<value>[\d.]+)\s*,?\s*){4}\}")]
    private static partial Regex TexCoordRegex();

    private static List<JsonNode> ReadArray(string dataDir, string file) =>
        [.. JsonNode.Parse(File.ReadAllText(Path.Combine(dataDir, file))).AsArray()];

    private static HashSet<int> ReadIds(string dataDir, string file) => [.. ReadArray(dataDir, file).Select(node => node["id"].GetValue<int>())];

    private static void Write(string outDir, string file, object data) =>
        File.WriteAllText(Path.Combine(outDir, file), JsonSerializer.Serialize(data, JsonOptions));
}
