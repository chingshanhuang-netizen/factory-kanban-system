using Dapper;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Constants;
using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Services.Map;

public class MapImportService : IMapImportService
{
    private readonly IDbConnectionFactory _db;
    private readonly string _webRootPath;
    private readonly ImageMapHandler _imageHandler;
    private readonly SvgMapParser _svgParser;
    private readonly DxfMapParser _dxfParser;
    private readonly JsonXmlCoordParser _coordParser;

    public MapImportService(IDbConnectionFactory db, IWebHostEnvironmentAccessor envAccessor)
    {
        _db = db;
        _webRootPath = envAccessor.WebRootPath;
        _imageHandler = new ImageMapHandler(_webRootPath);
        _svgParser = new SvgMapParser(_webRootPath);
        _dxfParser = new DxfMapParser(_webRootPath);
        _coordParser = new JsonXmlCoordParser();
    }

    public async Task<FactoryMap> ImportAsync(Stream file, string fileName, MapFormatType format)
    {
        // MI-2: null stream produces a NullReferenceException deep inside the format handler
        ArgumentNullException.ThrowIfNull(file);

        // MI-1: null/empty fileName causes map.Name = null which violates NOT NULL DB constraint
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("fileName must not be null or whitespace.", nameof(fileName));

        var map = format switch
        {
            MapFormatType.Png or MapFormatType.Jpg => await _imageHandler.HandleAsync(file, fileName, format),
            MapFormatType.Svg => await _svgParser.ParseAsync(file, fileName),
            MapFormatType.Dxf => await _dxfParser.ParseAsync(file, fileName),
            MapFormatType.JsonCoord => await _coordParser.ParseJsonAsync(file, fileName),
            MapFormatType.XmlCoord => await _coordParser.ParseXmlAsync(file, fileName),
            _ => throw new NotSupportedException($"Format {format} not supported.")
        };

        map.Name = Path.GetFileNameWithoutExtension(fileName);

        await using var conn = _db.CreateConnection();
        map.Id = await conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO kanban_factory_maps (Name, FormatType, FilePath, ThumbnailPath, CreatedAt)
            VALUES (@Name, @FormatType, @FilePath, @ThumbnailPath, @CreatedAt);
            SELECT LAST_INSERT_ID();
            """, map);

        return map;
    }

    public async Task<IEnumerable<FactoryMap>> GetAllAsync()
    {
        await using var conn = _db.CreateConnection();
        return await conn.QueryAsync<FactoryMap>("SELECT * FROM kanban_factory_maps ORDER BY CreatedAt DESC");
    }

    public async Task UpdateAsync(FactoryMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (map.Id <= 0) throw new ArgumentOutOfRangeException(nameof(map));
        await using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            """
            UPDATE kanban_factory_maps
            SET Name = @Name, Floor = @Floor, Area = @Area,
                CarouselEnabled = @CarouselEnabled, CarouselOrder = @CarouselOrder, CarouselSeconds = @CarouselSeconds
            WHERE Id = @Id
            """, new { map.Name, map.Floor, map.Area,
                       map.CarouselEnabled, map.CarouselOrder, map.CarouselSeconds,
                       map.Id });
    }

    public async Task DeleteAsync(int mapId)
    {
        // S-7: DELETE WHERE Id=0 silently affects no rows and leaks physical files
        if (mapId <= 0)
            throw new ArgumentOutOfRangeException(nameof(mapId), $"mapId must be positive, got {mapId}.");

        await using var conn = _db.CreateConnection();

        // MI-3: read FilePath/ThumbnailPath before deleting the DB record so we can clean up
        // the physical files. Without this, orphaned files accumulate on disk indefinitely.
        var map = await conn.QueryFirstOrDefaultAsync<FactoryMap>(
            "SELECT FilePath, ThumbnailPath FROM kanban_factory_maps WHERE Id = @Id",
            new { Id = mapId });

        await conn.ExecuteAsync("DELETE FROM kanban_factory_maps WHERE Id = @Id", new { Id = mapId });

        if (map != null)
        {
            TryDeleteFile(map.FilePath);
            TryDeleteFile(map.ThumbnailPath);
        }
    }

    // Resolves a stored path (either a URL-relative path or an absolute temp path) to the
    // physical file and deletes it. Best-effort: IOException is swallowed so a missing file
    // does not abort the delete operation.
    private void TryDeleteFile(string? storedPath)
    {
        if (string.IsNullOrEmpty(storedPath)) return;
        try
        {
            string fullPath;
            if (Path.IsPathRooted(storedPath))
            {
                fullPath = storedPath;          // JsonCoord/XmlCoord absolute temp path
            }
            else
            {
                var fileName = Path.GetFileName(storedPath);
                fullPath = Path.Combine(_webRootPath, KanbanAssets.MapsSubdir, fileName);
            }
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch (IOException) { }
    }
}
