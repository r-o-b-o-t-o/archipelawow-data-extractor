using ArchipelaWoW.DataExtractor.Dbc;
using ArchipelaWoW.DataExtractor.Extensions;

namespace ArchipelaWoW.DataExtractor.Services;

/// <summary>
/// A spot on a world map: the WorldMapArea it is drawn on, and where on that map, from 0 to 1 left to right
/// and top to bottom, as the game's own map coordinates read divided by 100.
/// </summary>
public readonly record struct MapPosition(int Area, float X, float Y)
{
    public object[] ToJson() => [Area, MathF.Round(X, 4), MathF.Round(Y, 4)];
}

/// <summary>Where the world maps sit and what they cover, from WorldMapArea.dbc and WorldMapTransforms.dbc.</summary>
public class WorldMapGeometry
{
    // The maps a continent's world map draws: Azeroth's, Kalimdor's, Outland's and Northrend's
    public static readonly int[] CONTINENT_MAPS = [0, 1, 530, 571];

    private readonly AreaTableContainer areaTable;
    private readonly List<WorldMapTransforms> transforms;

    /// <summary>The maps of a zone or city, keyed by its AreaTable id.</summary>
    private readonly Dictionary<int, WorldMapArea> mapsByArea;

    public WorldMapGeometry(WorldMapAreaContainer worldMapAreas, WorldMapTransformsContainer worldMapTransforms, AreaTableContainer areaTable)
    {
        this.areaTable = areaTable;
        transforms = [.. worldMapTransforms.Where(t => CONTINENT_MAPS.Contains(t.NewMapID) && t.MapID != t.NewMapID)];
        Continents = [.. worldMapAreas.Where(area => area.AreaID == 0 && CONTINENT_MAPS.Contains(area.MapID))];
        Zones = [.. worldMapAreas.Where(area => area.AreaID != 0 && CONTINENT_MAPS.Contains(area.MapID) && area.LocLeft != area.LocRight)];
        mapsByArea = Zones.ToDictionary(area => area.AreaID);
    }

    /// <summary>The world maps of the continents, one per map.</summary>
    public List<WorldMapArea> Continents { get; }

    /// <summary>The world maps of the zones and cities of the continents, Dalaran's aside, which only has dungeon floors.</summary>
    public List<WorldMapArea> Zones { get; }

    /// <summary>
    /// The continent a map is drawn on. The blood elf and draenei lands are part of Outland's map, and drawn
    /// on the old continents through WorldMapTransforms.
    /// </summary>
    public int DisplayMap(WorldMapArea area) => area.DisplayMapID >= 0 ? area.DisplayMapID : area.MapID;

    /// <summary>A map's bounds, moved onto the continent it is drawn on: left, right, top, bottom.</summary>
    public float[] DisplayBounds(WorldMapArea area)
    {
        var (x, y) = Offset(area.MapID, (area.LocTop + area.LocBottom) / 2, (area.LocLeft + area.LocRight) / 2);
        return [area.LocLeft + y, area.LocRight + y, area.LocTop + x, area.LocBottom + x];
    }

    /// <summary>How much a point of <paramref name="map"/> moves to be drawn on its continent.</summary>
    private (float X, float Y) Offset(int map, float x, float y)
    {
        var transform = transforms.FirstOrDefault(t => t.MapID == map
            && x >= t.RegionMin[0] && x <= t.RegionMax[0] && y >= t.RegionMin[1] && y <= t.RegionMax[1]);
        return transform == null ? (0, 0) : (transform.RegionOffset[0], transform.RegionOffset[1]);
    }

    /// <summary>
    /// Where a spawn sits on the map of its zone, or of the closest zone above it with a map of its own; null
    /// when there is none, inside an instance for one.
    /// </summary>
    public MapPosition? Locate(int map, int areaId, float x, float y)
    {
        foreach (var area in areaTable.WithParents(areaId))
        {
            if (mapsByArea.TryGetValue(area.ID, out var worldMap) && worldMap.MapID == map)
            {
                return Project(worldMap, x, y);
            }
        }
        return null;
    }

    /// <summary>The map of an area, or of the closest area above it with one; null when there is none.</summary>
    public WorldMapArea MapOf(int areaId)
    {
        foreach (var area in areaTable.WithParents(areaId))
        {
            if (mapsByArea.TryGetValue(area.ID, out var worldMap))
            {
                return worldMap;
            }
        }
        return null;
    }

    /// <summary>Where a point sits on the map of a zone or city, given by its WorldMapArea id.</summary>
    public MapPosition Project(int worldMapAreaId, float x, float y) => Project(Zones.First(zone => zone.ID == worldMapAreaId), x, y);

    public static MapPosition Project(WorldMapArea area, float x, float y) =>
        new(area.ID, (area.LocLeft - y) / (area.LocLeft - area.LocRight), (area.LocTop - x) / (area.LocTop - area.LocBottom));
}
