using ArchipelaWoW.DataExtractor.Dbc;

namespace ArchipelaWoW.DataExtractor.Extensions;

public static class DbcContainerExtensions
{
    /// <summary>An area and the areas above it, the last of them its zone.</summary>
    public static IEnumerable<AreaTable> WithParents(this AreaTableContainer areas, int areaId)
    {
        for (var area = areas.Get(areaId); area != null; area = area.ParentAreaID == 0 ? null : areas.Get(area.ParentAreaID))
        {
            yield return area;
        }
    }

    /// <summary>The id of a category and of the categories above it.</summary>
    public static IEnumerable<int> WithParents(this AchievementCategoryContainer categories, int categoryId)
    {
        for (int id = categoryId; id > 0; id = categories.Get(id)?.Parent ?? 0)
        {
            yield return id;
        }
    }
}
