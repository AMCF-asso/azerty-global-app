using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// A1 (candidat 1.3.0) : les rangées du bas de « Mes statistiques » suivent le texte mesuré.
/// Calcul pur ; le dessin et le défilement restent du Win32, prouvés par la recette et les
/// captures. Échelle de référence : <c>s</c> = identité (100 %), ou ×2 / ×3.
/// </summary>
public class UsageStatsRowsTests
{
    private static readonly Func<int, int> Pct100 = v => v;
    private static readonly Func<int, int> Pct200 = v => v * 2;
    private static readonly Func<int, int> Pct300 = v => v * 3;

    [Fact]
    public void TexteCourt_LaHauteurDeReferenceReste_BoutonsAuBasDuCadre()
    {
        var rows = UsageStatsWindow.ComputeRows(textBottom: 100, minContentH: 460, rateVisible: false, Pct100);

        Assert.Equal(460, rows.ContentHeight);
        Assert.Equal(460 - 20 - 32, rows.ButtonY);
        Assert.Equal(0, rows.RateY);
        Assert.True(rows.LinkY > 100, "les liens restent sous le texte");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TexteLong_LaFenetreGrandit_RienNePasseSousLesBoutons(bool noter)
    {
        foreach (var s in new[] { Pct100, Pct200, Pct300 })
        {
            int textBottom = s(700); // bien au-delà du plancher de référence
            var rows = UsageStatsWindow.ComputeRows(textBottom, minContentH: s(460), noter, s);

            Assert.True(rows.ContentHeight > s(460));
            Assert.True(rows.LinkY >= textBottom, "les liens sont sous le dernier paragraphe");
            if (noter)
            {
                Assert.True(rows.RateY >= rows.LinkY + s(22), "« Noter » sous les liens");
                Assert.True(rows.ButtonY >= rows.RateY + s(32), "« Copier » et « Fermer » sous « Noter »");
            }
            else
            {
                Assert.Equal(0, rows.RateY);
                Assert.True(rows.ButtonY >= rows.LinkY + s(22), "boutons sous les liens");
            }
            Assert.Equal(rows.ButtonY + s(32) + s(20), rows.ContentHeight);
        }
    }

    [Fact]
    public void BoutonNoter_AjouteUneRangee_SansReduireLeContenu()
    {
        var sans = UsageStatsWindow.ComputeRows(300, 460, rateVisible: false, Pct100);
        var avec = UsageStatsWindow.ComputeRows(300, 460, rateVisible: true, Pct100);

        Assert.True(avec.ContentHeight >= sans.ContentHeight);
        Assert.True(avec.RateY > 0);
        Assert.True(avec.ButtonY > avec.RateY);
    }
}
