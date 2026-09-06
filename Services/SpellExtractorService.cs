using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArchipelaWoW.DataExtractor.Dbc;
using ArchipelaWoW.DataExtractor.Entities.World;
using ArchipelaWoW.DataExtractor.Services.Repositories;
using Microsoft.Extensions.Logging;

namespace ArchipelaWoW.DataExtractor.Services;

/// <summary>
/// What a spell entry is, which decides whether archipelawow puts it in the pool at all.
/// </summary>
public enum SpellKind
{
    Class,
    Mount,
    Riding,
    Starter,
    Weapon,
}

public class ExtractedSpellData
{
    public int Id { get; set; }
    public string Name { get; set; }

    /// <summary>
    /// The class whose trainer teaches this, or 0 for the entries no single class owns: the riding
    /// ranks and the weapon skills, which name their classes in <see cref="ClassRaces"/> instead.
    /// </summary>
    public int ClassId { get; set; }

    /// <summary>
    /// For a weapon skill, the races of each class that can still buy it from a weapon master, keyed
    /// by class id. A class the skill is closed to, and a race and class created already holding it --
    /// a warrior with Thrown, a troll hunter with Bows -- is left out.
    /// </summary>
    public Dictionary<int, int> ClassRaces { get; set; } = [];
    public int ReqLevel { get; set; }
    public int ReqSkillRank { get; set; }

    /// <summary>
    /// What this entry teaches when it is cast, for the few trainer entries that wrap a spell rather
    /// than being one -- a paladin's Judgement, the class mounts, Flight Form. The trainer casts those
    /// instead of teaching them, so what comes out of them has to be known to be held back.
    /// </summary>
    public List<int> TaughtSpells { get; set; } = [];
    public int RaceMask { get; set; }
    public int Factions { get; set; }
    public int Expansion { get; set; }
    public SpellKind Kind { get; set; }
}

