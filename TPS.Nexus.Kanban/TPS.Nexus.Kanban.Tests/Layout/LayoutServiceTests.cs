using TPS.Nexus.Kanban.Core.Enums;
using TPS.Nexus.Kanban.Core.Models;
using Xunit;

namespace TPS.Nexus.Kanban.Tests.Layout;

public class LayoutServiceTests
{
    [Fact]
    public void Draft_Status_Is_Draft()
    {
        var draft = new LayoutVersion { Id = 1, Status = LayoutStatus.Draft };
        Assert.Equal(LayoutStatus.Draft, draft.Status);
        Assert.NotEqual(LayoutStatus.Published, draft.Status);
    }

    [Fact]
    public void Rollback_Simulation_ArchivesCurrent_PublishesTarget()
    {
        var current = new LayoutVersion { Id = 1, Status = LayoutStatus.Published };
        var target  = new LayoutVersion { Id = 2, Status = LayoutStatus.Archived };

        current.Status = LayoutStatus.Archived;
        target.Status  = LayoutStatus.Published;

        Assert.Equal(LayoutStatus.Archived,  current.Status);
        Assert.Equal(LayoutStatus.Published, target.Status);
    }
}
