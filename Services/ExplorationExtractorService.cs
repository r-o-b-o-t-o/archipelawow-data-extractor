using System.Text.Encodings.Web;
using System.Text.Json;
using ArchipelaWoW.DataExtractor.Dbc;
using ArchipelaWoW.DataExtractor.Extensions;
using Microsoft.Extensions.Logging;

namespace ArchipelaWoW.DataExtractor.Services;

public class ExtractedExplorationData
{
    /// <summary>
    /// The Achievement_Criteria.dbc id, which the core credits the first time the subzone is explored and what
    /// the server module checks the location on.
    /// </summary>
    public int Id { get; set; }
    public string Name { get; set; }

    /// <summary>
    /// The zone the explored area lies in. That is the achievement's zone but for the capitals, which a zone's
    /// achievement asks for though they are zones of their own: Orgrimmar for Durotar's, Shattrath City for
    /// Terokkar Forest's.
    /// </summary>
    public ExtractedArea Zone { get; set; }
}

/// <summary>
/// Builds the subzone table archipelawow ships: the areas the exploration achievements of the client's
/// Achievement.dbc ask to explore.
/// </summary>
public class ExplorationExtractorService(
        ILogger<ExplorationExtractorService> logger,
        AchievementContainer achievements,
        AchievementCategoryContainer achievementCategories,
        AchievementCriteriaContainer achievementCriteria,
        WorldMapOverlayContainer overlays,
        AreaTableContainer areas
    )
{
    private readonly JsonSerializerOptions jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    // Achievement_Category.dbc: Exploration, the parent of one category per continent
    private const int ACHIEVEMENT_CATEGORY_EXPLORATION = 97;

    // Achievement_Criteria.Type, see DBCEnums.h. The category's other criteria are the continents' and World
    // Explorer's, which ask for other achievements, and a couple that ask for kills.
    private const int ACHIEVEMENT_CRITERIA_TYPE_EXPLORE_AREA = 43;

    // Names Achievement_Criteria.dbc misspells, by criterion id, as AreaTable.dbc spells them
    private static readonly Dictionary<int, string> NAME_FIXES = new()
    {
        [910] = "Bael'dun Digsite",
        [4135] = "The Dens of Dying",
        [4147] = "Giants' Run",
        [4170] = "Angrathar the Wrathgate",
        [4194] = "Amphitheater of Anguish",
        [4209] = "The Makers' Overlook",
        [4210] = "The Makers' Perch",
    };

    public async Task ExtractExplorations()
    {
        string outDir = OutputDirectory.Prepare();

        var explorationAchievements = achievements
            .Where(a => achievementCategories.WithParents(a.Category).Contains(ACHIEVEMENT_CATEGORY_EXPLORATION))
            .Select(a => a.ID)
            .ToHashSet();

        // By achievement then as the achievement lists them, rather than by name, which depends on DBC_LOCALE
        List<ExtractedExplorationData> subzones = [.. achievementCriteria
            .Where(c => c.Type == ACHIEVEMENT_CRITERIA_TYPE_EXPLORE_AREA && explorationAchievements.Contains(c.AchievementID))
            .OrderBy(c => c.AchievementID)
            .ThenBy(c => c.UiOrder)
            .Select(c => new ExtractedExplorationData()
            {
                Id = c.ID,
                Name = NAME_FIXES.GetValueOrDefault(c.ID, c.DescriptionLang),
                Zone = ExtractedArea.FromAreaTable(areas.WithParents(overlays.Get(c.AssetID).AreaID[0]).Last()),
            })];

        string outFile = Path.Combine(outDir, "explorations.json");
        await File.WriteAllTextAsync(outFile, JsonSerializer.Serialize(subzones, jsonOptions));

        logger.LogInformation("Wrote {count} subzones to {file}.", subzones.Count, outFile);
    }
}
