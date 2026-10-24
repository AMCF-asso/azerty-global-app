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
    [InlineData(null, "Unpackaged", false)]
    [InlineData(false, "Amcf", false)]
    [InlineData(false, "Store", false)]
    [InlineData(true, "Amcf", false)]
    [InlineData(true, "Store", false)]
    public void Ligne_SeulementCollecteÉteinteParLeCanal(bool? politique, string canal, bool attendu) =>
        Assert.Equal(attendu,
            PolicyManager.ShowsCollectionPolicyHint(politique, Enum.Parse<DistributionChannel>(canal)));

    /// <summary>La fenêtre réserve la place de la ligne, comme pour le Défi : sans elle, la
    /// ligne passerait sous les liens du bas.</summary>
    [Fact]
    public void Fenêtre_RéserveLaLigneSurAmcf()
    {
        using var isolation = new BancCapture.Isolation();
        int store = HauteurCliente(out float échelle);
        int amcf;
        using (AppChannel.OverrideForTests(DistributionChannel.Amcf))
            amcf = HauteurCliente(out _);

        int attendu = (int)(40 * échelle);
        Assert.InRange(amcf - store, attendu - 1, attendu + 1);
    }

    private static int HauteurCliente(out float échelle)
    {
        var window = new UsageStatsWindow();
        try
        {
            window.Show();
            échelle = BancCapture.Field<float>(window, "_dpiScale");
            Win32.GetClientRect(BancCapture.Handle(window), out var client);
            return client.bottom;
        }
        finally
        {
            BancCapture.Teardown(window);
        }
    }
}
