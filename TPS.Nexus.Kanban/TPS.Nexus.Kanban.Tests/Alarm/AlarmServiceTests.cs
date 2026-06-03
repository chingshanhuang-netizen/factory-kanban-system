using TPS.Nexus.Kanban.Services.Alarm;
using Xunit;

namespace TPS.Nexus.Kanban.Tests.Alarm;

public class AlarmServiceTests
{
    [Theory]
    [InlineData(">",  100.0, 101.0, true)]
    [InlineData(">",  100.0,  99.0, false)]
    [InlineData("<",   50.0,  49.0, true)]
    [InlineData("<",   50.0,  51.0, false)]
    [InlineData("==",  42.0,  42.0, true)]
    [InlineData("==",  42.0,  43.0, false)]
    [InlineData("!=",  42.0,  43.0, true)]
    [InlineData("!=",  42.0,  42.0, false)]
    public void EvaluateCondition_ReturnsExpected(string condition, double threshold, double value, bool expected)
    {
        Assert.Equal(expected, AlarmService.EvaluateCondition(condition, threshold, value));
    }
}
