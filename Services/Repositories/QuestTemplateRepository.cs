using ArchipelaWoW.QuestExtractor.Entities;
using ArchipelaWoW.QuestExtractor.Entities.World;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.QuestExtractor.Services.Repositories;

public class QuestTemplateRepository(WorldDbContext db)
{
    public async Task<List<QuestTemplate>> GetAllQuests()
    {
        return await db.QuestTemplates
            // Nine collection navigations in one query would multiply out into a cartesian product of
            // every starter against every ender against every game event, for each of ~20k quests.
            .AsSplitQuery()
            .AsNoTracking()
            .Include(quest => quest.QuestTemplateAddons)
            .Include(quest => quest.Pool)
            .Include(quest => quest.CreatureStarters)
            .Include(quest => quest.CreatureEnders)
            .Include(quest => quest.GameObjectStarters)
            .Include(quest => quest.GameObjectEnders)
            .Include(quest => quest.ItemTemplateStarters)
            .Include(quest => quest.GameEventCreatureQuests)
            .Include(quest => quest.GameEventGameObjectQuests)
            .ToListAsync();
    }
}
