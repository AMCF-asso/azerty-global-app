using System;
using System.Linq;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Le word-wrap des runs colorés (cartes de l'accueil) ne coupe pas aux insécables : en 1.3.0,
/// « en touches mortes. » laissait « mortes. » seul sur sa ligne malgré l'U+00A0.
/// </summary>
public class ColoredRunsWrapTests
{
    [Fact]
    public void Les_insecables_restent_dans_le_mot()
    {
        var tokens = GdiHelpers.TokenizeColoredRuns(("en touches mortes, 99 %", 0u, IntPtr.Zero));

        Assert.Equal(new[] { "en", " ", "touches mortes,", " ", "99 %" }, tokens.Select(t => t.Text));
        Assert.Equal(new[] { false, true, false, true, false }, tokens.Select(t => t.IsSpace));
    }

    [Fact]
    public void Les_espaces_ordinaires_coupent_toujours()
    {
        Assert.True(GdiHelpers.IsBreakingSpace(' '));
        Assert.True(GdiHelpers.IsBreakingSpace('\t'));
        Assert.False(GdiHelpers.IsBreakingSpace(' '));
        Assert.False(GdiHelpers.IsBreakingSpace(' '));
    }
}
