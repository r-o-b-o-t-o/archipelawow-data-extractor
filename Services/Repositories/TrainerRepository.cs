using ArchipelaWoW.DataExtractor.Entities;
using ArchipelaWoW.DataExtractor.Entities.World;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.DataExtractor.Services.Repositories;

/// <summary>
/// One creature template acting as a given trainer. A trainer list is shared by every creature that
/// offers it, and which creatures those are decides both the faction the list can be reached from and
/// the continent it stands on.
/// </summary>
public class TrainerCreature
{
    public uint TrainerId { get; set; }
    public ushort Faction { get; set; }
}

/// <summary>
/// One zone a creature acting as a given trainer is spawned in, with what decides who it will train: its
/// faction, and the gossip menu whose conditions can hide the trainer option from some races.
/// </summary>
public class TrainerSpawn
{
    public uint TrainerId { get; set; }
    public int ZoneId { get; set; }
    public ushort Faction { get; set; }
    public uint GossipMenuId { get; set; }
}

public class TrainerRepository(WorldDbContext db)
{
    // conditions.ConditionTypeOrReference, see ConditionMgr.h
    private const int CONDITION_RACE = 16;

    // gossip_menu_option.OptionType, see GossipDef.h
    private const int GOSSIP_OPTION_TRAINER = 5;

    public async Task<List<Trainer>> GetTrainersWithSpells()
    {
        return await db.Trainers
            .AsNoTracking()
            .Include(trainer => trainer.Spells)
            .ToListAsync();
    }

    /// <summary>
    /// The spells <c>spell_ranks</c> records as anything but the first rank of their chain.
    /// </summary>
    public async Task<HashSet<uint>> GetHigherRankSpellIds()
    {
        return [.. await db.SpellRanks
            .AsNoTracking()
            .Where(spellRank => spellRank.Rank != 1)
            .Select(spellRank => spellRank.SpellId)
            .ToListAsync()];
    }

    public async Task<List<PlayercreateinfoSkill>> GetCreateSkills()
    {
        return await db.PlayercreateinfoSkills
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Trainer id to the lowest map id any creature offering that trainer is spawned on. The map is
    /// what says which expansion a trainer belongs to, and the lowest one wins because a trainer that
    /// stands in the old world too is reachable without ever leaving it.
    /// </summary>
    public async Task<Dictionary<uint, ushort>> GetTrainerLowestSpawnMaps()
    {
        return await db.CreatureDefaultTrainers
            .AsNoTracking()
            .Join(db.Creatures, trainer => trainer.CreatureId, creature => creature.Id,
                  (trainer, creature) => new { trainer.TrainerId, creature.Map })
            .GroupBy(spawn => spawn.TrainerId)
            .Select(group => new { TrainerId = group.Key, Map = group.Min(spawn => spawn.Map) })
            .ToDictionaryAsync(entry => entry.TrainerId, entry => entry.Map);
    }

    /// <summary>
    /// Every zone a creature offering a trainer is spawned in. Reads the denormalised <c>zoneId</c> spawn
    /// column, which stays at 0 until worldserver has populated it (see the README).
    /// </summary>
    public async Task<List<TrainerSpawn>> GetTrainerSpawns()
    {
        var spawns = await db.CreatureDefaultTrainers
            .AsNoTracking()
            .Join(db.CreatureTemplates, trainer => trainer.CreatureId, template => template.Entry,
                  (trainer, template) => new { trainer.TrainerId, trainer.CreatureId, template.Faction, template.GossipMenuId })
            .Join(db.Creatures, trainer => trainer.CreatureId, creature => creature.Id,
                  (trainer, creature) => new { trainer.TrainerId, trainer.Faction, trainer.GossipMenuId, creature.ZoneId })
            .Where(spawn => spawn.ZoneId != 0)
            .Distinct()
            .ToListAsync();

        return [.. spawns.Select(spawn => new TrainerSpawn
        {
            TrainerId = spawn.TrainerId,
            ZoneId = spawn.ZoneId,
            Faction = spawn.Faction,
            GossipMenuId = spawn.GossipMenuId,
        })];
    }

    /// <summary>
    /// Gossip menu id to the races its trainer option is shown to, for the menus that hide it from some.
    ///
    /// This is how a racial riding trainer turns the other races away: the trainer list itself names no
    /// race. Only the race conditions are read, which leaves out the alternative group that lets in a
    /// player exalted with the trainer's faction -- no seed can count on that reputation.
    /// </summary>
    public async Task<Dictionary<uint, int>> GetTrainerOptionRaceMasks()
    {
        var conditions = await db.GossipMenuOptions
            .AsNoTracking()
            .Where(option => option.OptionType == GOSSIP_OPTION_TRAINER)
            .Join(db.Conditions,
                  option => new { Group = option.MenuId, Entry = (int)option.OptionId },
                  condition => new { Group = condition.SourceGroup, Entry = condition.SourceEntry },
                  (option, condition) => condition)
            .Where(condition => condition.SourceTypeOrReferenceId == (int)ConditionsRepository.SourceType.CONDITION_SOURCE_TYPE_GOSSIP_MENU_OPTION
                && condition.ConditionTypeOrReference == CONDITION_RACE
                && condition.NegativeCondition == 0)
            .Select(condition => new { condition.SourceGroup, condition.ConditionValue1 })
            .ToListAsync();

        return conditions
            .GroupBy(condition => condition.SourceGroup)
            .ToDictionary(group => group.Key, group => group.Aggregate(0, (mask, condition) => mask | (int)condition.ConditionValue1));
    }

    public async Task<List<TrainerCreature>> GetTrainerCreatures()
    {
        return await db.CreatureDefaultTrainers
            .AsNoTracking()
            .Join(db.CreatureTemplates, trainer => trainer.CreatureId, creature => creature.Entry,
                  (trainer, creature) => new TrainerCreature { TrainerId = trainer.TrainerId, Faction = creature.Faction })
            .ToListAsync();
    }
}
