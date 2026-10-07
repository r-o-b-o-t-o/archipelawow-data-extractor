using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using ArchipelaWoW.DataExtractor.Dbc;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace ArchipelaWoW.DataExtractor.Services;

public class TrackerMap
{
    /// <summary>The WorldMapArea id; 0 for Azeroth's world map and -1 for the cosmic map, as the game numbers them.</summary>
    public int Id { get; set; }
    public string Name { get; set; }

    /// <summary>cosmic, world, continent, zone or city.</summary>
    public string Kind { get; set; }
    public int? Parent { get; set; }
    public string Image { get; set; }

    /// <summary>The map id of the continent the map is drawn on.</summary>
    public int? Continent { get; set; }

    /// <summary>What the map covers on its continent, in world coordinates: left, right, top, bottom.</summary>
    public float[] Bounds { get; set; }

    /// <summary>Where on the map its checks gather when they have no spot of their own, from 0 to 1.</summary>
    public float[] Center { get; set; }

    /// <summary>What lights up on the parent map when the pointer is over this one.</summary>
    public TrackerHighlight Highlight { get; set; }

    /// <summary>
    /// Where the pointer opens this map on its parent: x, y, width and height from 0 to 1. Without it, a zone
    /// opens from its continent's <see cref="Zones"/>, and a city from the part of its highlight that lights up.
    /// </summary>
    public float[] Hit { get; set; }

    /// <summary>A continent's zones, as the game tells which one is under the pointer.</summary>
    public TrackerZoneGrid Zones { get; set; }
}

public class TrackerZoneGrid
{
    /// <summary>Where the grid lies on the continent's map: x, y, width and height from 0 to 1.</summary>
    public float[] Rect { get; set; }
    public int Columns { get; set; }

    /// <summary>The id of each cell's zone, row by row; 0 where there is none.</summary>
    public int[] Cells { get; set; }
}

public class TrackerHighlight
{
    public string Image { get; set; }

    /// <summary>
    /// How the image is drawn, as the game's alphaMode: <c>add</c> adds its colour onto the map, as an opaque
    /// image whose black adds nothing; <c>blend</c> draws it over the map through its alpha.
    /// </summary>
    public string Blend { get; set; }

    /// <summary>Where the image is drawn on the parent map: x, y, width and height from 0 to 1.</summary>
    public float[] Rect { get; set; }

    /// <summary>The middle of the shape, from 0 to 1 across the image.</summary>
    [JsonIgnore] public float[] Centroid { get; set; }
}

