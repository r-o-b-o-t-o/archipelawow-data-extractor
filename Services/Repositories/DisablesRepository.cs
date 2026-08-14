using ArchipelaWoW.QuestExtractor.Entities;
using ArchipelaWoW.QuestExtractor.Entities.World;
using Microsoft.EntityFrameworkCore;

namespace ArchipelaWoW.QuestExtractor.Services.Repositories;

public class DisablesRepository(WorldDbContext db)
{
    public enum SourceType : uint
    {
        DISABLE_TYPE_SPELL = 0,
        DISABLE_TYPE_QUEST = 1,
        DISABLE_TYPE_MAP = 2,
        DISABLE_TYPE_BATTLEGROUND = 3,
        DISABLE_TYPE_ACHIEVEMENT_CRITERIA = 4,
        DISABLE_TYPE_OUTDOORPVP = 5,
        DISABLE_TYPE_VMAP = 6,
        DISABLE_TYPE_MMAP = 7,
        DISABLE_TYPE_LFG_MAP = 8,
        DISABLE_TYPE_GAME_EVENT = 9,
        DISABLE_TYPE_LOOT = 10,
    }

    public async Task<List<Disable>> GetDisables(SourceType sourceType)
    {
        return await db.Disables
            .Where((disable) => disable.SourceType == (uint)sourceType)
            .ToListAsync();
    }

    public async Task<List<Disable>> GetQuestDisables()
    {
        return await GetDisables(SourceType.DISABLE_TYPE_QUEST);
    }
}
