using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

public class SettingsScrollTests
{
    [Fact]
    public void Petits_deltas_finissent_par_defiler_et_conservent_le_reste()
    {
        var state = new SettingsScrollState();
        Assert.Equal(0, state.ConsumeWheel(40));
        Assert.Equal(0, state.ConsumeWheel(40));
        Assert.Equal(1, state.ConsumeWheel(60));
        Assert.Equal(1, state.ConsumeWheel(100));
        Assert.Equal(-1, state.ConsumeWheel(-120));
    }

    [Theory]
    [InlineData(100, 30, 60, 300, 100)]
    [InlineData(100, -20, 10, 300, 72)]
    [InlineData(0, 400, 430, 300, 138)]
    [InlineData(138, 100, 130, 300, 138)]
    public void Le_focus_est_rendu_visible_sans_deplacement_inutile(int scroll, int top, int bottom, int viewport, int expected)
    {
        Assert.Equal(expected, SettingsScrollState.EnsureVisible(scroll, top, bottom, viewport, 8));
    }
}
