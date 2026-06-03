using Dapper;
using TPS.Nexus.Core;
using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Models;
using IWebHostEnvironmentAccessor = TPS.Nexus.Kanban.Core.Interfaces.IWebHostEnvironmentAccessor;

namespace TPS.Nexus.Kanban.Services.Map;

public class MapImportService : IMapImportService
{
    private readonly IDbConnectionFactory _db;
    private readonly ImageMapHandler _imageHandler;
    private readonly SvgMapParser _svgParser;
    private readonly DxfMapParser _dxfParser;
    private readonly JsonXmlCoordParser _coordParser;

    public MapImportService(IDbConnectionFactory db, IWebHostEnvironmentAccessor envAccessor)
    {
        _db = db;
        var root = envAccessor.WebRootPath;
        _imageHandler = new ImageMapHandler(root);
        _svgParser = new SvgMapParser(root);
        _dxfParser = new DxfMapParser(root);
        _coordParser = new JsonXmlCoordParser();
    }

    public async Task<FactoryMap> ImportAsync(Stream file, string fileName, MapFormatType format)
    {
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

        using var conn = _db.CreateConnection();
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
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<FactoryMap>("SELECT * FROM kanban_factory_maps ORDER BY CreatedAt DESC");
    }

    public async Task DeleteAsync(int mapId)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM kanban_factory_maps WHERE Id = @Id", new { Id = mapId });
    }
}
