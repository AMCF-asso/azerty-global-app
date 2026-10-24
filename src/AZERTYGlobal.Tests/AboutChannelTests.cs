using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// À propos selon le canal — décisions S1-1 et S1-2 du 2026-09-02, emportées en 1.3.0 le
/// 24/10. S1-1 : le lien HelloAsso de l'association suit les liens externes (canal sobre, ou
/// politique). S1-2 : le canal suit la version, sur les trois canaux. Chaque lien est éprouvé
/// dans les deux sens, comme dans <see cref="SoberChannelTests"/>.
/// </summary>
public class AboutChannelTests
{
    private static readonly int[] Identité =
        { AboutWindow.IDC_LINK_SITE, AboutWindow.IDC_LINK_GITHUB, AboutWindow.IDC_LINK_LICENSE };

    private static readonly int[] IdentitéEtSoutien =
        { AboutWindow.IDC_LINK_SITE, AboutWindow.IDC_LINK_GITHUB, AboutWindow.IDC_LINK_LICENSE, AboutWindow.IDC_LINK_AMCF };

    [Fact]
    public void Liens_SurStore_PortentHelloAsso() =>
        Assert.Equal(IdentitéEtSoutien, AboutWindow.LinkEntries(DistributionChannel.Store));

    [Fact]
    public void Liens_SurAmcf_PerdentHelloAssoSeulement() =>
        Assert.Equal(Identité, AboutWindow.LinkEntries(DistributionChannel.Amcf));

    /// <summary>D8 : l'exécutable hors paquet garde la fenêtre d'aujourd'hui.</summary>
    [Fact]
    public void Liens_HorsPaquet_InchangésD8() =>
        Assert.Equal(IdentitéEtSoutien, AboutWindow.LinkEntries(DistributionChannel.Unpackaged));

    [Fact]
    public void Liens_PolitiqueÀZéro_ÉteintHelloAssoSurStore() =>
        Assert.Equal(Identité, AboutWindow.LinkEntries(DistributionChannel.Store, externalLinksPolicy: false));

    [Fact]
    public void Liens_PolitiqueÀUn_RallumeHelloAssoSurAmcf() =>
        Assert.Equal(IdentitéEtSoutien, AboutWindow.LinkEntries(DistributionChannel.Amcf, externalLinksPolicy: true));

    /// <summary>La fenêtre elle-même : sur AMCF, aucun contrôle pour le lien, donc rien que Tab
    /// atteigne ; sur Store, le lien existe et se voit.</summary>
    [Fact]
    public void Fenêtre_CréeLeLienSelonLeCanal()
    {
        using var isolation = new BancCapture.Isolation();

        var store = new AboutWindow();
        try
        {
            store.Show();
            IntPtr lien = BancCapture.Field<IntPtr>(store, "_hWndLinkAmcf");
            Assert.NotEqual(IntPtr.Zero, lien);
            Assert.True(Win32.IsWindowVisible(lien));
        }
        finally
        {
            BancCapture.Teardown(store);
        }

        using (AppChannel.OverrideForTests(DistributionChannel.Amcf))
        {
            var amcf = new AboutWindow();
            try
            {
                Assert.Equal(IntPtr.Zero, BancCapture.Field<IntPtr>(amcf, "_hWndLinkAmcf"));
            }
            finally
            {
                BancCapture.Teardown(amcf);
            }
        }
    }

    [Theory]
    [InlineData("fr", "Store", "Version 1.3.0 · Microsoft Store")]
    [InlineData("fr", "Amcf", "Version 1.3.0 · AMCF")]
    [InlineData("fr", "Unpackaged", "Version 1.3.0 · hors paquet")]
    [InlineData("en", "Store", "Version 1.3.0 · Microsoft Store")]
    [InlineData("en", "Amcf", "Version 1.3.0 · AMCF")]
    [InlineData("en", "Unpackaged", "Version 1.3.0 · unpackaged")]
    public void LigneDeVersion_PorteLeCanal(string langue, string canal, string attendu)
    {
        using var isolation = new BancCapture.Isolation();
        isolation.SetLanguage(langue);
        Assert.Equal(attendu, L.About_VersionLine("1.3.0", L.About_Channel(Enum.Parse<DistributionChannel>(canal))));
    }
}