/// <summary>
/// Builds the spell table archipelawow ships, by joining the trainer lists of an AzerothCore world
/// database to the names, ranks and effects in the client's DBCs.
/// </summary>
public class SpellExtractorService(
        ILogger<SpellExtractorService> logger,
        TrainerRepository trainersRepo,
        SpellContainer spells,
        SkillLineContainer skillLines,
        SkillLineAbilityContainer skillLineAbilities,
        SkillRaceClassInfoContainer skillRaceClassInfos,
        FactionTemplateContainer factionTemplates
    )
{
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    // Trainer.Type in the world database
    private const int TRAINER_TYPE_CLASS = 0;
    private const int TRAINER_TYPE_MOUNT = 1;
    private const int TRAINER_TYPE_TRADESKILL = 2;

    // SkillLineAbility.AcquireMethod, see DBCEnums.h
    private const int ACQUIRE_ON_SKILL_VALUE = 1;
    private const int ACQUIRE_ON_SKILL_LEARN = 2;

    // SkillLine.CategoryID, see SharedDefines.h. Only the class category holds spells a player thinks
    // of as their class kit; the others carry racials, languages, weapon skills and professions.
    private const int SKILL_CATEGORY_CLASS = 7;

    // Spell effects, see SharedDefines.h. A proficiency spell teaches the use of a kind of equipment
    // rather than an ability, and which kind decides whether it belongs in the pool: the weapon skills
    // a weapon master sells are in, while the armor ones -- Mail, Plate Mail -- and Dual Wield stay
    // out, since a character that loses those cannot wear or hold what its class is built around.
    private const int SPELL_EFFECT_LEARN_SPELL = 36;
    private const int SPELL_EFFECT_DUAL_WIELD = 40;
    private const int SPELL_EFFECT_PROFICIENCY = 60;

    // ItemTemplate classes, see SharedDefines.h. A proficiency spell names the one it grants in the
    // spell's own equipped-item class.
    private const int ITEM_CLASS_WEAPON = 2;

    // SPELL_ATTR0_DO_NOT_DISPLAY, see SharedDefines.h. The skill lines hand out a handful of
    // bookkeeping spells alongside the real abilities -- "Maelstrom Ready!", "Deep Freeze Immunity
    // State" -- which never show up in the spellbook and would be baffling as checks.
    private const int SPELL_ATTR0_DO_NOT_DISPLAY = 0x00000080;

    // A mage's travel spells are excluded from the pool: they are two dozen entries on their own,
    // which would leave the mage with twice the checks of any other class.
    private static readonly string[] EXCLUDED_NAME_PREFIXES = ["Teleport: ", "Portal: "];

    // Auto Shot (75) is the whole of a hunter's ranged attack, and a hunter without it has no class
    // left to play until the multiworld hands it back. It stays with the character.
    private static readonly HashSet<int> EXCLUDED_SPELL_IDS = [75];

    // FactionTemplate.FactionGroup, the team bits a faction belongs to
    // (FACTION_MASK_ALLIANCE / FACTION_MASK_HORDE in DBCEnums.h)
    private const int FACTION_MASK_ALLIANCE = 2;
    private const int FACTION_MASK_HORDE = 4;

    // Continent maps, and the expansion each one arrived with. A trainer only ever found on one of
    // them can only be reached by a seed that goes that far.
    private const int MAP_OUTLAND = 530;
    private const int MAP_NORTHREND = 571;
    private const int EXPANSION_CLASSIC = 0;
    private const int EXPANSION_BURNING_CRUSADE = 1;
    private const int EXPANSION_WRATH = 2;
    private static readonly Dictionary<int, int> MAP_EXPANSIONS = new()
    {
        [MAP_OUTLAND] = EXPANSION_BURNING_CRUSADE,
        [MAP_NORTHREND] = EXPANSION_WRATH,
    };

    // ExtractedSpellData.Factions: which team's trainers teach a spell, zero when both do
    private const int FACTION_ANY = 0;
    private const int FACTION_ALLIANCE = 1;
    private const int FACTION_HORDE = 2;

    // The classes ArchipelaWoW can roll a seed for. Listed rather than counted through for two
    // reasons: Wrath has no class 10, so the ids run 1-9 and then 11 and a mask bit for that gap does
    // turn up in the DBCs; and the death knight (6) is left out on purpose, since it starts at level
    // 55 with a kit of its own and ArchipelaWoW does not offer it.
    private static readonly int[] RANDOMIZED_CLASS_IDS = [1, 2, 3, 4, 5, 7, 8, 9, 11];

    // The races a Wrath character can be. Listed for the same reason as the classes above: race 9, the
    // goblin, holds a mask bit and a ChrRaces row of its own but is not playable until Cataclysm.
    private static readonly int[] PLAYABLE_RACE_IDS = [1, 2, 3, 4, 5, 6, 7, 8, 10, 11];
    private static readonly int PLAYABLE_RACES_MASK = PLAYABLE_RACE_IDS.Aggregate(0, (mask, raceId) => mask | RaceBit(raceId));

    // A race or class mask of zero means "every one of them" throughout the DBCs and the create-info
    // tables, so it is widened to these before two masks are intersected.
    private const int ALL_RACES_MASK = 0x7FF;
    private static readonly int ALL_CLASSES_MASK = RANDOMIZED_CLASS_IDS.Aggregate(0, (mask, classId) => mask | ClassBit(classId));

    private Dictionary<int, List<SkillLineAbility>> abilitiesBySpell;
    private Dictionary<int, List<SkillLineAbility>> abilitiesBySkill;
    private Dictionary<int, List<SkillRaceClassInfo>> raceClassInfoBySkill;
    private HashSet<int> ladderSpells;
    private HashSet<int> classSkillLines;

    public async Task ExtractSpells()
    {
        string outDir = OutputDirectory.Prepare();

        BuildAbilityIndexes();
        BuildRaceClassIndex();
        ladderSpells = CollectLadderSpells();
        classSkillLines = [.. skillLines.Where(skillLine => skillLine.CategoryID == SKILL_CATEGORY_CLASS).Select(skillLine => skillLine.ID)];

        var trainers = await trainersRepo.GetTrainersWithSpells();
        var higherRanks = await trainersRepo.GetHigherRankSpellIds();
        var createSkills = await trainersRepo.GetCreateSkills();
        var trainerExpansions = await CollectTrainerExpansions();
        var trainerTeams = await CollectTrainerTeams();

        // Starters are worked out first and then kept out of the trainer sweep. A realm running the
        // module has its archipelawow_world_007 update applied, which puts the starting abilities on
        // the class trainers so they can be bought back; without this the extract would call them
        // trainer spells on such a realm and starter spells on any other.
        var starters = CollectStarterSpells(createSkills);
        var starterKeys = starters.Select(spell => (spell.ClassId, spell.Id)).ToHashSet();

        List<ExtractedSpellData> collected =
        [
            .. CollectTrainerSpells(trainers, higherRanks, trainerTeams, trainerExpansions)
                .Where(spell => !starterKeys.Contains((spell.ClassId, spell.Id))),
            .. CollectWeaponSkills(trainers, createSkills, trainerExpansions),
            .. starters,
        ];

        collected = Deduplicate(collected);
        ReportDuplicateNames(collected);

        string outFile = Path.Combine(outDir, "spells.json");
        await File.WriteAllTextAsync(outFile, JsonSerializer.Serialize(collected, jsonOptions));

        logger.LogInformation("Wrote {count} spells to {file}.", collected.Count, outFile);
    }

    private void BuildAbilityIndexes()
    {
        abilitiesBySpell = [];
        abilitiesBySkill = [];

        foreach (SkillLineAbility ability in skillLineAbilities)
        {
            Index(abilitiesBySpell, ability.Spell, ability);
            Index(abilitiesBySkill, ability.SkillLine, ability);
        }

        static void Index(Dictionary<int, List<SkillLineAbility>> index, int key, SkillLineAbility ability)
        {
            if (!index.TryGetValue(key, out var abilities))
            {
                index[key] = abilities = [];
            }
            abilities.Add(ability);
        }
    }

    private void BuildRaceClassIndex()
    {
        raceClassInfoBySkill = [];

        foreach (SkillRaceClassInfo info in skillRaceClassInfos)
        {
            if (!raceClassInfoBySkill.TryGetValue(info.SkillID, out var infos))
            {
                raceClassInfoBySkill[info.SkillID] = infos = [];
            }
            infos.Add(info);
        }
    }

    /// <summary>
    /// Every spell that is a rung of some ladder: one that replaces another, or is replaced by one.
    ///
    /// This is what tells the riding ranks apart from Cold Weather Flying. The four ranks replace each
    /// other in turn, each one a faster mount than the last, while Cold Weather Flying replaces nothing
    /// and is replaced by nothing: it is a permit to fly over Northrend that an Expert or Artisan rider
    /// buys on top of what they already have.
    /// </summary>
    private HashSet<int> CollectLadderSpells()
    {
        HashSet<int> ladder = [];
        foreach ((int spellId, var abilities) in abilitiesBySpell)
        {
            foreach (SkillLineAbility ability in abilities)
            {
                if (ability.SupercededBySpell != 0)
                {
                    ladder.Add(spellId);
                    ladder.Add(ability.SupercededBySpell);
                }
            }
        }
        return ladder;
    }

    /// <summary>
    /// Trainer id to the earliest expansion one of its creatures is spawned in.
    ///
    /// The flying ranks of riding are only sold in Outland and Cold Weather Flying only in Northrend,
    /// so a seed that stops at level 60 has no way to reach the trainer however high its character
    /// gets. Taking the lowest map any of a trainer's creatures stands on keeps that honest without
    /// naming spells one by one.
    /// </summary>
    private async Task<Dictionary<uint, int>> CollectTrainerExpansions()
    {
        var lowestMaps = await trainersRepo.GetTrainerLowestSpawnMaps();
        return lowestMaps.ToDictionary(
            entry => entry.Key,
            entry => MAP_EXPANSIONS.GetValueOrDefault(entry.Value, EXPANSION_CLASSIC));
    }

    /// <summary>
    /// Trainer id to the teams whose creatures act as that trainer.
    ///
    /// A handful of class spells only ever appear on one team's trainers -- a paladin's Summon Warhorse
    /// against a blood elf paladin's own copy of it -- and they share a name, so a slot has to be able
    /// to drop the one its faction can never train.
    /// </summary>
    private async Task<Dictionary<uint, int>> CollectTrainerTeams()
    {
        var trainerCreatures = await trainersRepo.GetTrainerCreatures();

        Dictionary<uint, int> teams = [];
        foreach (TrainerCreature creature in trainerCreatures)
        {
            teams[creature.TrainerId] = teams.GetValueOrDefault(creature.TrainerId, FACTION_ANY) | TeamOf(creature.Faction);
        }
        return teams;
    }

    private int TeamOf(int factionTemplateId)
    {
        FactionTemplate factionTemplate = factionTemplates.Get(factionTemplateId);
        if (factionTemplate == null)
        {
            return FACTION_ANY;
        }

        int team = FACTION_ANY;
        if ((factionTemplate.FactionGroup & FACTION_MASK_ALLIANCE) != 0)
        {
            team |= FACTION_ALLIANCE;
        }
        if ((factionTemplate.FactionGroup & FACTION_MASK_HORDE) != 0)
        {
            team |= FACTION_HORDE;
        }
        return team;
    }

    /// <summary>
    /// Every first-rank spell a class trainer teaches, plus the riding ranks the mount trainers do.
    ///
    /// Only first ranks become checks: shuffling every rank would multiply the pool several times over
    /// for no added variety, and a character handed the first rank can train the rest from its trainer
    /// as usual.
    /// </summary>
    private List<ExtractedSpellData> CollectTrainerSpells(List<Trainer> trainers, HashSet<uint> higherRanks,
        Dictionary<uint, int> trainerTeams, Dictionary<uint, int> trainerExpansions)
    {
        List<ExtractedSpellData> collected = [];

        foreach (Trainer trainer in trainers.Where(t => t.Type is TRAINER_TYPE_CLASS or TRAINER_TYPE_MOUNT))
        {
            // A class trainer names its class in Requirement, and the death knight has trainers of its
            // own. Nothing they sell can ever be reached, so their lists are skipped outright rather
            // than left to be filtered out further down.
            if (trainer.Type == TRAINER_TYPE_CLASS && !RANDOMIZED_CLASS_IDS.Contains((int)trainer.Requirement))
            {
                continue;
            }

            foreach (TrainerSpell trainerSpell in trainer.Spells)
            {
                int spellId = (int)trainerSpell.SpellId;
                Spell spell = spells.Get(spellId);
                if (spell == null || string.IsNullOrEmpty(spell.NameLang))
                {
                    continue;
                }

                if (trainer.Type == TRAINER_TYPE_MOUNT)
                {
                    // Every rank is kept, unlike the class spells: the ranks are the progression, and
                    // a character handed Journeyman Riding has no trainer path to Artisan without it
                    // also being in the pool. Mount trainers are keyed by race and all of them teach
                    // the same ranks, so the class is left open and the duplicates collapse in
                    // Deduplicate().
                    collected.Add(new ExtractedSpellData()
                    {
                        Id = spellId,
                        Name = spell.NameLang,
                        ReqLevel = trainerSpell.ReqLevel,
                        ReqSkillRank = (int)trainerSpell.ReqSkillRank,
                        TaughtSpells = TaughtSpells(spell),
                        Factions = FACTION_ANY,
                        Expansion = trainerExpansions.GetValueOrDefault(trainer.Id, EXPANSION_CLASSIC),
                        Kind = ladderSpells.Contains(spellId) ? SpellKind.Riding : SpellKind.Mount,
                    });
                    continue;
                }

                if (higherRanks.Contains(trainerSpell.SpellId) || IsExcluded(spell))
                {
                    continue;
                }

                collected.Add(new ExtractedSpellData()
                {
                    Id = spellId,
                    Name = spell.NameLang,
                    ClassId = (int)trainer.Requirement,
                    ReqLevel = trainerSpell.ReqLevel,
                    ReqSkillRank = (int)trainerSpell.ReqSkillRank,
                    TaughtSpells = TaughtSpells(spell),
                    RaceMask = RacesFor(AbilitiesOf(spellId), (int)trainer.Requirement),
                    Factions = trainerTeams.GetValueOrDefault(trainer.Id, FACTION_ANY),
                    Expansion = trainerExpansions.GetValueOrDefault(trainer.Id, EXPANSION_CLASSIC),
                    Kind = SpellKind.Class,
                });
            }
        }

        return collected;
    }

    /// <summary>
    /// Whether a character of this race and class is created already holding a skill.
    ///
    /// A weapon skill the character already has is not something a weapon master will ever sell, so it
    /// cannot become a check however many masters list it. Mirrors what the core hands out at creation
    /// in ObjectMgr: both masks of the create-info row have to name the character, and
    /// SkillRaceClassInfo has to hold the skill for that pair. Which weapon a character starts with is
    /// as much a matter of race as of class -- a dwarf hunter is created holding Guns and a troll one
    /// Bows -- and the ones it did not start with are still there to be bought.
    /// </summary>
    private bool IsCreatedHolding(List<PlayercreateinfoSkill> createSkills, int skillId, int raceId, int classId)
    {
        return HasRaceClassInfo(skillId, raceId, classId)
            && createSkills.Any(createSkill => createSkill.Skill == skillId
                && (Widen((int)createSkill.RaceMask, ALL_RACES_MASK) & RaceBit(raceId)) != 0
                && (Widen((int)createSkill.ClassMask, ALL_CLASSES_MASK) & ClassBit(classId)) != 0);
    }

    /// <summary>
    /// The weapon proficiencies a weapon master sells, with the races and classes that can buy each.
    ///
    /// Weapon masters are keyed by nothing at all -- any class may talk to one -- so unlike a class
    /// trainer they carry no class of their own, and the classes come from the skill line instead. One
    /// entry covers the lot rather than one per class: the spell is the same whoever buys it, and the
    /// race and class pairs that can never buy it are simply left out of the map.
    /// </summary>
    private List<ExtractedSpellData> CollectWeaponSkills(List<Trainer> trainers,
        List<PlayercreateinfoSkill> createSkills, Dictionary<uint, int> trainerExpansions)
    {
        List<ExtractedSpellData> collected = [];

        // Who may buy a weapon skill turns on the spell and nothing else, while a weapon master that
        // sells it is one of dozens, so the answer is worked out once and shared by every copy. The
        // copies are collapsed in Deduplicate() and the map is never written to again.
        Dictionary<int, Dictionary<int, int>> classRacesBySpell = [];

        foreach (Trainer trainer in trainers.Where(t => t.Type == TRAINER_TYPE_TRADESKILL))
        {
            foreach (TrainerSpell trainerSpell in trainer.Spells)
            {
                int spellId = (int)trainerSpell.SpellId;
                Spell spell = spells.Get(spellId);
                if (spell == null || string.IsNullOrEmpty(spell.NameLang) || !IsWeaponProficiency(spell))
                {
                    continue;
                }

                if (!classRacesBySpell.TryGetValue(spellId, out Dictionary<int, int> classRaces))
                {
                    classRacesBySpell[spellId] = classRaces = BuyersOf(spellId, createSkills);
                }

                if (classRaces.Count == 0)
                {
                    continue;
                }

                collected.Add(new ExtractedSpellData()
                {
                    Id = spellId,
                    Name = spell.NameLang,
                    ClassRaces = classRaces,
                    ReqLevel = trainerSpell.ReqLevel,
                    ReqSkillRank = (int)trainerSpell.ReqSkillRank,
                    TaughtSpells = TaughtSpells(spell),
                    Factions = FACTION_ANY,
                    Expansion = trainerExpansions.GetValueOrDefault(trainer.Id, EXPANSION_CLASSIC),
                    Kind = SpellKind.Weapon,
                });
            }
        }

        return collected;
    }

    /// <summary>
    /// The races of each class that can still buy a weapon skill, keyed by class id.
    ///
    /// A class with no race left to sell to is not in the map at all, and a map with nothing in it is a
    /// skill nobody can buy.
    /// </summary>
    private Dictionary<int, int> BuyersOf(int spellId, List<PlayercreateinfoSkill> createSkills)
    {
        var abilities = AbilitiesOf(spellId);
        var skills = abilities.Select(ability => ability.SkillLine).ToHashSet();

        Dictionary<int, int> classRaces = [];
        foreach (int classId in RANDOMIZED_CLASS_IDS)
        {
            int raceMask = 0;
            foreach (int raceId in PLAYABLE_RACE_IDS)
            {
                if (IsFitByClassAndRace(abilities, raceId, classId)
                    && !skills.Any(skill => IsCreatedHolding(createSkills, skill, raceId, classId)))
                {
                    raceMask |= RaceBit(raceId);
                }
            }

            if (raceMask != 0)
            {
                classRaces[classId] = raceMask;
            }
        }

        return classRaces;
    }

    /// <summary>
    /// The races of one class the core would offer a spell to, or 0 when no race of it is barred.
    ///
    /// Zero is what the extract means by "every race" throughout, so a spell open to the whole class
    /// comes out unrestricted rather than as a mask naming all ten races.
    /// </summary>
    private int RacesFor(List<SkillLineAbility> abilities, int classId)
    {
        int raceMask = 0;
        foreach (int raceId in PLAYABLE_RACE_IDS)
        {
            if (IsFitByClassAndRace(abilities, raceId, classId))
            {
                raceMask |= RaceBit(raceId);
            }
        }

        return raceMask == PLAYABLE_RACES_MASK ? 0 : raceMask;
    }

    /// <summary>
    /// Whether the core would offer a spell to a character of this race and class.
    ///
    /// Mirrors Player::IsSpellFitByClassAndRace: one of the skill line entries of the spell has to name
    /// the race and the class, and SkillRaceClassInfo has to hold that pair for the skill line the
    /// entry sits in -- and a spell that sits in no skill line at all is closed to nobody. The second
    /// half is the one that matters for the weapon skills, whose entries mostly name nobody: only
    /// SkillRaceClassInfo says that Thrown belongs to warriors, hunters and rogues and to no one else.
    /// </summary>
    private bool IsFitByClassAndRace(List<SkillLineAbility> abilities, int raceId, int classId)
    {
        if (abilities.Count == 0)
        {
            return true;
        }

        return abilities.Any(ability =>
            (Widen(ability.RaceMask, ALL_RACES_MASK) & RaceBit(raceId)) != 0
            && (Widen(ability.ClassMask, ALL_CLASSES_MASK) & ClassBit(classId)) != 0
            && HasRaceClassInfo(ability.SkillLine, raceId, classId));
    }

    /// <summary>
    /// Whether SkillRaceClassInfo hands a skill line to this race and class, the way
    /// GetSkillRaceClassInfo in DBCStores reads it.
    /// </summary>
    private bool HasRaceClassInfo(int skillLineId, int raceId, int classId)
    {
        return raceClassInfoBySkill.GetValueOrDefault(skillLineId, []).Any(info =>
            (Widen(info.RaceMask, ALL_RACES_MASK) & RaceBit(raceId)) != 0
            && (Widen(info.ClassMask, ALL_CLASSES_MASK) & ClassBit(classId)) != 0);
    }

    /// <summary>
    /// The class abilities a character is created knowing.
    ///
    /// Mirrors what the core does at character creation: it walks the default skills of the race and
    /// class and learns the abilities those skill lines hand out. Only the class category is kept, so
    /// racials, languages and weapon skills stay out of the pool.
    /// </summary>
    private List<ExtractedSpellData> CollectStarterSpells(List<PlayercreateinfoSkill> createSkills)
    {
        Dictionary<(int ClassId, int SpellId), ExtractedSpellData> collected = [];

        foreach (PlayercreateinfoSkill createSkill in createSkills)
        {
            if (!classSkillLines.Contains(createSkill.Skill))
            {
                continue;
            }

            foreach (SkillLineAbility ability in AbilitiesOfSkill(createSkill.Skill))
            {
                if (ability.AcquireMethod is not (ACQUIRE_ON_SKILL_VALUE or ACQUIRE_ON_SKILL_LEARN))
                {
                    continue;
                }
                // A class skill line starts at rank 1, so anything asking for more is learned later
                if (ability.AcquireMethod == ACQUIRE_ON_SKILL_VALUE && ability.MinSkillLineRank > 1)
                {
                    continue;
                }

                Spell spell = spells.Get(ability.Spell);
                if (spell == null || string.IsNullOrEmpty(spell.NameLang) || IsExcluded(spell))
                {
                    continue;
                }

                int classMask = Widen(ability.ClassMask, ALL_CLASSES_MASK) & Widen((int)createSkill.ClassMask, ALL_CLASSES_MASK);
                int raceMask = Widen(ability.RaceMask, ALL_RACES_MASK) & Widen((int)createSkill.RaceMask, ALL_RACES_MASK);
                if (classMask == 0 || raceMask == 0)
                {
                    continue;
                }

                if (IsSupercededAtCreation(ability))
                {
                    continue;
                }

                foreach (int classId in RANDOMIZED_CLASS_IDS.Where(classId => (classMask & ClassBit(classId)) != 0))
                {
                    // The core only hands a create-info skill to a race and class SkillRaceClassInfo
                    // holds that skill for, so the races are narrowed per class rather than once for
                    // the row
                    int classRaceMask = 0;
                    foreach (int raceId in PLAYABLE_RACE_IDS)
                    {
                        if ((raceMask & RaceBit(raceId)) != 0 && HasRaceClassInfo(createSkill.Skill, raceId, classId))
                        {
                            classRaceMask |= RaceBit(raceId);
                        }
                    }

                    if (classRaceMask == 0)
                    {
                        continue;
                    }

                    bool unrestricted = classRaceMask == PLAYABLE_RACES_MASK;
                    var key = (classId, ability.Spell);
                    if (!collected.TryGetValue(key, out ExtractedSpellData existing))
                    {
                        collected[key] = new ExtractedSpellData()
                        {
                            Id = ability.Spell,
                            Name = spell.NameLang,
                            ClassId = classId,
                            // A starting ability costs nothing and asks for no level: the character is
                            // created holding it, and the trainer only sells it back because the
                            // module's own update put it there.
                            ReqLevel = 1,
                            TaughtSpells = TaughtSpells(spell),
                            RaceMask = unrestricted ? 0 : classRaceMask,
                            Factions = FACTION_ANY,
                            Expansion = EXPANSION_CLASSIC,
                            Kind = SpellKind.Starter,
                        };
                    }
                    else if (existing.RaceMask != 0)
                    {
                        existing.RaceMask = unrestricted ? 0 : existing.RaceMask | classRaceMask;
                    }
                }
            }
        }

        return [.. collected.Values];
    }

    /// <summary>
    /// Whether the core skips this ability at creation in favour of the one that replaces it.
    ///
    /// Mirrors the guard in Player::learnSkillRewardedSpells, which exists for the paladin's two copies
    /// of Seal of Righteousness -- the only player spell that is auto-learned both directly and through
    /// a forward spell, and so the only one that would otherwise be extracted twice under one name.
    /// </summary>
    private bool IsSupercededAtCreation(SkillLineAbility ability)
    {
        if (ability.AcquireMethod != ACQUIRE_ON_SKILL_LEARN || ability.SupercededBySpell == 0)
        {
            return false;
        }

        return AbilitiesOf(ability.SupercededBySpell)
            .Any(forward => forward.AcquireMethod == ACQUIRE_ON_SKILL_LEARN && forward.MinSkillLineRank <= 1);
    }

    /// <summary>
    /// One entry per class and spell.
    ///
    /// Keeps the lowest level any trainer asks for, the earliest expansion any of them is reachable in,
    /// and the teams of every trainer that teaches it.
    /// </summary>
    private static List<ExtractedSpellData> Deduplicate(List<ExtractedSpellData> collected)
    {
        Dictionary<(int ClassId, int Id), ExtractedSpellData> best = [];

        foreach (ExtractedSpellData spell in collected)
        {
            var key = (spell.ClassId, spell.Id);
            if (!best.TryGetValue(key, out ExtractedSpellData existing))
            {
                best[key] = spell;
                continue;
            }

            existing.ReqLevel = Math.Min(existing.ReqLevel, spell.ReqLevel);
            existing.ReqSkillRank = Math.Min(existing.ReqSkillRank, spell.ReqSkillRank);
            existing.Expansion = Math.Min(existing.Expansion, spell.Expansion);
            existing.Factions |= spell.Factions;
        }

        foreach (ExtractedSpellData spell in best.Values)
        {
            // Both teams, or neither, means the spell is not faction bound
            spell.Factions = spell.Factions == (FACTION_ALLIANCE | FACTION_HORDE) ? FACTION_ANY : spell.Factions;
        }

        return [.. best.Values
            .OrderBy(spell => spell.ClassId)
            .ThenBy(spell => spell.ReqLevel)
            .ThenBy(spell => spell.Id)];
    }

    /// <summary>
    /// Report names shared by several spells.
    ///
    /// Archipelago keys items and locations by name, so archipelawow disambiguates these with the class
    /// name. Logging them keeps that decision honest as the extract is refreshed.
    /// </summary>
    private void ReportDuplicateNames(List<ExtractedSpellData> collected)
    {
        var byName = collected
            .GroupBy(spell => spell.Name)
            .Select(group => new { Name = group.Key, Ids = group.Select(spell => spell.Id).Distinct().Order().ToList() })
            .Where(entry => entry.Ids.Count > 1)
            .OrderBy(entry => entry.Name);

        foreach (var entry in byName)
        {
            logger.LogInformation("\"{name}\" is shared by spells {ids}.", entry.Name, string.Join(", ", entry.Ids));
        }
    }

    private List<SkillLineAbility> AbilitiesOf(int spellId)
    {
        return abilitiesBySpell.GetValueOrDefault(spellId, []);
    }

    private List<SkillLineAbility> AbilitiesOfSkill(int skillLineId)
    {
        return abilitiesBySkill.GetValueOrDefault(skillLineId, []);
    }

    /// <summary>
    /// What the spell teaches when it is cast, for the handful of trainer entries that are a wrapper
    /// around one or more real spells rather than the spell itself.
    /// </summary>
    private static List<int> TaughtSpells(Spell spell)
    {
        return [.. spell.Effect
            .Zip(spell.EffectTriggerSpell)
            .Where(effect => effect.First == SPELL_EFFECT_LEARN_SPELL && effect.Second != 0)
            .Select(effect => effect.Second)];
    }

    private static bool IsWeaponProficiency(Spell spell)
    {
        return spell.Effect.Contains(SPELL_EFFECT_PROFICIENCY) && spell.EquippedItemClass == ITEM_CLASS_WEAPON;
    }

    private static bool IsExcluded(Spell spell)
    {
        if (EXCLUDED_SPELL_IDS.Contains(spell.ID))
        {
            return true;
        }
        if ((spell.Attributes & SPELL_ATTR0_DO_NOT_DISPLAY) != 0)
        {
            return true;
        }
        if (spell.Effect.Contains(SPELL_EFFECT_DUAL_WIELD))
        {
            return true;
        }
        if (spell.Effect.Contains(SPELL_EFFECT_PROFICIENCY) && !IsWeaponProficiency(spell))
        {
            return true;
        }
        return EXCLUDED_NAME_PREFIXES.Any(spell.NameLang.StartsWith);
    }

    /// <summary>
    /// A mask of zero means "all of them" throughout the DBCs and the create-info tables, and has to be
    /// widened to the full set before it is intersected with another mask.
    /// </summary>
    private static int Widen(int mask, int all)
    {
        return mask == 0 ? all : mask;
    }

    private static int ClassBit(int classId)
    {
        return 1 << (classId - 1);
    }

    private static int RaceBit(int raceId)
    {
        return 1 << (raceId - 1);
    }
}
