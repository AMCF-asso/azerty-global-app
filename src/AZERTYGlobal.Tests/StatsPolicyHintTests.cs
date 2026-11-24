using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Statistiques sur le canal sobre — décision S1-3 du 2026-09-02, emportée en 1.3.0 le 24/10 :
/// la collecte éteinte par le seul défaut du canal, une ligne dit qu'une stratégie de groupe peut
/// la rallumer. Jamais quand une politique a tranché, dans un sens ou dans l'autre.
/// </summary>
public class StatsPolicyHintTests
{
    [Theory]
    [InlineData(null, "Amcf", true)]
    [InlineData(null, "Store", false)]
    [InlineData(null, "Unpackaged", true)]
    [InlineData(false, "Amcf", false)]
    [InlineData(false, "Store", false)]
    [InlineData(true, "Amcf", false)]
    [InlineData(true, "Store", false)]
    public void Ligne_SeulementCollecteÉteinteParLeCanal(bool? politique, string canal, bool attendu) =>
        Assert.Equal(attendu,
            PolicyManager.ShowsCollectionPolicyHint(politique, Enum.Parse<DistributionChannel>(canal)));

    /// <summary>La fenêtre réserve la place de la ligne, comme pour le Défi : sans elle, la
    /// ligne passerait sous les liens du bas. Depuis A1 (candidat 1.3.0), la hauteur suit le
    /// texte mesuré au lieu d'un +40 px fixe : sur Amcf le texte descend plus bas que sur le
    /// Store, et dans les deux cas les liens restent sous lui. La fenêtre ne rétrécit jamais.</summary>
    [Fact]
    public void Fenêtre_RéserveLaLigneSurAmcf()
    {
        using var isolation = new BancCapture.Isolation();
        var store = Mesure();
        (int TextBottom, int LinkTop, int ClientBottom) amcf;
        using (AppChannel.OverrideForTests(DistributionChannel.Amcf))
            amcf = Mesure();

        Assert.True(amcf.TextBottom > store.TextBottom,
            $"la ligne de stratégie de groupe ajoute du texte : Amcf {amcf.TextBottom}, Store {store.TextBottom}");
        Assert.True(store.LinkTop >= store.TextBottom, $"Store : liens à {store.LinkTop}, texte jusqu'à {store.TextBottom}");
        Assert.True(amcf.LinkTop >= amcf.TextBottom, $"Amcf : liens à {amcf.LinkTop}, texte jusqu'à {amcf.TextBottom}");
        Assert.True(amcf.ClientBottom >= store.ClientBottom);
    }

    /// <summary>Bas du texte mesuré, haut des liens et bas de la zone cliente, en pixels cliente.</summary>
    private static (int TextBottom, int LinkTop, int ClientBottom) Mesure()
    {
        var window = new UsageStatsWindow();
        try
        {
            window.Show();
            IntPtr hWnd = BancCapture.Handle(window);
            Win32.GetClientRect(hWnd, out var client);
            int textBottom = (int)BancCapture.Call(window, "MeasureTextBottom", client.right)!;
            Win32.GetWindowRect(BancCapture.Field<IntPtr>(window, "_hWndLinkFeedback"), out var link);
            var origin = new Win32.POINT { x = 0, y = 0 };
            Win32.ClientToScreen(hWnd, ref origin);
            return (textBottom, link.top - origin.y, client.bottom);
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }
}
