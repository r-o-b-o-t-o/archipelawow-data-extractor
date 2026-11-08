using ArchipelaWoW.DataExtractor.Dbc;
using ArchipelaWoW.DataExtractor.Services.Repositories;
using static ArchipelaWoW.DataExtractor.Services.CharacterSkills;

namespace ArchipelaWoW.DataExtractor.Services;

/// <summary>
/// Who the trainer creatures will teach. Shared by the spells' trainer zones and the tracker's trainer spots,
/// which have to agree.
/// </summary>
public class TrainerRaces(TrainerRepository trainersRepo, FactionTemplateContainer factionTemplates, ChrRacesContainer races)
{
    /// <summary>
    /// Every spawn of a trainer creature, with the mask of the playable races it teaches. A spawn that teaches
    /// nobody is left out.
    /// </summary>
    public async Task<List<(TrainerSpawn Spawn, int Races)>> GetTeachingSpawns()
    {
        var spawns = await trainersRepo.GetTrainerSpawns();
        var optionRaceMasks = await trainersRepo.GetTrainerOptionRaceMasks();

        return [.. spawns
            .Select(spawn => (Spawn: spawn, Races: RacesTrainedBy(spawn, optionRaceMasks)))
            .Where(entry => entry.Races != 0)];
    }

    /// <summary>
    /// The playable races a trainer creature will teach: the ones its trainer option is shown to, less the
    /// ones its faction is hostile to.
    /// </summary>
    private int RacesTrainedBy(TrainerSpawn spawn, Dictionary<uint, int> optionRaceMasks)
    {
        int optionRaces = optionRaceMasks.GetValueOrDefault(spawn.GossipMenuId, ALL_RACES_MASK);
        FactionTemplate trainerFaction = factionTemplates.Get(spawn.Faction);

        int raceMask = 0;
        foreach (int raceId in PLAYABLE_RACE_IDS.Where(raceId => (optionRaces & RaceBit(raceId)) != 0))
        {
            FactionTemplate raceFaction = factionTemplates.Get(races.Get(raceId)?.FactionID ?? 0);
            if (trainerFaction == null || raceFaction == null || FactionRelations.WillTalkTo(trainerFaction, raceFaction))
            {
                raceMask |= RaceBit(raceId);
            }
        }
        return raceMask;
    }
}
