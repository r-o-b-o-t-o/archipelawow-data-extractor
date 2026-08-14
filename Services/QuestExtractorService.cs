using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArchipelaWoW.QuestExtractor.Dbc;
using ArchipelaWoW.QuestExtractor.Entities;
using ArchipelaWoW.QuestExtractor.Entities.World;
using ArchipelaWoW.QuestExtractor.Services.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArchipelaWoW.QuestExtractor.Services;

public class ExtractedQuestData
{
    public uint Id { get; set; }
    public string Title { get; set; }
    public string DisplayTitle { get; set; }
    public short MinLevel { get; set; }
    public short? RecommendedLevel { get; set; }
    public byte? SuggestedGroupSize { get; set; }
    public List<int> Races { get; set; } = [];
    public List<int> Classes { get; set; } = [];
    public ExtractedArea QuestSortArea { get; set; }
    public bool IsBreadcrumb { get; set; }

    /// <summary>
    /// The quest is marked as taking place in a dungeon. Kept in the output rather than filtered out so
    /// that archipelawow can gate these behind a player option; note that the quest data almost never
    /// sets a suggested group size on them, so <see cref="SuggestedGroupSize"/> is no substitute.
    /// </summary>
    public bool IsDungeon { get; set; }
    public List<uint> RequiresAny { get; set; } = [];
    public List<uint> RequiresAll { get; set; } = [];
    public List<ExtractedArea> StartZones { get; set; } = [];
    public List<ExtractedArea> EndZones { get; set; } = [];
    public List<ExtractedArea> ObjectiveZones { get; set; } = [];

    // Only the filters below read these; archipelawow's QuestModel ignores them, so they are kept out
    // of the output rather than adding a few hundred KB of dead weight to quests.json.
    [JsonIgnore] public QuestSort QuestSort { get; set; }
    [JsonIgnore] public QuestInfo QuestInfo { get; set; }
    [JsonIgnore] public uint Flags { get; set; }
    [JsonIgnore] public uint? SpecialFlags { get; set; }
    [JsonIgnore] public QuestTemplate QuestTemplate { get; set; }
}

public class ExtractedArea
{
    public int Id { get; set; }
    public string Name { get; set; }

    public static ExtractedArea FromAreaTable(AreaTable row)
    {
        return row == null
            ? null
            : new ExtractedArea()
            {
                Id = row.ID,
                Name = row.AreaNameLang,
            };
    }
}

