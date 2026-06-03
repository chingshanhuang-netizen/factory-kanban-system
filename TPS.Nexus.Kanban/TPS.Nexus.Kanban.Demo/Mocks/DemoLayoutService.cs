using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Interfaces;
using TPS.Nexus.Kanban.Core.Models;

namespace TPS.Nexus.Kanban.Demo.Mocks;

public class DemoLayoutService : ILayoutService
{
    private int _nextId = 4;

    private readonly List<LayoutVersion> _versions = new()
    {
        new() { Id=1, FactoryMapId=1, VersionNo=1, Status=LayoutStatus.Archived,  CreatedBy="系統管理員", PublishedAt=DateTime.UtcNow.AddDays(-14), LayoutJson="{}" },
        new() { Id=2, FactoryMapId=1, VersionNo=2, Status=LayoutStatus.Archived,  CreatedBy="系統管理員", PublishedAt=DateTime.UtcNow.AddDays(-7),  LayoutJson="{}" },
        new() { Id=3, FactoryMapId=1, VersionNo=3, Status=LayoutStatus.Published, CreatedBy="系統管理員", PublishedAt=DateTime.UtcNow.AddDays(-1),  LayoutJson="{}" },
    };

    public Task<LayoutVersion> SaveDraftAsync(int factoryMapId, string layoutJson, string createdBy)
    {
        if (factoryMapId <= 0) throw new ArgumentOutOfRangeException(nameof(factoryMapId));
        if (string.IsNullOrWhiteSpace(layoutJson)) throw new ArgumentException(null, nameof(layoutJson));
        if (string.IsNullOrWhiteSpace(createdBy)) throw new ArgumentException(null, nameof(createdBy));

        var next = _versions.Where(v => v.FactoryMapId == factoryMapId)
                            .DefaultIfEmpty().Max(v => v?.VersionNo ?? 0) + 1;
        var draft = new LayoutVersion
        {
            Id = _nextId++, FactoryMapId = factoryMapId,
            VersionNo = next, Status = LayoutStatus.Draft,
            CreatedBy = createdBy, LayoutJson = layoutJson,
        };
        _versions.Add(draft);
        return Task.FromResult(draft);
    }

    public Task<LayoutVersion> PublishAsync(int draftId)
    {
        if (draftId <= 0) throw new ArgumentOutOfRangeException(nameof(draftId));

        var draft = _versions.FirstOrDefault(v => v.Id == draftId)
            ?? throw new InvalidOperationException($"版本 {draftId} 不存在。");
        if (draft.Status != LayoutStatus.Draft)
            throw new InvalidOperationException($"版本 {draftId} 目前狀態為 '{draft.Status}'，只有草稿可發布。");

        var current = _versions.FirstOrDefault(v => v.FactoryMapId == draft.FactoryMapId && v.Status == LayoutStatus.Published);
        if (current != null) current.Status = LayoutStatus.Archived;

        draft.Status = LayoutStatus.Published;
        draft.PublishedAt = DateTime.UtcNow;
        return Task.FromResult(draft);
    }

    public Task<IEnumerable<LayoutVersion>> GetVersionHistoryAsync(int mapId)
    {
        if (mapId <= 0) throw new ArgumentOutOfRangeException(nameof(mapId));
        return Task.FromResult(
            _versions.Where(v => v.FactoryMapId == mapId)
                     .OrderByDescending(v => v.VersionNo)
                     .AsEnumerable());
    }

    public Task<LayoutVersion?> GetPublishedVersionAsync(int mapId)
    {
        if (mapId <= 0) throw new ArgumentOutOfRangeException(nameof(mapId));
        return Task.FromResult(
            _versions.FirstOrDefault(v => v.FactoryMapId == mapId && v.Status == LayoutStatus.Published));
    }

    public Task RollbackAsync(int versionId)
    {
        if (versionId <= 0) throw new ArgumentOutOfRangeException(nameof(versionId));

        var target = _versions.FirstOrDefault(v => v.Id == versionId)
            ?? throw new InvalidOperationException($"版本 {versionId} 不存在。");
        if (target.Status != LayoutStatus.Archived)
            throw new InvalidOperationException($"版本 {versionId} 不是已封存狀態，無法回溯。");

        var current = _versions.FirstOrDefault(v => v.FactoryMapId == target.FactoryMapId && v.Status == LayoutStatus.Published);
        if (current != null) current.Status = LayoutStatus.Archived;

        target.Status = LayoutStatus.Published;
        return Task.CompletedTask;
    }
}
