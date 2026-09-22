using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

public class DisplayGlyphTests
{
    [Theory]
    [InlineData("̏", "◌̏")]
    [InlineData("̛", "◌̛")]
    [InlineData("̉", "◌̉")]
    [InlineData("̑", "◌̑")]
    [InlineData("é", "é")]
    [InlineData("é", "é")]
    [InlineData("", "")]
    public void Le_support_n_est_ajoute_qu_aux_marques_isolees(string text, string expected)
    {
        Assert.Equal(expected, DisplayGlyph.ForStandaloneMark(text));
    }
}
