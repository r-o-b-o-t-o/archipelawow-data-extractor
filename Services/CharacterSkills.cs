using ArchipelaWoW.DataExtractor.Dbc;
using ArchipelaWoW.DataExtractor.Entities.World;

namespace ArchipelaWoW.DataExtractor.Services;

/// <summary>
/// The races and classes ArchipelaWoW rolls characters as, and the skill lines those may hold and are created
/// holding, read the way the core reads SkillRaceClassInfo.dbc and the world database's playercreateinfo_skills.
/// Shared by the extractors of the spells and the skills.
/// </summary>
public class CharacterSkills(SkillRaceClassInfoContainer skillRaceClassInfos)
{
    // The classes ArchipelaWoW can roll a seed for. Listed rather than counted through for two
    // reasons: Wrath has no class 10, so the ids run 1-9 and then 11 and a mask bit for that gap does
    // turn up in the DBCs; and the death knight (6) is left out on purpose, since it starts at level
    // 55 with a kit of its own and ArchipelaWoW does not offer it.
    public static readonly int[] RANDOMIZED_CLASS_IDS = [1, 2, 3, 4, 5, 7, 8, 9, 11];

    // The races a Wrath character can be. Listed for the same reason as the classes above: race 9, the
    // goblin, holds a mask bit and a ChrRaces row of its own but is not playable until Cataclysm.
    public static readonly int[] PLAYABLE_RACE_IDS = [1, 2, 3, 4, 5, 6, 7, 8, 10, 11];
    public static readonly int PLAYABLE_RACES_MASK = PLAYABLE_RACE_IDS.Aggregate(0, (mask, raceId) => mask | RaceBit(raceId));

    // A race or class mask of zero means "every one of them" throughout the DBCs and the create-info
    // tables, so it is widened to these before two masks are intersected.
    public const int ALL_RACES_MASK = 0x7FF;
    public static readonly int ALL_CLASSES_MASK = RANDOMIZED_CLASS_IDS.Aggregate(0, (mask, classId) => mask | ClassBit(classId));

    private readonly ILookup<int, SkillRaceClassInfo> raceClassInfoBySkill = skillRaceClassInfos.ToLookup(info => info.SkillID);

    /// <summary>
    /// Whether SkillRaceClassInfo hands a skill line to this race and class, the way
    /// GetSkillRaceClassInfo in DBCStores reads it.
    /// </summary>
    public bool HasRaceClassInfo(int skillLineId, int raceId, int classId)
    {
        return raceClassInfoBySkill[skillLineId].Any(info =>
            (Widen(info.RaceMask, ALL_RACES_MASK) & RaceBit(raceId)) != 0
            && (Widen(info.ClassMask, ALL_CLASSES_MASK) & ClassBit(classId)) != 0);
    }

    /// <summary>
    /// Whether a character of this race and class is created already holding a skill.
    ///
    /// Mirrors what the core hands out at creation in ObjectMgr: both masks of the create-info row have
    /// to name the character, and SkillRaceClassInfo has to hold the skill for that pair. Which weapon a
    /// character starts with is as much a matter of race as of class -- a dwarf hunter is created holding
    /// Guns and a troll one Bows.
    /// </summary>
    public bool IsCreatedHolding(List<PlayercreateinfoSkill> createSkills, int skillId, int raceId, int classId)
    {
        return HasRaceClassInfo(skillId, raceId, classId)
            && createSkills.Any(createSkill => createSkill.Skill == skillId
                && (Widen((int)createSkill.RaceMask, ALL_RACES_MASK) & RaceBit(raceId)) != 0
                && (Widen((int)createSkill.ClassMask, ALL_CLASSES_MASK) & ClassBit(classId)) != 0);
    }

    /// <summary>
    /// A mask of zero means "all of them" throughout the DBCs and the create-info tables, and has to be
    /// widened to the full set before it is intersected with another mask.
    /// </summary>
    public static int Widen(int mask, int all)
    {
        return mask == 0 ? all : mask;
    }

    public static int ClassBit(int classId)
    {
        return 1 << (classId - 1);
    }

    public static int RaceBit(int raceId)
    {
        return 1 << (raceId - 1);
    }
}
