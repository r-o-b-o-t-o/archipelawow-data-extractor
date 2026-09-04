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

public class TrainerRepository(WorldDbContext db)
{
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

    public async Task<List<TrainerCreature>> GetTrainerCreatures()
    {
        return await db.CreatureDefaultTrainers
            .AsNoTracking()
            .Join(db.CreatureTemplates, trainer => trainer.CreatureId, creature => creature.Entry,
                  (trainer, creature) => new TrainerCreature { TrainerId = trainer.TrainerId, Faction = creature.Faction })
            .ToListAsync();
    }
}
