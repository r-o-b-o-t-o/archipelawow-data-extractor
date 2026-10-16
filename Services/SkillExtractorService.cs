using System.Text.Encodings.Web;
using System.Text.Json;
using ArchipelaWoW.DataExtractor.Dbc;
using ArchipelaWoW.DataExtractor.Entities.World;
using ArchipelaWoW.DataExtractor.Services.Repositories;
using Microsoft.Extensions.Logging;
using static ArchipelaWoW.DataExtractor.Services.CharacterSkills;

namespace ArchipelaWoW.DataExtractor.Services;

public class ExtractedSkillData
{
    /// <summary>The SkillLine.dbc id, which the server module checks the locations on.</summary>
    public int Id { get; set; }
    public string Name { get; set; }

    /// <summary>
    /// The weapon proficiency a weapon master sells for the skill, which is how a character not created holding it
    /// comes by it, or 0 for Defense, which no weapon master sells.
    /// </summary>
    public int Spell { get; set; }

    /// <summary>
    /// The races of each class created holding the skill, keyed by class id. The others of a class open to it buy
    /// it, and are named by the <c>classRaces</c> of its spell in spells.json.
    /// </summary>
    public Dictionary<int, int> StartingClassRaces { get; set; } = [];

    /// <summary>
    /// The zones a vendor sells a weapon of the skill in, each with the lowest level such a weapon can be
    /// equipped at and the mask of the races that can buy it there, 0 for every race. Empty for Defense.
    /// </summary>
    public List<ExtractedVendorZone> VendorZones { get; set; } = [];
}

public class ExtractedVendorZone
{
    public int Zone { get; set; }
    public int Level { get; set; }
    public int Races { get; set; }
}

/// <summary>
/// Builds the skill table archipelawow ships: the weapon skills and Defense, which rise with use up to five times the
/// character's level.
/// </summary>
public class SkillExtractorService(
        ILogger<SkillExtractorService> logger,
        TrainerRepository trainersRepo,
        VendorRepository vendorsRepo,
        CharacterSkills characterSkills,
        SpellContainer spells,
        SkillLineContainer skillLines,
        SkillLineAbilityContainer skillLineAbilities,
        SkillRaceClassInfoContainer skillRaceClassInfos,
        FactionTemplateContainer factionTemplates,
        ChrRacesContainer races
    )
{
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // SkillLine.CategoryID, see SharedDefines.h: the skills the character pane lists under Weapon Skills
    private const int SKILL_CATEGORY_WEAPON = 6;

    // SkillRaceClassInfo.Flags, see DBCEnums.h. Such a skill is set to its cap at every level rather than raised by
    // use, which leaves out Dual Wield.
    private const int SKILL_FLAG_ALWAYS_MAX_VALUE = 0x10;

    // Unarmed is left out too: it rises by fighting with no weapon in hand, which no class is played with.
    private const int SKILL_UNARMED = 162;

    public async Task ExtractSkills()
    {
        string outDir = OutputDirectory.Prepare();

        var createSkills = await trainersRepo.GetCreateSkills();
        var vendorWeapons = await vendorsRepo.GetVendorWeapons();
        var alwaysMax = skillRaceClassInfos
            .Where(info => (info.Flags & SKILL_FLAG_ALWAYS_MAX_VALUE) != 0)
            .Select(info => info.SkillID)
            .ToHashSet();
        var abilitiesBySkill = skillLineAbilities.ToLookup(ability => ability.SkillLine);

        List<ExtractedSkillData> collected = [.. skillLines
            .Where(skillLine => skillLine.CategoryID == SKILL_CATEGORY_WEAPON && !alwaysMax.Contains(skillLine.ID)
                && skillLine.ID != SKILL_UNARMED)
            .OrderBy(skillLine => skillLine.ID)
            .Select(skillLine =>
            {
                Spell proficiency = abilitiesBySkill[skillLine.ID]
                    .Select(ability => spells.Get(ability.Spell))
                    .FirstOrDefault(spell => spell != null && SpellExtractorService.IsWeaponProficiency(spell));
                return new ExtractedSkillData()
                {
                    Id = skillLine.ID,
                    Name = skillLine.DisplayNameLang,
                    Spell = proficiency?.ID ?? 0,
                    StartingClassRaces = StartingClassRaces(createSkills, skillLine.ID),
                    VendorZones = proficiency == null ? [] : VendorZones(vendorWeapons, proficiency.EquippedItemSubclass),
                };
            })];

        string outFile = Path.Combine(outDir, "skills.json");
        await File.WriteAllTextAsync(outFile, JsonSerializer.Serialize(collected, jsonOptions));

        logger.LogInformation("Wrote {count} skills to {file}.", collected.Count, outFile);
    }

    /// <summary>
    /// Where a weapon of one of the subclasses of <paramref name="subclassMask"/> is sold, the way
    /// <see cref="ExtractedSkillData.VendorZones"/> lists it: for each zone and race, the lowest level of
    /// the weapons a vendor there will sell to that race.
    /// </summary>
    private List<ExtractedVendorZone> VendorZones(List<VendorWeapon> vendorWeapons, int subclassMask)
    {
        Dictionary<(int Zone, int Race), int> lowestLevels = [];
        foreach (VendorWeapon weapon in vendorWeapons.Where(weapon => (subclassMask & (1 << weapon.Subclass)) != 0))
        {
            FactionTemplate vendorFaction = factionTemplates.Get(weapon.Faction);
            foreach (int raceId in PLAYABLE_RACE_IDS.Where(raceId => (Widen(weapon.AllowableRace, ALL_RACES_MASK) & RaceBit(raceId)) != 0))
            {
                FactionTemplate raceFaction = factionTemplates.Get(races.Get(raceId)?.FactionID ?? 0);
                if (vendorFaction != null && raceFaction != null && !FactionRelations.WillTalkTo(vendorFaction, raceFaction))
                {
                    continue;
                }

                var key = (weapon.ZoneId, raceId);
                lowestLevels[key] = Math.Min(lowestLevels.GetValueOrDefault(key, int.MaxValue), weapon.RequiredLevel);
            }
        }

        return [.. lowestLevels
            .GroupBy(entry => (entry.Key.Zone, Level: entry.Value))
            .Select(group => (group.Key.Zone, group.Key.Level, Races: group.Aggregate(0, (mask, entry) => mask | RaceBit(entry.Key.Race))))
            .OrderBy(entry => entry.Zone)
            .ThenBy(entry => entry.Level)
            .Select(entry => new ExtractedVendorZone()
            {
                Zone = entry.Zone,
                Level = entry.Level,
                // Every race written as 0, as RaceMask does
                Races = entry.Races == PLAYABLE_RACES_MASK ? 0 : entry.Races,
            })];
    }

    private Dictionary<int, int> StartingClassRaces(List<PlayercreateinfoSkill> createSkills, int skillId)
    {
        Dictionary<int, int> classRaces = [];
        foreach (int classId in RANDOMIZED_CLASS_IDS)
        {
            int raceMask = PLAYABLE_RACE_IDS
                .Where(raceId => characterSkills.IsCreatedHolding(createSkills, skillId, raceId, classId))
                .Aggregate(0, (mask, raceId) => mask | RaceBit(raceId));
            if (raceMask != 0)
            {
                classRaces[classId] = raceMask;
            }
        }
        return classRaces;
    }
}
