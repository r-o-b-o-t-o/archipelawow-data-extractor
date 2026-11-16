namespace ArchipelaWoW.DataExtractor.Services;

/// <summary>
/// The areas of the server's extracted maps (AzerothCore's <c>data/maps</c>): which area the ground at a spot of a map is
/// in. The core goes by the building or cave a spot is in first, which only the vmaps know, so a spot inside one can be
/// in another area than the ground above it.
/// </summary>
public class ServerMaps
{
    // As the core's GridDefines.h
    private const double SIZE_OF_GRIDS = 533.3333f;
    private const int CENTER_GRID_ID = 32;
    private const int MAX_NUMBER_OF_GRIDS = 64;
    private const int CELLS_PER_GRID = 16;

    private const uint MAP_MAGIC = 0x5350414D; // "MAPS"
    private const ushort MAP_AREA_NO_AREA = 0x0001;

    private readonly string directory;
    private readonly Dictionary<(uint Map, int X, int Y), ushort[]> grids = [];

    public ServerMaps()
    {
        directory = Env.GetString("MAPS_DIRECTORY");
        if (directory == null || !Directory.Exists(directory))
        {
            throw new InvalidOperationException("Maps directory not found. Set the MAPS_DIRECTORY environment variable to the server's data/maps.");
        }
    }

    /// <summary>The <c>AreaTable.dbc</c> id of the area of the ground at a spot, 0 where the maps have none.</summary>
    public int GetAreaId(uint mapId, double x, double y)
    {
        // As the core's ComputeGridCoord and GridTerrainData::getArea
        int cellX = (int)Math.Floor(CELLS_PER_GRID * (CENTER_GRID_ID - x / SIZE_OF_GRIDS));
        int cellY = (int)Math.Floor(CELLS_PER_GRID * (CENTER_GRID_ID - y / SIZE_OF_GRIDS));
        if (cellX < 0 || cellY < 0 || cellX >= MAX_NUMBER_OF_GRIDS * CELLS_PER_GRID || cellY >= MAX_NUMBER_OF_GRIDS * CELLS_PER_GRID)
        {
            return 0;
        }

        var areas = GetGrid(mapId, cellX / CELLS_PER_GRID, cellY / CELLS_PER_GRID);
        return areas == null ? 0 : areas[(cellX % CELLS_PER_GRID) * CELLS_PER_GRID + cellY % CELLS_PER_GRID];
    }

    /// <summary>A grid's areas, cell by cell, or null when the map has no file for it, such as out at sea.</summary>
    private ushort[] GetGrid(uint mapId, int gridX, int gridY)
    {
        if (grids.TryGetValue((mapId, gridX, gridY), out var cached))
        {
            return cached;
        }

        ushort[] areas = null;
        string path = Path.Combine(directory, $"{mapId:D3}{gridX:D2}{gridY:D2}.map");
        if (File.Exists(path))
        {
            using var reader = new BinaryReader(File.OpenRead(path));
            if (reader.ReadUInt32() != MAP_MAGIC)
            {
                throw new InvalidDataException($"\"{path}\" is not a map file extracted for AzerothCore.");
            }

            // map_fileheader: mapMagic, versionMagic, buildMagic, then areaMapOffset
            reader.BaseStream.Position = 3 * sizeof(uint);
            reader.BaseStream.Position = reader.ReadUInt32();

            // map_areaHeader: fourcc, flags, gridArea, then the cells unless the whole grid is one area
            reader.ReadUInt32();
            ushort flags = reader.ReadUInt16();
            ushort gridArea = reader.ReadUInt16();
            areas = new ushort[CELLS_PER_GRID * CELLS_PER_GRID];
            for (int i = 0; i < areas.Length; i++)
            {
                areas[i] = (flags & MAP_AREA_NO_AREA) != 0 ? gridArea : reader.ReadUInt16();
            }
        }

        grids[(mapId, gridX, gridY)] = areas;
        return areas;
    }
}