/// <summary>
/// Builds the map hierarchy the launcher's tracker browses, from the cosmic map down to the cities, and
/// writes the map images, put together from their tiles, and the highlights shown when hovering a map.
/// </summary>
public class TrackerMapExtractorService(
        ILogger<TrackerMapExtractorService> logger,
        ClientTextures client,
        WorldMapGeometry geometry,
        WorldMapOverlayContainer overlays,
        WorldMapContinentContainer worldMapContinents,
        AreaTableContainer areaTable,
        MapContainer maps
    )
{
    // The size of every world map on screen, out of the 1024x768 its twelve 256px tiles cover
    private const int MAP_WIDTH = 1002;
    private const int MAP_HEIGHT = 668;
    private const int MAP_QUALITY = 85;

    private const int COSMIC_MAP = -1;
    private const int WORLD_MAP = 0;

    // How the game places a continent on Azeroth's world map from WorldMapContinent.dbc, which it does in
    // code: map pixels per unit of the continent's offset, from the middle of the map. Measured against
    // in-game screenshots, to a fraction of a pixel.
    private const float WORLD_MAP_SCALE = 16;

    // An ADT tile, the unit of WorldMapContinent's boundaries: its size in world coordinates, and the middle of
    // the 64x64 grid of them
    private const float ADT_SIZE = 1600f / 3;
    private const int GRID_CENTER = 32;

    // A continent's .zmp, which the game reads the zone under the pointer from: the AreaTable id of each half
    // ADT tile, 128x128 over the whole grid, as the continent's map draws them
    private const int ZONE_GRID = 128;

    // The buttons of the cosmic map in FrameXML's WorldMapFrame.xml: Azeroth's world map, and Outland's
    private const string WORLD_BUTTON = "AzerothButton";
    private static readonly Dictionary<int, string> CONTINENT_BUTTONS = new() { [530] = "OutlandButton" };

    // How the game blends the textures: WorldMapHighlight, which shows the zones and continents, adds them,
    // while the cosmic buttons' HighlightTextures are drawn over the map
    private const string ADD = "add";
    private const string BLEND = "blend";

    // The game has no highlight for a city, so the tracker lights up its explored area on the zone's map
    // instead, by a guess at about as much as a zone's highlight
    private const float CITY_HIGHLIGHT = 0.3f;

    public List<TrackerMap> ExtractMaps(string outDir)
    {
        var continentsByMap = geometry.Continents.ToDictionary(area => area.MapID);
        var cosmicButtons = ReadCosmicButtons();

        List<TrackerMap> result =
        [
            new()
            {
                Id = COSMIC_MAP,
                Name = "Cosmic",
                Kind = "cosmic",
                Image = WriteMap(outDir, "Cosmic", "Cosmic", []),
            },
            new()
            {
                Id = WORLD_MAP,
                Name = "Azeroth",
                Kind = "world",
                Parent = COSMIC_MAP,
                Image = WriteMap(outDir, "World", "World", []),
                Highlight = WriteHighlight(outDir, "world", cosmicButtons[WORLD_BUTTON].Texture, BLEND, cosmicButtons[WORLD_BUTTON].Rect, null),
                Hit = cosmicButtons[WORLD_BUTTON].Hit,
            },
        ];

        foreach (var continent in geometry.Continents.OrderBy(area => Array.IndexOf(WorldMapGeometry.CONTINENT_MAPS, area.MapID)))
        {
            var map = new TrackerMap()
            {
                Id = continent.ID,
                Name = maps.Get(continent.MapID).MapNameLang,
                Kind = "continent",
                Image = WriteMap(outDir, continent.AreaName, continent.AreaName, []),
                Continent = continent.MapID,
                Bounds = geometry.DisplayBounds(continent),
            };

            if (CONTINENT_BUTTONS.TryGetValue(continent.MapID, out string buttonName))
            {
                var button = cosmicButtons[buttonName];
                map.Parent = COSMIC_MAP;
                map.Highlight = WriteHighlight(outDir, continent.AreaName, button.Texture, BLEND, button.Rect, null);
                map.Hit = button.Hit;
            }
            else
            {
                var placement = worldMapContinents.First(c => c.MapID == continent.MapID);
                var (highlight, hit) = WorldMapPlacement(placement);
                map.Parent = WORLD_MAP;
                map.Highlight = WriteHighlight(outDir, continent.AreaName, $"Interface/WorldMap/{continent.AreaName}/{continent.AreaName}Highlight", ADD, highlight, null);
                map.Hit = hit;
            }
            result.Add(map);
        }

        // A city's map opens from its zone, through the zone's explored area that covers it
        var cityOverlays = overlays
            .Where(overlay => overlay.TextureName.Length > 0)
            .SelectMany(overlay => overlay.AreaID.Where(id => id != 0).Select(id => (Area: id, Overlay: overlay)))
            .GroupBy(pair => pair.Area)
            .ToDictionary(g => g.Key, g => g.Select(pair => pair.Overlay).ToList());
        var zoneIds = geometry.Zones.Select(zone => zone.ID).ToHashSet();

        foreach (var zone in geometry.Zones.OrderBy(zone => zone.ID))
        {
            var bounds = geometry.DisplayBounds(zone);
            var continent = continentsByMap[geometry.DisplayMap(zone)];
            var cityOverlay = cityOverlays.GetValueOrDefault(zone.AreaID)?.FirstOrDefault(overlay => overlay.MapAreaID != zone.ID && zoneIds.Contains(overlay.MapAreaID));

            var map = new TrackerMap()
            {
                Id = zone.ID,
                Name = areaTable.Get(zone.AreaID)?.AreaNameLang ?? zone.AreaName,
                Kind = cityOverlay == null ? "zone" : "city",
                Parent = cityOverlay?.MapAreaID ?? continent.ID,
                Image = WriteMap(outDir, zone.AreaName, zone.AreaName, [.. overlays.Where(overlay => overlay.MapAreaID == zone.ID)]),
                Continent = geometry.DisplayMap(zone),
                Bounds = bounds,
            };

            if (cityOverlay != null)
            {
                var parent = geometry.Zones.First(z => z.ID == cityOverlay.MapAreaID);
                var rect = new SKRectI(cityOverlay.OffsetX, cityOverlay.OffsetY, cityOverlay.OffsetX + cityOverlay.TextureWidth, cityOverlay.OffsetY + cityOverlay.TextureHeight);
                using var area = ComposeOverlay(parent.AreaName, cityOverlay);
                map.Highlight = WriteImage(outDir, zone.AreaName, area, ADD, CITY_HIGHLIGHT,
                    [(float)rect.Left / MAP_WIDTH, (float)rect.Top / MAP_HEIGHT, (float)rect.Width / MAP_WIDTH, (float)rect.Height / MAP_HEIGHT]);
            }
            else
            {
                var frame = geometry.DisplayBounds(continent);
                float[] rect = [
                    (frame[0] - bounds[0]) / (frame[0] - frame[1]),
                    (frame[2] - bounds[2]) / (frame[2] - frame[3]),
                    (bounds[0] - bounds[1]) / (frame[0] - frame[1]),
                    (bounds[2] - bounds[3]) / (frame[2] - frame[3]),
                ];
                // A zone's highlight is its own map scaled into a square texture, and only the part holding
                // the 1002x668 that is drawn is stretched over the zone
                map.Highlight = WriteHighlight(outDir, zone.AreaName, $"Interface/WorldMap/{zone.AreaName}/{zone.AreaName}Highlight", ADD, rect,
                    size => new SKRectI(0, 0, size.Width * MAP_WIDTH / 1024, size.Height * MAP_HEIGHT / 1024));
                if (map.Highlight == null)
                {
                    logger.LogWarning("{zone} has no highlight, it can't be opened from {continent}.", map.Name, continent.AreaName);
                }
            }

            // A zone's highlight covers the zone's own map, which a city's doesn't
            map.Center = map.Kind == "zone" && map.Highlight != null ? map.Highlight.Centroid : [0.5f, 0.5f];
            result.Add(map);
        }

        var mapsById = result.ToDictionary(map => map.Id);
        foreach (var continent in geometry.Continents)
        {
            mapsById[continent.ID].Zones = ReadZoneGrid(outDir, continent, mapsById);
        }

        File.WriteAllText(Path.Combine(outDir, "maps.json"), JsonSerializer.Serialize(result, TrackerExtractorService.JsonOptions));
        logger.LogInformation("Wrote {count} maps.", result.Count);
        return result;
    }

    /// <summary>
    /// A continent's place on Azeroth's world map: where its highlight is drawn, a square anchored at the
    /// top left of its tiles, and the rectangle its tiles cover, which opens it. The boundaries are the
    /// first and last tiles, both included.
    /// </summary>
    private static (float[] Highlight, float[] Hit) WorldMapPlacement(WorldMapContinent continent)
    {
        float X(int tile) => MAP_WIDTH / 2f + WORLD_MAP_SCALE * (continent.Scale * (tile - GRID_CENTER) + continent.ContinentOffset[0]);
        float Y(int tile) => MAP_HEIGHT / 2f + WORLD_MAP_SCALE * (continent.Scale * (tile - GRID_CENTER) + continent.ContinentOffset[1]);

        float left = X(continent.LeftBoundary), right = X(continent.RightBoundary + 1);
        float top = Y(continent.TopBoundary), bottom = Y(continent.BottomBoundary + 1);
        float side = Math.Max(right - left, bottom - top);
        return (
            [left / MAP_WIDTH, top / MAP_HEIGHT, side / MAP_WIDTH, side / MAP_HEIGHT],
            [left / MAP_WIDTH, top / MAP_HEIGHT, (right - left) / MAP_WIDTH, (bottom - top) / MAP_HEIGHT]
        );
    }

    /// <summary>
    /// The zones of a continent's .zmp, a city counting as its zone, cropped to the cells holding one; null when
    /// the client has no .zmp for it.
    /// </summary>
    private TrackerZoneGrid ReadZoneGrid(string outDir, WorldMapArea continent, Dictionary<int, TrackerMap> mapsById)
    {
        byte[] data = client.ReadBytes($"Interface/WorldMap/{continent.AreaName}.zmp");
        if (data == null)
        {
            logger.LogWarning("{continent} has no .zmp, its zones can't be opened from its map.", continent.AreaName);
            return null;
        }

        var cells = new int[ZONE_GRID, ZONE_GRID];
        for (int row = 0; row < ZONE_GRID; row++)
        {
            for (int column = 0; column < ZONE_GRID; column++)
            {
                var area = geometry.MapOf(BitConverter.ToInt32(data, (row * ZONE_GRID + column) * 4));
                var map = area == null ? null : mapsById.GetValueOrDefault(area.ID);
                if (map?.Kind == "city")
                {
                    map = mapsById[map.Parent.Value];
                }
                if (map?.Parent == continent.ID)
                {
                    cells[row, column] = map.Id;
                }
            }
        }

        // The cells' edges on the map: the grid starts at the top left, where world coordinates are the greatest
        var bounds = geometry.DisplayBounds(continent);
        float U(float column) => (bounds[0] - (GRID_CENTER - column / 2) * ADT_SIZE) / (bounds[0] - bounds[1]);
        float V(float row) => (bounds[2] - (GRID_CENTER - row / 2) * ADT_SIZE) / (bounds[2] - bounds[3]);

        // The .zmp predates Hrothgar's Landing: a zone it misses takes the empty cells its highlight lights up
        var missing = mapsById.Values.Where(map => map.Parent == continent.ID && map.Kind == "zone" && map.Highlight != null)
            .Where(map => !cells.Cast<int>().Contains(map.Id)).ToList();
        static int Strength(SKColor color) => Math.Max(color.Red, Math.Max(color.Green, color.Blue));
        foreach (var zone in missing)
        {
            using var image = SKBitmap.Decode(Path.Combine(outDir, zone.Highlight.Image));
            int max = image.Pixels.Max(Strength);
            var highlight = zone.Highlight.Rect;
            for (int row = 0; row < ZONE_GRID; row++)
            {
                for (int column = 0; column < ZONE_GRID; column++)
                {
                    float u = (U(column + 0.5f) - highlight[0]) / highlight[2], v = (V(row + 0.5f) - highlight[1]) / highlight[3];
                    if (cells[row, column] == 0 && u >= 0 && u < 1 && v >= 0 && v < 1
                        && Strength(image.GetPixel((int)(u * image.Width), (int)(v * image.Height))) * 10 > max)
                    {
                        cells[row, column] = zone.Id;
                    }
                }
            }
            logger.LogInformation("{zone} is missing from {continent}.zmp, it opens from where its highlight lights up instead.", zone.Name, continent.AreaName);
        }

        var used = Enumerable.Range(0, ZONE_GRID * ZONE_GRID).Where(i => cells[i / ZONE_GRID, i % ZONE_GRID] != 0).ToList();
        int top = used.Min(i => i / ZONE_GRID), bottom = used.Max(i => i / ZONE_GRID);
        int left = used.Min(i => i % ZONE_GRID), right = used.Max(i => i % ZONE_GRID);
        float[] rect = [U(left), V(top), U(right + 1) - U(left), V(bottom + 1) - V(top)];
        return new TrackerZoneGrid()
        {
            Rect = [.. rect.Select(v => MathF.Round(v, 5))],
            Columns = right - left + 1,
            Cells = [.. Enumerable.Range(top, bottom - top + 1).SelectMany(row => Enumerable.Range(left, right - left + 1).Select(column => cells[row, column]))],
        };
    }

    private sealed record CosmicButton(string Texture, float[] Rect, float[] Hit);

    /// <summary>
    /// The cosmic map's two buttons, as WorldMapFrame.xml lays them out: a button anchored by its top left
    /// corner, and its highlight by its center on the button's.
    /// </summary>
    private Dictionary<string, CosmicButton> ReadCosmicButtons()
    {
        string xml = client.ReadText("Interface/FrameXML/WorldMapFrame.xml")
            ?? throw new FileNotFoundException("Interface/FrameXML/WorldMapFrame.xml is missing from the client directory.");
        var document = XDocument.Parse(xml);

        var result = new Dictionary<string, CosmicButton>();
        foreach (string name in CONTINENT_BUTTONS.Values.Append(WORLD_BUTTON))
        {
            var button = document.Descendants().First(e => e.Name.LocalName == "Button" && (string)e.Attribute("name") == name);
            var (width, height) = Size(button);
            var (left, top) = Offset(button);
            top = -top;

            var highlight = button.Elements().First(e => e.Name.LocalName == "HighlightTexture");
            var (highlightWidth, highlightHeight) = Size(highlight);
            var (dx, dy) = Offset(highlight);
            float centerX = left + width / 2 + dx, centerY = top + height / 2 - dy;

            result[name] = new CosmicButton(
                ((string)highlight.Attribute("file")).Replace('\\', '/'),
                [(centerX - highlightWidth / 2) / MAP_WIDTH, (centerY - highlightHeight / 2) / MAP_HEIGHT, highlightWidth / MAP_WIDTH, highlightHeight / MAP_HEIGHT],
                [left / MAP_WIDTH, top / MAP_HEIGHT, width / MAP_WIDTH, height / MAP_HEIGHT]);
        }
        return result;

        static XElement Child(XElement element, string name) => element.Elements().FirstOrDefault(e => e.Name.LocalName == name);

        // Either <Size x="" y=""/> or <Size><AbsDimension x="" y=""/></Size>
        static (float, float) Size(XElement element)
        {
            var size = Child(element, "Size");
            var dimension = Child(size, "AbsDimension") ?? size;
            return ((float)dimension.Attribute("x"), (float)dimension.Attribute("y"));
        }

        static (float, float) Offset(XElement element)
        {
            var dimension = Child(Child(Child(Child(element, "Anchors"), "Anchor"), "Offset"), "AbsDimension");
            return ((float)dimension.Attribute("x"), (float)dimension.Attribute("y"));
        }
    }

    /// <summary>Puts a map together from its twelve tiles and the explored areas drawn over them, and writes it.</summary>
    private string WriteMap(string outDir, string folder, string name, List<WorldMapOverlay> mapOverlays)
    {
        using var bitmap = ClientTextures.NewBitmap(MAP_WIDTH, MAP_HEIGHT);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);

        for (int i = 0; i < 12; i++)
        {
            using var tile = client.Load($"Interface/WorldMap/{folder}/{name}{i + 1}")
                ?? throw new FileNotFoundException($"Map tile {folder}/{name}{i + 1} is missing from the client directory.");
            canvas.DrawBitmap(tile, i % 4 * 256, i / 4 * 256, SKSamplingOptions.Default);
        }

        foreach (var overlay in mapOverlays.Where(overlay => overlay.TextureName.Length > 0))
        {
            using var image = ComposeOverlay(folder, overlay);
            canvas.DrawBitmap(image, overlay.OffsetX, overlay.OffsetY, SKSamplingOptions.Default);
        }

        string file = $"maps/{name.ToLowerInvariant()}.webp";
        ClientTextures.SaveWebp(bitmap, Path.Combine(outDir, file), MAP_QUALITY);
        return file;
    }

    /// <summary>An explored area of a map, put together from its 256px tiles.</summary>
    private SKBitmap ComposeOverlay(string folder, WorldMapOverlay overlay)
    {
        int columns = (overlay.TextureWidth + 255) / 256;
        int rows = (overlay.TextureHeight + 255) / 256;
        var bitmap = ClientTextures.NewBitmap(overlay.TextureWidth, overlay.TextureHeight);
        using var canvas = new SKCanvas(bitmap);
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                string tileName = $"Interface/WorldMap/{folder}/{overlay.TextureName}{row * columns + column + 1}";
                using var tile = client.Load(tileName);
                if (tile == null)
                {
                    logger.LogWarning("Map tile {tile} is missing from the client directory.", tileName);
                    continue;
                }
                canvas.DrawBitmap(tile, column * 256, row * 256, SKSamplingOptions.Default);
            }
        }
        return bitmap;
    }

    /// <summary>Writes a highlight texture, or the given part of it, or returns null when the client has none.</summary>
    private TrackerHighlight WriteHighlight(string outDir, string name, string texture, string blend, float[] rect, Func<SKSizeI, SKRectI> used)
    {
        using var bitmap = client.Load(texture);
        if (bitmap == null)
        {
            return null;
        }

        var source = used?.Invoke(new SKSizeI(bitmap.Width, bitmap.Height)) ?? new SKRectI(0, 0, bitmap.Width, bitmap.Height);
        using var part = ClientTextures.Crop(bitmap, source, source.Width, source.Height);
        return WriteImage(outDir, name, part, blend, 1, rect);
    }

    /// <summary>
    /// Writes a highlight's image for the launcher to draw as the game does: to add, its colour weighted by
    /// its alpha and <paramref name="gain"/>, on black; to blend, as it is.
    /// </summary>
    private static TrackerHighlight WriteImage(string outDir, string name, SKBitmap bitmap, string blend, float gain, float[] rect)
    {
        string file = $"highlights/{name.ToLowerInvariant()}.webp";
        var pixels = bitmap.Pixels;
        if (blend == ADD)
        {
            pixels = [.. pixels.Select(p => new SKColor(
                (byte)(p.Red * p.Alpha * gain / 255), (byte)(p.Green * p.Alpha * gain / 255), (byte)(p.Blue * p.Alpha * gain / 255)))];
            using var image = ClientTextures.NewBitmap(bitmap.Width, bitmap.Height);
            image.Pixels = pixels;
            ClientTextures.SaveWebp(image, Path.Combine(outDir, file), 90);
        }
        else
        {
            ClientTextures.SaveWebp(bitmap, Path.Combine(outDir, file), 90);
        }

        // The middle of what lights up the map
        var strengths = pixels.Select(p => (int)(blend == ADD ? Math.Max(p.Red, Math.Max(p.Green, p.Blue)) : p.Alpha)).ToArray();
        int max = Math.Max(1, strengths.Max());
        double total = 0, sumX = 0, sumY = 0;
        for (int i = 0; i < strengths.Length; i++)
        {
            if (strengths[i] * 2 > max)
            {
                total += strengths[i];
                sumX += strengths[i] * (i % bitmap.Width + 0.5);
                sumY += strengths[i] * (i / bitmap.Width + 0.5);
            }
        }

        return new TrackerHighlight()
        {
            Image = file,
            Blend = blend,
            Rect = [.. rect.Select(v => MathF.Round(v, 5))],
            Centroid = total == 0 ? [0.5f, 0.5f] : [MathF.Round((float)(sumX / total / bitmap.Width), 4), MathF.Round((float)(sumY / total / bitmap.Height), 4)],
        };
    }
}
