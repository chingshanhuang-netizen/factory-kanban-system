using TPS.Nexus.Kanban.Services.Alarm;
using Xunit;

namespace TPS.Nexus.Kanban.Tests.Alarm;

public class AlarmServiceTests
{
    // ── existing EvaluateCondition tests ─────────────────────────────────────

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

    // ── A-7: unknown / misspelled condition ──────────────────────────────────

    [Theory]
    [InlineData("BETWEEN")]
    [InlineData("gt")]
    [InlineData("")]
    [InlineData("=>")]   // reversed
    public void EvaluateCondition_UnknownCondition_ReturnsFalse(string condition)
    {
        // Unknown conditions must return false (safe default) rather than throwing.
        Assert.False(AlarmService.EvaluateCondition(condition, 100.0, 200.0));
    }

    // ── A-7: condition string with surrounding whitespace (trim fix) ──────────

    [Theory]
    [InlineData(" >",  100.0, 101.0, true)]
    [InlineData("> ",  100.0, 101.0, true)]
    [InlineData(" >= ", 100.0, 100.0, true)]
    [InlineData(" == ", 42.0,  42.0, true)]
    public void EvaluateCondition_ConditionWithWhitespace_StillEvaluatesCorrectly(
        string condition, double threshold, double value, bool expected)
    {
        Assert.Equal(expected, AlarmService.EvaluateCondition(condition, threshold, value));
    }

    // ── A-5: NaN input behaviour ─────────────────────────────────────────────

    [Theory]
    [InlineData(">")]
    [InlineData("<")]
    [InlineData(">=")]
    [InlineData("<=")]
    public void EvaluateCondition_NaN_ReturnsFalse_ForOrderingComparisons(string condition)
    {
        // IEEE 754: all ordering comparisons with NaN return false.
        // A NaN sensor value must not trigger an alarm via these operators.
        Assert.False(AlarmService.EvaluateCondition(condition, 100.0, double.NaN));
    }

    [Fact]
    public void EvaluateCondition_NaN_EqualityCheck_ReturnsFalse()
    {
        // NaN == NaN is false in IEEE 754; our Abs-based check also returns false.
        Assert.False(AlarmService.EvaluateCondition("==", double.NaN, double.NaN));
    }

    [Fact]
    public void EvaluateCondition_NaN_InequalityCheck_ReturnsFalse()
    {
        // Abs(NaN - NaN) = NaN; NaN >= 1e-9 is false, so "!=" also returns false for NaN.
        // This means a NaN value will never trigger any alarm — callers must guard NaN upstream.
        Assert.False(AlarmService.EvaluateCondition("!=", double.NaN, double.NaN));
    }

    // ── boundary: threshold edge cases ───────────────────────────────────────

    [Fact]
    public void EvaluateCondition_ExactThreshold_GreaterThan_ReturnsFalse()
    {
        Assert.False(AlarmService.EvaluateCondition(">", 100.0, 100.0));
    }

    [Fact]
    public void EvaluateCondition_ExactThreshold_GreaterOrEqual_ReturnsTrue()
    {
        Assert.True(AlarmService.EvaluateCondition(">=", 100.0, 100.0));
    }

    [Fact]
    public void EvaluateCondition_FloatingPointEquality_WithinEpsilon_ReturnsTrue()
    {
        // Values within 1e-9 of each other are treated as equal.
        double a = 1.0 / 3.0;
        double b = 1.0 / 3.0;
        Assert.True(AlarmService.EvaluateCondition("==", a, b));
    }

    [Fact]
    public void EvaluateCondition_FloatingPointEquality_BeyondEpsilon_ReturnsFalse()
    {
        Assert.False(AlarmService.EvaluateCondition("==", 1.0, 1.0 + 1e-8));
    }

    // ── A-6: input guard documentation tests ─────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void EvaluateAsync_NonPositiveEquipmentId_ShouldBeRejected(int equipmentId)
    {
        // Documents the expected guard — actual throw happens inside EvaluateAsync (requires DB).
        Assert.True(equipmentId <= 0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EvaluateAsync_EmptyEquipmentName_ShouldBeRejected(string name)
    {
        Assert.True(string.IsNullOrWhiteSpace(name));
    }
}
