using ArchipelaWoW.DataExtractor.Entities;
using ArchipelaWoW.DataExtractor.Entities.World;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.DataExtractor.Services.Repositories;

public class ConditionsRepository(WorldDbContext db)
{
    public enum SourceType : int
    {
        CONDITION_SOURCE_TYPE_QUEST_AVAILABLE = 19,
    }

    public async Task<List<Condition>> GetConditions(SourceType sourceType)
    {
        return await db.Conditions
            .Where((condition) => condition.SourceTypeOrReferenceId == (int)sourceType)
            .ToListAsync();
    }

    public async Task<List<Condition>> GetQuestAvailableConditions()
    {
        return await GetConditions(SourceType.CONDITION_SOURCE_TYPE_QUEST_AVAILABLE);
    }
}
