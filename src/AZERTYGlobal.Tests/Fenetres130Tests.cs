using System;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Lot 3a « fenêtres » de l'audit 1.3.0 (A9, A12 à A18) : ce qui se vérifie sans ouvrir de
/// fenêtre. Le rendu lui-même se vérifie au banc de captures.
/// </summary>
public class Fenetres130Tests
{
    // ═══ A15 : contraste du texte « à venir » ═══

    /// <summary>Rapport de contraste WCAG 2 entre deux COLORREF (0x00BBGGRR).</summary>
    internal static double ContrastRatio(uint a, uint b)
    {
        static double Channel(uint c, int shift)
        {
            double v = ((c >> shift) & 0xFF) / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
        static double Luminance(uint c) => 0.2126 * Channel(c, 0) + 0.7152 * Channel(c, 8) + 0.0722 * Channel(c, 16);
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    [Theory]
    [InlineData(PaperTheme.Paper)]
    [InlineData(PaperTheme.Surface)]
    [InlineData(PaperTheme.FunctionKey)]
    [InlineData(PaperTheme.ActionTint)]
    public void PaleText_AtteintQuatreEtDemiSurChaqueFondPapier(uint fond)
        => Assert.True(ContrastRatio(PaperTheme.PaleText, fond) >= 4.5,
            $"PaleText sur #{fond:X6} : {ContrastRatio(PaperTheme.PaleText, fond):0.00}:1");

    [Fact]
    public void Pale_ResteSousLeSeuil_CeQuiJustifiePaleText()
        => Assert.True(ContrastRatio(PaperTheme.Pale, PaperTheme.Paper) < 4.5);

    [Fact]
    public void Contraste_CalculConformeAuxValeursDeLAudit()
    {
        // contraste.py de l'audit : Ink/Paper 16,65 ; Pale/Paper 3,49 ; Pale/Surface 3,71.
        Assert.Equal(16.65, ContrastRatio(PaperTheme.Ink, PaperTheme.Paper), 2);
        Assert.Equal(3.49, ContrastRatio(PaperTheme.Pale, PaperTheme.Paper), 2);
        Assert.Equal(3.71, ContrastRatio(PaperTheme.Pale, PaperTheme.Surface), 2);
    }

    // ═══ A12 : barre de titre papier ═══

    [Fact]
    public void BarreDeTitre_FenêtreClaire_PrendLePapier()
        => Assert.Equal((PaperTheme.Paper, PaperTheme.TextSecondary), NativeWindow.CaptionColors(dark: false, highContrast: false));

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void BarreDeTitre_SombreOuContrasteÉlevé_LaisseWindowsDécider(bool dark, bool highContrast)
        => Assert.Equal((0xFFFFFFFFu, 0xFFFFFFFFu), NativeWindow.CaptionColors(dark, highContrast));

    // ═══ A14 : contraste élevé, couleurs du système ═══

    /// <summary>Un thème de contraste élevé factice : chaque index GetSysColor rend une couleur à soi.</summary>
    private static uint FakeSysColor(int index) => (uint)(0x00010000 * (index + 1));

    [Fact]
    public void Palette_HorsContrasteÉlevé_EstLePapier()
        => Assert.Equal(PaperPalette.Default, PaperPalette.For(false, FakeSysColor));

    [Fact]
    public void Palette_ContrasteÉlevé_PrendLesCouleursDuSystème()
    {
        var p = PaperPalette.For(true, FakeSysColor);
        Assert.True(p.HighContrast);
        Assert.Equal(FakeSysColor(5), p.Paper);        // COLOR_WINDOW
        Assert.Equal(FakeSysColor(5), p.Surface);
        Assert.Equal(FakeSysColor(8), p.Ink);          // COLOR_WINDOWTEXT
        Assert.Equal(FakeSysColor(8), p.Muted);
        Assert.Equal(FakeSysColor(8), p.Error);        // pas de rouge système : le texte
        Assert.Equal(FakeSysColor(26), p.Action);      // COLOR_HOTLIGHT
        Assert.Equal(FakeSysColor(13), p.ActionFill);  // COLOR_HIGHLIGHT
        Assert.Equal(FakeSysColor(14), p.OnAction);    // COLOR_HIGHLIGHTTEXT
        Assert.Equal(FakeSysColor(17), p.PaleText);    // COLOR_GRAYTEXT
    }

    [Fact]
    public void Paramètres_MessageDeValidation_SuitLaPalette()
    {
        var hc = PaperPalette.For(true, FakeSysColor);
        Assert.Equal(PaperTheme.Error, SettingsWindow.ValidationTextColor(refused: true));
        Assert.Equal(hc.Ink, SettingsWindow.ValidationTextColor(refused: true, hc));
        Assert.Equal(hc.Muted, SettingsWindow.ValidationTextColor(refused: false, hc));
    }

    // ═══ A9 : Durée de pause au papier ═══

    [Fact]
    public void Pause_LigneDeReprise_EncreOuErreurDuPapier()
    {
        Assert.Equal(PaperTheme.Ink, PauseDurationDialog.ResumeLineColor(true, PaperPalette.Default));
        Assert.Equal(PaperTheme.Error, PauseDurationDialog.ResumeLineColor(false, PaperPalette.Default));
    }

    // ═══ A18 : la Recherche parle ═══

    [Fact]
    public void Recherche_RésultatChoisi_LuAvecSaPlaceEtSonGeste()
    {
        string avant = L.Language;
        try
        {
            L.Language = "fr";
            string fr = L.Search_AccessibleResult(2, 14, "é", "Lettre minuscule latine e accent aigu", "AltGr + E.");
            Assert.Equal("Résultat 2 sur 14 : é, Lettre minuscule latine e accent aigu. AltGr + E.", fr);
            Assert.Equal("Rechercher un caractère. " + fr, CharacterSearch.AccessibleFieldName(fr));

            L.Language = "en";
            Assert.Equal("Result 2 of 14: é, Latin small letter e with acute. AltGr + E.",
                L.Search_AccessibleResult(2, 14, "é", "Latin small letter e with acute", "AltGr + E."));
        }
        finally
        {
            L.Language = avant;
        }
    }

    [Fact]
    public void Recherche_RienÀDire_LeChampGardeSonNom()
    {
        string avant = L.Language;
        try
        {
            L.Language = "fr";
            Assert.Equal(L.Search_WindowTitle, CharacterSearch.AccessibleFieldName(null));
            Assert.Equal(L.Search_WindowTitle, CharacterSearch.AccessibleFieldName(""));
            Assert.Equal("Rechercher un caractère. Aucun résultat", CharacterSearch.AccessibleFieldName(L.Search_NoResults));
        }
        finally
        {
            L.Language = avant;
        }
    }
}