public class QuestExtractorService(
        ILogger<QuestExtractorService> logger,
        QuestTemplateRepository questsRepo,
        DisablesRepository disablesRepo,
        WorldDbContext db,
        ChrRacesContainer races,
        ChrClassesContainer classes,
        AreaTableContainer areas,
        QuestSortContainer questSorts,
        QuestInfoContainer questInfos,
        WorldMapAreaContainer worldMapAreas,
        FactionTemplateContainer factionTemplates
    )
{
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private const int QUEST_FLAGS_RAID = 64;
    private const int QUEST_FLAGS_DAILY = 4096;
    private const int QUEST_FLAGS_FLAGS_PVP = 8192;
    private const int QUEST_FLAGS_UNAVAILABLE = 16384;
    private const int QUEST_FLAGS_WEEKLY = 32768;

    private const int QUEST_SPECIAL_FLAGS_REPEATABLE = 1;
    private const int QUEST_SPECIAL_FLAGS_DF_QUEST = 8;
    private const int QUEST_SPECIAL_FLAGS_MONTHLY = 16;

    private const int QUEST_INFO_LIFE = 21;
    private const int QUEST_INFO_PVP = 41;
    private const int QUEST_INFO_RAID = 62;
    private const int QUEST_INFO_DUNGEON = 81;
    private const int QUEST_INFO_WORLD_EVENT = 82;
    private const int QUEST_INFO_LEGENDARY = 83;
    private const int QUEST_INFO_HEROIC = 85;
    private const int QUEST_INFO_RAID_10 = 88;
    private const int QUEST_INFO_RAID_25 = 89;

    private const int QUEST_SORT_EPIC = 1;
    private const int QUEST_SORT_REUSE_OLD_WAILING_CAVERNS = 21;
    private const int QUEST_SORT_SEASONAL = 22;
    private const int QUEST_SORT_REUSE_OLD_UNDERCITY_ONE = 23;
    private const int QUEST_SORT_HERBALISM = 24;
    private const int QUEST_SORT_BATTLEGROUNDS = 25;
    private const int QUEST_SORT_DAY_OF_THE_DEAD = 41;
    private const int QUEST_SORT_FISHING = 101;
    private const int QUEST_SORT_BLACKSMITHING = 121;
    private const int QUEST_SORT_ALCHEMY = 181;
    private const int QUEST_SORT_LEATHERWORKING = 182;
    private const int QUEST_SORT_ENGINEERING = 201;
    private const int QUEST_SORT_TOURNAMENT = 241;
    private const int QUEST_SORT_TAILORING = 264;
    private const int QUEST_SORT_SPECIAL = 284;
    private const int QUEST_SORT_COOKING = 304;
    private const int QUEST_SORT_FIRST_AID = 324;
    private const int QUEST_SORT_LEGENDARY = 344;
    private const int QUEST_SORT_DARKMOON_FAIRE = 364;
    private const int QUEST_SORT_AHNQIRAJ_WAR = 365;
    private const int QUEST_SORT_LUNAR_FESTIVAL = 366;
    private const int QUEST_SORT_REPUTATION = 367;
    private const int QUEST_SORT_INVASION = 368;
    private const int QUEST_SORT_MIDSUMMER = 369;
    private const int QUEST_SORT_BREWFEST = 370;
    private const int QUEST_SORT_INSCRIPTION = 371;
    private const int QUEST_SORT_JEWELCRAFTING = 373;
    private const int QUEST_SORT_NOBLEGARDEN = 374;
    private const int QUEST_SORT_PILGRIMS_BOUNTY = 375;
    private const int QUEST_SORT_LOVE_IS_IN_THE_AIR = 376;

    private const int CHR_RACE_FLAG_NOT_PLAYABLE = 0x1;

    private const int MAX_CHARACTER_LEVEL = 80;

    /// <summary>
    /// Below this share of quest givers resolving a zone, the spawn table is assumed to be missing its
    /// denormalised zone data rather than merely holding a few odd rows.
    /// </summary>
    private const double MIN_ZONE_COVERAGE = 0.5;

    // Held as statics so the filters below stop allocating their constants once per quest.
    private static readonly int[] EXCLUDED_FLAGS = [
        QUEST_FLAGS_RAID,
        QUEST_FLAGS_DAILY,
        QUEST_FLAGS_FLAGS_PVP,
        QUEST_FLAGS_UNAVAILABLE,
        QUEST_FLAGS_WEEKLY,
    ];

    private static readonly int[] EXCLUDED_SPECIAL_FLAGS = [
        QUEST_SPECIAL_FLAGS_REPEATABLE,
        QUEST_SPECIAL_FLAGS_DF_QUEST,
        QUEST_SPECIAL_FLAGS_MONTHLY,
    ];

    private static readonly HashSet<int> EXCLUDED_QUEST_SORTS = [
        QUEST_SORT_EPIC,
        QUEST_SORT_REUSE_OLD_WAILING_CAVERNS,
        QUEST_SORT_SEASONAL,
        QUEST_SORT_REUSE_OLD_UNDERCITY_ONE,
        QUEST_SORT_HERBALISM,
        QUEST_SORT_BATTLEGROUNDS,
        QUEST_SORT_DAY_OF_THE_DEAD,
        QUEST_SORT_FISHING,
        QUEST_SORT_BLACKSMITHING,
        QUEST_SORT_ALCHEMY,
        QUEST_SORT_LEATHERWORKING,
        QUEST_SORT_ENGINEERING,
        QUEST_SORT_TOURNAMENT,
        QUEST_SORT_TAILORING,
        QUEST_SORT_SPECIAL,
        QUEST_SORT_COOKING,
        QUEST_SORT_FIRST_AID,
        QUEST_SORT_LEGENDARY,
        QUEST_SORT_DARKMOON_FAIRE,
        QUEST_SORT_AHNQIRAJ_WAR,
        QUEST_SORT_LUNAR_FESTIVAL,
        QUEST_SORT_REPUTATION,
        QUEST_SORT_INVASION,
        QUEST_SORT_MIDSUMMER,
        QUEST_SORT_BREWFEST,
        QUEST_SORT_INSCRIPTION,
        QUEST_SORT_JEWELCRAFTING,
        QUEST_SORT_NOBLEGARDEN,
        QUEST_SORT_PILGRIMS_BOUNTY,
        QUEST_SORT_LOVE_IS_IN_THE_AIR,
    ];

    // QUEST_INFO_DUNGEON is deliberately absent: dungeon quests are kept and flagged with
    // ExtractedQuestData.IsDungeon so that archipelawow can include or exclude them per player option.
    private static readonly HashSet<int> EXCLUDED_QUEST_INFOS = [
        QUEST_INFO_LIFE,
        QUEST_INFO_PVP,
        QUEST_INFO_RAID,
        QUEST_INFO_WORLD_EVENT,
        QUEST_INFO_LEGENDARY,
        QUEST_INFO_HEROIC,
        QUEST_INFO_RAID_10,
        QUEST_INFO_RAID_25,
    ];

    private static readonly HashSet<uint> DARKMOON_FAIRE_QUESTS = [7905, 7926];

    private static readonly HashSet<uint> COLLECTORS_EDITION_QUESTS = [
        // "Frosty" pet
        70998, 70999,
        // Collector's edition welcome packages
        5805, 5841, 5842, 5843, 5844, 5847, 8547, 9278, 12781,
    ];

    private static readonly HashSet<uint> CLOTH_DONATION_QUESTS = [
        7791, 7792, 7793, 7794, 7795, 7798, 7799, 7800, 7802, 7803, 7804, 7805, 7807, 7808, 7809, 7811, 7813, 7814, 7817, 7818, 7820,
        7821, 7822, 7823, 7824, 7826, 7827, 7831, 7833, 7834, 7835, 7836, 10352, 10354, 10356, 10357, 10359, 10360, 10361, 10362,
    ];

    private static readonly HashSet<uint> LEARN_TO_RIDE_QUESTS = [
        14079, 14081, 14082, 14083, 14084, 14085, 14086, 14087, 14088, 14089,
    ];

    /// <summary>Zone ids already reported as missing from AreaTable.dbc, so each is only logged once.</summary>
    private readonly HashSet<int> unknownZoneIds = [];

    private List<ChrRaces> playableRaces;

    public async Task ExtractQuests()
    {
        string outDir = Env.GetString("OUT_DIR") ?? throw new InvalidOperationException("\"OUT_DIR\" environment variable not set.");
        if (!Directory.Exists(outDir))
        {
            Directory.CreateDirectory(outDir);
        }

        var questTemplates = await questsRepo.GetAllQuests();
        var questGiverFactions = await GetQuestGiverFactions(questTemplates);

        var quests = questTemplates
            .Select(quest => MapQuestTemplate(quest, questGiverFactions))
            .ToList();

        FillQuestPrerequisites(quests);
        await FillCreatureZones(quests);
        await FillGameObjectZones(quests);
        await FillPOIZones(quests);

        var questDisables = (await disablesRepo.GetQuestDisables()).Select(d => d.Entry).ToHashSet();

        quests = [.. quests
            .Where(q => FilterQuest(q, "disables", q => FilterDisables(questDisables, q)))
            .Where(q => FilterQuest(q, "flags", FilterFlags))
            .Where(q => FilterQuest(q, "special flags", FilterSpecialFlags))
            .Where(q => FilterQuest(q, "pvp kills required", FilterPvPKills))
            .Where(q => FilterQuest(q, "excluded quest sort", FilterQuestSort))
            .Where(q => FilterQuest(q, "excluded quest info", FilterQuestInfo))
            .Where(q => FilterQuest(q, "pooled quest", FilterPooled))
            .Where(q => FilterQuest(q, "world events", FilterWorldEvents))
            .Where(q => FilterQuest(q, "no starters", FilterMissingStarters))
            .Where(q => FilterQuest(q, "no enders", FilterMissingEnders))
            .Where(q => FilterQuest(q, "no allowed races", FilterNoAllowedRaces))
            .Where(q => FilterQuest(q, "deprecated name", FilterDeprecatedName))
            .Where(q => FilterQuest(q, "unreachable minimum level", FilterUnreachableMinLevel))
            .Where(q => FilterQuest(q, "reputation requirement", FilterReputation))
            .Where(q => FilterQuest(q, "profession requirement", FilterProfession))
            .Where(q => FilterQuest(q, "exclusive group", FilterExclusiveGroups))
            .Where(q => FilterQuest(q, "collector's edition", FilterCollectorsEdition))
            .Where(q => FilterQuest(q, "cloth donation", FilterClothDonation))
            .Where(q => FilterQuest(q, "learn to ride", FilterLearnToRide))];

        FilterUnavailablePrerequisites(quests);
        FillQuestDisplayTitles(quests);

        string outFile = Path.Combine(outDir, "quests.json");
        await File.WriteAllTextAsync(outFile, JsonSerializer.Serialize(quests.OrderBy((q) => q.Id), jsonOptions));

        logger.LogInformation("Wrote {count} quests to {file}.", quests.Count, outFile);
    }

    /// <summary>
    /// Maps every quest giver to its faction in a single query, so that mapping a quest stays offline.
    /// </summary>
    private async Task<Dictionary<uint, ushort>> GetQuestGiverFactions(List<QuestTemplate> quests)
    {
        var npcIds = quests
            .SelectMany(q => q.CreatureStarters.Select(starter => starter.Id)
                .Concat(q.CreatureEnders.Select(ender => ender.Id)))
            .ToHashSet();

        return await db.CreatureTemplates
            .Where(creature => npcIds.Contains(creature.Entry))
            .Select(creature => new { creature.Entry, creature.Faction })
            .ToDictionaryAsync(creature => creature.Entry, creature => creature.Faction);
    }

    private ExtractedQuestData MapQuestTemplate(QuestTemplate q, Dictionary<uint, ushort> questGiverFactions)
    {
        var questInfo = GetQuestInfo(q);

        return new ExtractedQuestData()
        {
            Id = q.Id,
            Title = q.LogTitle,
            MinLevel = q.MinLevel,
            // QuestLevel is the reward tier and carries -1 as its unset sentinel, so it is not written
            // out; it only seeds the level a player can reasonably take the quest at.
            RecommendedLevel = q.QuestLevel > 0 ? (short)Math.Max(Math.Max(q.QuestLevel - 2, 1), q.MinLevel) : null,
            SuggestedGroupSize = q.SuggestedGroupNum > 1 ? q.SuggestedGroupNum : null,
            Races = GetAllowableRaces(q, questGiverFactions)?.Select(r => r.ID).ToList(),
            Classes = GetAllowableClasses(q)?.Select(c => c.ID).ToList(),
            QuestSort = GetQuestSort(q),
            QuestSortArea = GetQuestSortArea(q),
            QuestInfo = questInfo,
            Flags = q.Flags,
            SpecialFlags = q.QuestTemplateAddon?.SpecialFlags,
            IsBreadcrumb = (q.QuestTemplateAddon?.BreadcrumbForQuestId ?? 0) != 0,
            IsDungeon = questInfo?.ID == QUEST_INFO_DUNGEON,
            RequiresAny = (q.QuestTemplateAddon?.PrevQuestId ?? 0) == 0 ? [] : [(uint)Math.Abs(q.QuestTemplateAddon.PrevQuestId)],
            QuestTemplate = q,
        };
    }

    /// <summary>
    /// Returns null when every race may take the quest, an empty list when none may.
    /// </summary>
    private List<ChrRaces> GetAllowableRaces(QuestTemplate q, Dictionary<uint, ushort> questGiverFactions)
    {
        var playableRaces = GetPlayableRaces();
        List<ChrRaces> result = null;

        if (q.AllowableRaces != 0)
        {
            result = [.. playableRaces.Where(race => HasBit(q.AllowableRaces, race.ID))];
        }

        var starterIds = q.CreatureStarters.Select(starter => starter.Id).ToHashSet();
        var enderIds = q.CreatureEnders.Select(ender => ender.Id).ToHashSet();

        var starterFactions = GetFactionTemplates(starterIds);
        var enderFactions = GetFactionTemplates(enderIds);

        if (starterFactions.Count > 0 || enderFactions.Count > 0)
        {
            if (result == null)
            {
                // The quest carries no race restriction, but a starter/ender that attacks some races
                // on sight restricts it all the same, so derive the races from the faction relations.
                result = [.. playableRaces.Where(IsAvailableTo)];
            }
            else
            {
                result.RemoveAll(race => !IsAvailableTo(race));
            }
        }

        // A list holding every playable race says no more than no restriction at all.
        return result?.Count == playableRaces.Count ? null : result;

        List<FactionTemplate> GetFactionTemplates(HashSet<uint> creatureIds)
        {
            return [.. creatureIds
                .Select(id => questGiverFactions.TryGetValue(id, out var faction) ? factionTemplates.Get(faction) : null)
                .Where(template => template != null)
                .Distinct()];
        }

        bool IsAvailableTo(ChrRaces race)
        {
            var raceFaction = GetRaceFaction(race);
            return raceFaction == null ||
                (HasReachableGiver(starterFactions, raceFaction) && HasReachableGiver(enderFactions, raceFaction));
        }

        // A quest handed out by several creatures only needs one of them to talk to the race, and
        // the two sides are independent: quests shared by both factions typically pair an alliance
        // starter with an alliance ender and a horde starter with a horde ender.
        static bool HasReachableGiver(List<FactionTemplate> questGivers, FactionTemplate raceFaction)
        {
            return questGivers.Count == 0 || questGivers.Any(npc =>
                IsFriendlyTo(npc, raceFaction) || !IsHostileTo(npc, raceFaction));
        }
    }

    private FactionTemplate GetRaceFaction(ChrRaces race)
    {
        return factionTemplates.Get(race.FactionID);
    }

    // Faction relations are directional and only the quest giver's view matters here: the client
    // refuses to open a gossip/quest window with a creature that is hostile to the player.
    // See https://www.azerothcore.org/wiki/factiontemplate
    // `Enemies`/`Friend` hold Faction ids matched against the other template's `Faction`, while
    // `FactionGroup`/`FriendGroup`/`EnemyGroup` are bitmasks over the four faction groups
    // (1: players, 2: alliance, 4: horde, 8: monsters).
    private static bool IsHostileTo(FactionTemplate self, FactionTemplate other)
    {
        if (self.ID == other.ID)
        {
            return false;
        }

        if (other.Faction != 0)
        {
            if (self.Enemies.Contains(other.Faction))
            {
                return true;
            }

            // An explicit friend entry outranks the hostile group mask.
            if (self.Friend.Contains(other.Faction))
            {
                return false;
            }
        }

        return (self.EnemyGroup & other.FactionGroup) != 0;
    }

    private static bool IsFriendlyTo(FactionTemplate self, FactionTemplate other)
    {
        if (self.ID == other.ID)
        {
            return true;
        }

        if (other.Faction != 0)
        {
            if (self.Enemies.Contains(other.Faction))
            {
                return false;
            }

            if (self.Friend.Contains(other.Faction))
            {
                return true;
            }
        }

        return (self.FriendGroup & other.FactionGroup) != 0 || (self.FactionGroup & other.FriendGroup) != 0;
    }

    /// <summary>
    /// Whether the 1-based <paramref name="id"/> is set in a DBC race/class bitmask. Both masks are 32
    /// bits wide, so an id past that cannot be expressed and is never part of the mask.
    /// </summary>
    private static bool HasBit(uint mask, int id)
    {
        if (id < 1 || id > 32)
        {
            return false;
        }

        uint bit = 1u << (id - 1);
        return (mask & bit) != 0;
    }

    private List<ChrRaces> GetPlayableRaces()
    {
        // The DBC never changes over a run, and this is asked once per quest.
        return playableRaces ??= [.. races.Where(r => (r.Flags & CHR_RACE_FLAG_NOT_PLAYABLE) == 0)];
    }

    private List<ChrClasses> GetAllowableClasses(QuestTemplate q)
    {
        uint allowableClasses = q.QuestTemplateAddon?.AllowableClasses ?? 0;

        if (allowableClasses == 0)
        {
            return null;
        }

        return [.. classes.Where(c => HasBit(allowableClasses, c.ID))];
    }

    private QuestSort GetQuestSort(QuestTemplate q)
    {
        if (q.QuestSortId > 0)
        {
            return null;
        }

        // Negated rather than Math.Abs, which overflows on short.MinValue.
        return questSorts.Get(-q.QuestSortId);
    }

    private ExtractedArea GetQuestSortArea(QuestTemplate q)
    {
        if (q.QuestSortId < 0)
        {
            return null;
        }

        return ExtractedArea.FromAreaTable(areas.Get(q.QuestSortId));
    }

    private QuestInfo GetQuestInfo(QuestTemplate q)
    {
        return q.QuestInfoId > 0 ? questInfos.Get(q.QuestInfoId) : null;
    }

    private bool FilterQuest(ExtractedQuestData q, string reason, Func<ExtractedQuestData, bool> fn)
    {
        bool result = fn(q);
        if (!result && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Filtered out \"{title}\" ({id}): {reason}", q.Title, q.Id, reason);
        }
        return result;
    }

    private bool FilterExclusiveGroups(ExtractedQuestData q)
    {
        return q.QuestTemplate.QuestTemplateAddon == null ||
            q.QuestTemplate.QuestTemplateAddon.ExclusiveGroup <= 0;
    }

    private bool FilterDisables(HashSet<uint> disabledQuestIds, ExtractedQuestData q)
    {
        return !disabledQuestIds.Contains(q.Id);
    }

    private bool FilterPvPKills(ExtractedQuestData q)
    {
        return q.QuestTemplate.RequiredPlayerKills == 0;
    }

    private bool FilterFlags(ExtractedQuestData q)
    {
        int flags = (int)q.Flags;
        foreach (int check in EXCLUDED_FLAGS)
        {
            if ((flags & check) != 0)
            {
                return false;
            }
        }
        return true;
    }

    private bool FilterSpecialFlags(ExtractedQuestData q)
    {
        if (q.QuestTemplate.QuestTemplateAddon == null)
        {
            return true;
        }

        int flags = (int)q.QuestTemplate.QuestTemplateAddon.SpecialFlags;
        foreach (int check in EXCLUDED_SPECIAL_FLAGS)
        {
            if ((flags & check) != 0)
            {
                return false;
            }
        }
        return true;
    }

    private bool FilterQuestSort(ExtractedQuestData q)
    {
        return q.QuestSort == null || !EXCLUDED_QUEST_SORTS.Contains(q.QuestSort.ID);
    }

    private bool FilterQuestInfo(ExtractedQuestData q)
    {
        return q.QuestInfo == null || !EXCLUDED_QUEST_INFOS.Contains(q.QuestInfo.ID);
    }

    private bool FilterPooled(ExtractedQuestData q)
    {
        return q.QuestTemplate.Pool == null;
    }

    private bool FilterMissingStarters(ExtractedQuestData q)
    {
        return
            q.QuestTemplate.CreatureStarters.Count > 0 ||
            q.QuestTemplate.GameObjectStarters.Count > 0 ||
            q.QuestTemplate.ItemTemplateStarters.Count > 0;
    }

    private bool FilterMissingEnders(ExtractedQuestData q)
    {
        return q.QuestTemplate.CreatureEnders.Count > 0 || q.QuestTemplate.GameObjectEnders.Count > 0;
    }

    private bool FilterNoAllowedRaces(ExtractedQuestData q)
    {
        return q.Races == null || q.Races.Count > 0;
    }

    private bool FilterWorldEvents(ExtractedQuestData q)
    {
        return q.QuestTemplate.GameEventCreatureQuests.Count == 0 &&
            q.QuestTemplate.GameEventGameObjectQuests.Count == 0 &&
            !DARKMOON_FAIRE_QUESTS.Contains(q.Id);
    }

    private bool FilterReputation(ExtractedQuestData q)
    {
        var addon = q.QuestTemplate.QuestTemplateAddon;
        return addon == null || (addon.RequiredMinRepFaction == 0 && addon.RequiredMaxRepFaction == 0);
    }

    private bool FilterProfession(ExtractedQuestData q)
    {
        var addon = q.QuestTemplate.QuestTemplateAddon;
        return addon == null || addon.RequiredSkillId == 0;
    }

    private bool FilterDeprecatedName(ExtractedQuestData q)
    {
        // Heuristics derived from https://github.com/azerothcore/aowow/blob/8e247b8798e71f834d6c5c25a610856d5558a7a4/setup/tools/sqlgen/quests.ss.php#L182-L190
        // Quests whose titles contain these patterns are considered disabled or deprecated.
        string title = q.Title;
        return !title.Contains('<') &&
               !title.Contains('[') &&
               !title.Contains("deprecated", StringComparison.InvariantCultureIgnoreCase) &&
               !title.Contains("unused", StringComparison.InvariantCultureIgnoreCase) &&
               !title.StartsWith("temp ", StringComparison.InvariantCultureIgnoreCase);
    }

    private bool FilterUnreachableMinLevel(ExtractedQuestData q)
    {
        return q.MinLevel <= MAX_CHARACTER_LEVEL;
    }

    private bool FilterCollectorsEdition(ExtractedQuestData q)
    {
        return !COLLECTORS_EDITION_QUESTS.Contains(q.Id);
    }

    private bool FilterClothDonation(ExtractedQuestData q)
    {
        return !CLOTH_DONATION_QUESTS.Contains(q.Id);
    }

    private bool FilterLearnToRide(ExtractedQuestData q)
    {
        return !LEARN_TO_RIDE_QUESTS.Contains(q.Id);
    }

    private void FilterUnavailablePrerequisites(List<ExtractedQuestData> quests)
    {
        var dict = quests.ToDictionary(q => q.Id, q => q);

        bool removed;
        do
        {
            removed = false;
            foreach (var quest in quests.Where(IsUnavailable).ToList())
            {
                quests.Remove(quest);
                dict.Remove(quest.Id);
                removed = true;

                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Filtered out \"{title}\" ({id}): {reason}", quest.Title, quest.Id, "unavailable prerequisites");
                }
            }
        } while (removed);

        // Prerequisites are collected before the quests get filtered, so the survivors still point at
        // quests that are gone. Every survivor kept an available RequiresAny prerequisite and all of
        // its RequiresAll ones, which leaves those references meaningless to whoever reads the output.
        foreach (var quest in quests)
        {
            quest.RequiresAny.RemoveAll(id => !dict.ContainsKey(id));
            quest.RequiresAll.RemoveAll(id => !dict.ContainsKey(id));
        }

        bool IsUnavailable(ExtractedQuestData q)
        {
            // If multiple quests share the same NextQuestId; only one must be completed.
            // Filter out only when none of the requirements are available.
            if (q.RequiresAny.Count > 0 && q.RequiresAny.All(r => !dict.ContainsKey(r)))
            {
                return true;
            }

            // Negative ExclusiveGroup: all quests in the group must be completed to unlock the next quest.
            // Filter out if any of the required quests is unavailable.
            if (q.RequiresAll.Count > 0 && q.RequiresAll.Any(r => !dict.ContainsKey(r)))
            {
                return true;
            }

            return false;
        }
    }

    private void FillQuestPrerequisites(List<ExtractedQuestData> quests)
    {
        // Group quests by negative ExclusiveGroup so we can identify "all required" chains.
        // A negative ExclusiveGroup means every quest in that group must be completed before
        // the quest pointed to by their NextQuestId becomes available. Members of one group do not
        // always agree on that target, so it is part of the key: keying on the group alone would pin
        // the whole group to one member's target and leave the other targets without prerequisites.
        var negativeExclusiveGroups = quests
            .Where(q => q.QuestTemplate.QuestTemplateAddon != null)
            .Where(q =>
                q.QuestTemplate.QuestTemplateAddon.ExclusiveGroup < 0 &&
                q.QuestTemplate.QuestTemplateAddon.NextQuestId > 0
            )
            .GroupBy(q => (
                q.QuestTemplate.QuestTemplateAddon.ExclusiveGroup,
                q.QuestTemplate.QuestTemplateAddon.NextQuestId
            ));

        var questById = quests.ToDictionary(q => q.Id);

        foreach (var group in negativeExclusiveGroups)
        {
            if (!questById.TryGetValue(group.Key.NextQuestId, out var targetQuest))
            {
                continue;
            }

            // All members of the group are mandatory prerequisites for the target quest.
            var known = targetQuest.RequiresAll.ToHashSet();
            targetQuest.RequiresAll.AddRange(group.Select(q => q.Id).Where(known.Add));
        }

        var questsByNextId = quests
            .Where(q => (q.QuestTemplate.QuestTemplateAddon?.NextQuestId ?? 0) != 0)
            .GroupBy(q => q.QuestTemplate.QuestTemplateAddon?.NextQuestId ?? 0)
            .ToDictionary(grp => grp.Key, grp => grp.ToList());

        foreach (var quest in quests)
        {
            if (questsByNextId.TryGetValue(quest.Id, out var reqs))
            {
                // A quest reached through NextQuestId may already be seeded from PrevQuestId, so the
                // ids seen so far are tracked apart from the list being appended to.
                var known = quest.RequiresAny.ToHashSet();
                quest.RequiresAny.AddRange(reqs.Select(other => other.Id).Where(known.Add));
            }
        }

        // A mandatory prerequisite already satisfies every "one of" clause it takes part in. The
        // PrevQuestId seed lands in RequiresAny before the groups above fill RequiresAll, so both
        // lists have to settle before the redundant entries can be told apart.
        foreach (var quest in quests)
        {
            quest.RequiresAny.RemoveAll(quest.RequiresAll.Contains);
        }
    }

    private void FillQuestDisplayTitles(List<ExtractedQuestData> quests)
    {
        foreach (var group in quests.GroupBy(q => q.Title))
        {
            var groupList = group.ToList();

            if (groupList.Count == 1)
            {
                groupList[0].DisplayTitle = groupList[0].Title;
                continue;
            }

            var ordered = OrderQuestsByChain(groupList);
            ordered[0].DisplayTitle = ordered[0].Title;
            for (var i = 1; i < ordered.Count; i++)
            {
                ordered[i].DisplayTitle = $"{ordered[i].Title} ({i + 1})";
            }
        }
    }

    private static List<ExtractedQuestData> OrderQuestsByChain(List<ExtractedQuestData> quests)
    {
        var questIds = quests.Select(q => q.Id).ToHashSet();
        var questById = quests.ToDictionary(q => q.Id);

        var inDegree = quests.ToDictionary(q => q.Id, _ => 0);
        var successors = quests.ToDictionary(q => q.Id, _ => new List<uint>());

        foreach (var quest in quests)
        {
            foreach (var prereqId in quest.RequiresAll.Concat(quest.RequiresAny).Where(id => questIds.Contains(id)).Distinct())
            {
                inDegree[quest.Id]++;
                successors[prereqId].Add(quest.Id);
            }
        }

        // Kahn's algorithm; SortedSet ensures Id-order tiebreaking at each level
        var queue = new SortedSet<uint>(quests.Where(q => inDegree[q.Id] == 0).Select(q => q.Id));
        var result = new List<ExtractedQuestData>(quests.Count);

        while (queue.Count > 0)
        {
            var currentId = queue.Min;
            queue.Remove(currentId);
            result.Add(questById[currentId]);

            foreach (var successorId in successors[currentId])
            {
                if (--inDegree[successorId] == 0)
                {
                    queue.Add(successorId);
                }
            }
        }

        // Fallback: append any remaining quests (cycle or disconnected) sorted by Id
        if (result.Count < quests.Count)
        {
            var resultIds = result.Select(q => q.Id).ToHashSet();
            result.AddRange(quests.Where(q => !resultIds.Contains(q.Id)).OrderBy(q => q.Id));
        }

        return result;
    }

    private async Task FillCreatureZones(List<ExtractedQuestData> quests)
    {
        var entries = quests.SelectMany(q => q.QuestTemplate.CreatureStarters).Select(c => c.Id)
            .Concat(quests.SelectMany(q => q.QuestTemplate.CreatureEnders).Select(c => c.Id))
            .ToHashSet();

        var spawns = await db.Creatures
            .Where(c => entries.Contains(c.Id) && c.ZoneId != 0)
            .Select(c => new { c.Id, c.ZoneId })
            .ToListAsync();

        var zonesByEntry = spawns
            .GroupBy(c => c.Id)
            .ToDictionary(g => g.Key, g => g.Select(c => (int)c.ZoneId).ToHashSet());

        WarnOnPoorZoneCoverage("creature", "Calculate.Creature.Zone.Area.Data", entries.Count, zonesByEntry.Count);

        foreach (var q in quests)
        {
            AddZones(q.StartZones, q.QuestTemplate.CreatureStarters.Select(s => s.Id), zonesByEntry);
            AddZones(q.EndZones, q.QuestTemplate.CreatureEnders.Select(e => e.Id), zonesByEntry);
        }
    }

    private async Task FillGameObjectZones(List<ExtractedQuestData> quests)
    {
        var entries = quests.SelectMany(q => q.QuestTemplate.GameObjectStarters).Select(gobj => gobj.Id)
            .Concat(quests.SelectMany(q => q.QuestTemplate.GameObjectEnders).Select(gobj => gobj.Id))
            .ToHashSet();

        var spawns = await db.Gameobjects
            .Where(gobj => entries.Contains(gobj.Id) && gobj.ZoneId != 0)
            .Select(gobj => new { gobj.Id, gobj.ZoneId })
            .ToListAsync();

        var zonesByEntry = spawns
            .GroupBy(gobj => gobj.Id)
            .ToDictionary(g => g.Key, g => g.Select(gobj => (int)gobj.ZoneId).ToHashSet());

        WarnOnPoorZoneCoverage("gameobject", "Calculate.Gameobject.Zone.Area.Data", entries.Count, zonesByEntry.Count);

        foreach (var q in quests)
        {
            AddZones(q.StartZones, q.QuestTemplate.GameObjectStarters.Select(s => s.Id), zonesByEntry);
            AddZones(q.EndZones, q.QuestTemplate.GameObjectEnders.Select(e => e.Id), zonesByEntry);
        }
    }

    /// <summary>
    /// Appends the zones the given quest givers spawn in, skipping the ones already listed and the ones
    /// whose id has no AreaTable row.
    /// </summary>
    private void AddZones(List<ExtractedArea> target, IEnumerable<uint> questGiverIds, Dictionary<uint, HashSet<int>> zonesByEntry)
    {
        var seen = target.Select(a => a.Id).ToHashSet();

        foreach (uint questGiverId in questGiverIds)
        {
            if (!zonesByEntry.TryGetValue(questGiverId, out var zoneIds))
            {
                continue;
            }

            foreach (int zoneId in zoneIds)
            {
                var area = ExtractedArea.FromAreaTable(areas.Get(zoneId));
                if (area == null)
                {
                    // Spawn tables outlive the client data, and a modified core can hold zone ids the
                    // 3.3.5 AreaTable never had. Nothing to name the zone with, so it is dropped.
                    if (unknownZoneIds.Add(zoneId))
                    {
                        logger.LogWarning("Zone {zoneId} is not in AreaTable.dbc, ignoring it.", zoneId);
                    }
                    continue;
                }

                if (seen.Add(area.Id))
                {
                    target.Add(area);
                }
            }
        }
    }

    /// <summary>
    /// AzerothCore leaves the denormalised `zoneId` spawn column at 0 until worldserver has run once
    /// with the matching Calculate.*.Zone.Area.Data option on, which would silently strip the zones off
    /// nearly every quest instead of failing.
    /// </summary>
    private void WarnOnPoorZoneCoverage(string source, string configOption, int questGiverCount, int resolvedCount)
    {
        if (questGiverCount == 0)
        {
            return;
        }

        double coverage = (double)resolvedCount / questGiverCount;
        if (coverage >= MIN_ZONE_COVERAGE)
        {
            return;
        }

        logger.LogWarning(
            "Only {resolved} of {total} quest giver {source} entries resolved a zone ({coverage:P1}). " +
            "Run worldserver once with \"{option} = 1\" in worldserver.conf to populate the spawn tables, " +
            "otherwise most quests are extracted without zones.",
            resolvedCount, questGiverCount, source, coverage, configOption);
    }

    private async Task FillPOIZones(List<ExtractedQuestData> quests)
    {
        var questIds = quests.Select(q => q.Id).ToHashSet();

        var poisByQuestId = await db.QuestPois
            .Where(p => questIds.Contains(p.QuestId))
            .GroupBy(p => p.QuestId)
            .ToDictionaryAsync(g => g.Key, g => g.ToList());

        foreach (var quest in quests)
        {
            if (!poisByQuestId.TryGetValue(quest.Id, out var questPois))
            {
                continue;
            }

            foreach (var poi in questPois)
            {
                var worldMapArea = worldMapAreas.Get((int)poi.WorldMapAreaId);
                if (worldMapArea == null)
                {
                    continue;
                }

                var area = ExtractedArea.FromAreaTable(areas.Get(worldMapArea.AreaID));
                if (area == null)
                {
                    continue;
                }

                if (poi.ObjectiveIndex == -1)
                {
                    if (!quest.EndZones.Any(a => a.Id == area.Id))
                    {
                        quest.EndZones.Add(area);
                    }
                }
                else
                {
                    if (!quest.ObjectiveZones.Any(a => a.Id == area.Id))
                    {
                        quest.ObjectiveZones.Add(area);
                    }
                }
            }
        }
    }
}
