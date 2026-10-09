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
    /// comes by it, or 0 for the skills no weapon master sells: Defense and Unarmed.
    /// </summary>
    public int Spell { get; set; }

    /// <summary>
    /// The races of each class created holding the skill, keyed by class id. The others of a class open to it buy
    /// it, and are named by the <c>classRaces</c> of its spell in spells.json.
    /// </summary>
    public Dictionary<int, int> StartingClassRaces { get; set; } = [];
}

/// <summary>
/// Builds the skill table archipelawow ships: the weapon skills, Defense and Unarmed among them, which rise with use
/// up to five times the character's level.
/// </summary>
public class SkillExtractorService(
        ILogger<SkillExtractorService> logger,
        TrainerRepository trainersRepo,
        CharacterSkills characterSkills,
        SpellContainer spells,
        SkillLineContainer skillLines,
        SkillLineAbilityContainer skillLineAbilities,
        SkillRaceClassInfoContainer skillRaceClassInfos
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

    public async Task ExtractSkills()
    {
        string outDir = OutputDirectory.Prepare();

        var createSkills = await trainersRepo.GetCreateSkills();
        var alwaysMax = skillRaceClassInfos
            .Where(info => (info.Flags & SKILL_FLAG_ALWAYS_MAX_VALUE) != 0)
            .Select(info => info.SkillID)
            .ToHashSet();
        var abilitiesBySkill = skillLineAbilities.ToLookup(ability => ability.SkillLine);

        List<ExtractedSkillData> collected = [.. skillLines
            .Where(skillLine => skillLine.CategoryID == SKILL_CATEGORY_WEAPON && !alwaysMax.Contains(skillLine.ID))
            .OrderBy(skillLine => skillLine.ID)
            .Select(skillLine => new ExtractedSkillData()
            {
                Id = skillLine.ID,
                Name = skillLine.DisplayNameLang,
                Spell = abilitiesBySkill[skillLine.ID]
                    .Select(ability => spells.Get(ability.Spell))
                    .FirstOrDefault(spell => spell != null && SpellExtractorService.IsWeaponProficiency(spell))?.ID ?? 0,
                StartingClassRaces = StartingClassRaces(createSkills, skillLine.ID),
            })];

        string outFile = Path.Combine(outDir, "skills.json");
        await File.WriteAllTextAsync(outFile, JsonSerializer.Serialize(collected, jsonOptions));

        logger.LogInformation("Wrote {count} skills to {file}.", collected.Count, outFile);
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
