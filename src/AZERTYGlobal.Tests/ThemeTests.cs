using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Socle de thème — chantier CH0 de la refonte graphique v1.2.0.
///
/// Cette suite verrouille trois choses que rien d'autre ne peut attraper, parce qu'une couleur
/// fausse produit toujours une couleur valide : l'ordre des octets d'un COLORREF, le fait
/// qu'aucune nuance hors charte n'entre dans une palette, et les ratios WCAG que la charte
/// annonce. Les valeurs attendues sont recopiées de la charte en notation hexadécimale usuelle,
/// jamais reconstruites depuis <c>Theme.Rgb</c> — un test qui rappelle la fonction qu'il
/// vérifie passe au vert quelle que soit sa définition.
///
/// Source de vérité en cas d'écart :
/// operations/refonte-app/2026-08-28-audit-refonte-ui.md, §6.
/// </summary>
public class ThemeTests
{
    // ═══════════════════════════════════════════════════════════════
    // La charte, recopiée
    // ═══════════════════════════════════════════════════════════════

    private static readonly (string Name, string Hex)[] LightCharte =
    {
        ("Paper", "#FAF8F1"),
        ("Surface", "#FFFFFF"),
        ("Ink", "#1B1813"),
        ("TextSecondary", "#5B554A"),
        ("Border", "#D9D2C3"),
        ("Action", "#1A3EF2"),
        ("Success", "#186339"),
        ("Warning", "#8A5200"),
        ("Error", "#B02A1E"),
        ("SuccessFill", "#E9F2EA"),
        ("WarningFill", "#F7EDDC"),
        ("ErrorFill", "#F9E9E6"),
        ("ActionFill", "#E3E9FC"),
    };

    private static readonly (string Name, string Hex)[] DarkCharte =
    {
        ("Paper", "#1B1813"),
        ("Surface", "#241F17"),
        ("Ink", "#FAF8F1"),
        ("TextSecondary", "#B3A996"),
        ("Border", "#453D30"),
        ("Action", "#8FA6FF"),
        ("Success", "#6FBF8B"),
        ("Warning", "#D9A045"),
        ("Error", "#E88070"),
        ("SuccessFill", "#1E2A20"),
        ("WarningFill", "#2C2212"),
        ("ErrorFill", "#2D1A16"),
        ("ActionFill", "#1F2438"),
    };

    private static IReadOnlyDictionary<string, uint> Tokens(Palette p) => new Dictionary<string, uint>
    {
        ["Paper"] = p.Paper,
        ["Surface"] = p.Surface,
        ["Ink"] = p.Ink,
        ["TextSecondary"] = p.TextSecondary,
        ["Border"] = p.Border,
        ["Action"] = p.Action,
        ["Success"] = p.Success,
        ["Warning"] = p.Warning,
        ["Error"] = p.Error,
        ["SuccessFill"] = p.SuccessFill,
        ["WarningFill"] = p.WarningFill,
        ["ErrorFill"] = p.ErrorFill,
        ["ActionFill"] = p.ActionFill,
    };

    // ═══════════════════════════════════════════════════════════════
    // Ordre des octets
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Le piège qui a coûté onze occurrences d'une couleur fantôme à l'application actuelle :
    /// le COLORREF du bleu Windows #0078D4 s'écrit 0x00D47800, ce qui se lit exactement comme
    /// la notation hexadécimale de l'orange #D47800. Quelqu'un a recopié le COLORREF en croyant
    /// recopier une couleur, et l'orange est devenu l'accent de fait de l'application.
    /// </summary>
    [Fact]
    public void Rgb_LeColorrefDuBleuWindows_SeLitCommeLOrangeFantome()
    {
        Assert.Equal(0x00D47800u, Theme.Rgb(0x00, 0x78, 0xD4));
    }

    [Fact]
    public void Rgb_PrendSesOctetsDansLOrdreDeLaCharte()
    {
        // #FAF8F1 : rouge FA, vert F8, bleu F1 — GDI attend 0x00BBGGRR.
        Assert.Equal(0x00F1F8FAu, Theme.Rgb(0xFA, 0xF8, 0xF1));
        Assert.Equal(0x00000000u, Theme.Rgb(0x00, 0x00, 0x00));
        Assert.Equal(0x00FFFFFFu, Theme.Rgb(0xFF, 0xFF, 0xFF));
    }

    // ═══════════════════════════════════════════════════════════════
    // Les deux tables valent la charte
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void PaletteClaire_ValeurParValeur_EgaleLaCharte()
    {
        AssertePalette(Theme.LightPalette, LightCharte, "clair");
    }

    [Fact]
    public void PaletteSombre_ValeurParValeur_EgaleLaCharte()
    {
        AssertePalette(Theme.DarkPalette, DarkCharte, "sombre");
    }

    /// <summary>
    /// Décompose le COLORREF stocké comme GDI le lit — rouge en octet de poids faible — et le
    /// compare aux trois octets de la notation hexadécimale. Un jeton dont les octets auraient
    /// été saisis à l'envers est vert pour tout le reste de l'application et rouge ici.
    /// </summary>
    private static void AssertePalette(Palette palette, (string Name, string Hex)[] charte, string theme)
    {
        var tokens = Tokens(palette);

        foreach (var (name, hex) in charte)
        {
            uint value = tokens[name];
            int r = (int)(value & 0xFF);
            int g = (int)((value >> 8) & 0xFF);
            int b = (int)((value >> 16) & 0xFF);
            string relu = $"#{r:X2}{g:X2}{b:X2}";

            Assert.True(hex.Equals(relu, StringComparison.OrdinalIgnoreCase),
                $"Jeton {name} du thème {theme} : la charte dit {hex}, la palette porte {relu} " +
                $"(COLORREF 0x{value:X8}).");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Aucune couleur hors charte
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Treize jetons, et treize valeurs distinctes : les deux membres « On… » ne portent aucune
    /// couleur propre. Ce test tombe aussi bien si quelqu'un invente une nuance que s'il fait
    /// dériver un OnAction vers une valeur qui n'est plus celle d'un jeton.
    /// </summary>
    [Fact]
    public void UnePalette_NAQueLesTreizeCouleursDeLaCharte()
    {
        AsserteFermeture(ThemeVariant.Light);
        AsserteFermeture(ThemeVariant.Dark);
    }

    private static void AsserteFermeture(ThemeVariant variant)
    {
        var palette = Theme.ForVariant(variant);
        var charte = (variant == ThemeVariant.Dark ? DarkCharte : LightCharte)
            .Select(t => Tokens(palette)[t.Name])
            .ToHashSet();

        Assert.Equal(13, charte.Count);

        var employees = new[]
        {
            palette.Paper, palette.Surface, palette.Ink, palette.TextSecondary, palette.Border,
            palette.Action, palette.Success, palette.Warning, palette.Error,
            palette.SuccessFill, palette.WarningFill, palette.ErrorFill, palette.ActionFill,
            palette.OnAction, palette.OnActionFill, palette.Disabled,
        }.ToHashSet();

        Assert.Equal(13, employees.Count);
        Assert.True(employees.SetEquals(charte),
            $"Le thème {variant} emploie une couleur absente de la charte : " +
            string.Join(", ", employees.Except(charte).Select(c => $"0x{c:X8}")));
    }

    /// <summary>
    /// L'asymétrie du bouton primaire est celle du site, elle n'est pas un oubli : blanc sur
    /// bleu au thème clair, encre sombre sur bleu clair au thème sombre. Poser l'encre du thème
    /// courant dans les deux cas donnerait 1,4:1 au sombre, soit un bouton illisible.
    /// </summary>
    [Fact]
    public void OnAction_NEstPasUneCouleurDePlus()
    {
        Assert.Equal(Theme.LightPalette.Surface, Theme.LightPalette.OnAction);
        Assert.Equal(Theme.DarkPalette.Paper, Theme.DarkPalette.OnAction);
        Assert.NotEqual(Theme.DarkPalette.Ink, Theme.DarkPalette.OnAction);
    }

    [Fact]
    public void OnActionFill_EstToujoursLEncre()
    {
        Assert.Equal(Theme.LightPalette.Ink, Theme.LightPalette.OnActionFill);
        Assert.Equal(Theme.DarkPalette.Ink, Theme.DarkPalette.OnActionFill);
    }

    /// <summary>
    /// Disabled vaut le texte secondaire dans les deux thèmes de la charte : ce n'est pas une
    /// couleur de plus. Il n'existe en propre qu'en Contraste élevé, où le schéma système
    /// confond la surface avec le fond et le secondaire avec l'encre — un contrôle inactif y
    /// perdrait sinon ses deux signaux à la fois.
    /// </summary>
    [Fact]
    public void Disabled_NEstPasUneCouleurDePlus()
    {
        Assert.Equal(Theme.LightPalette.TextSecondary, Theme.LightPalette.Disabled);
        Assert.Equal(Theme.DarkPalette.TextSecondary, Theme.DarkPalette.Disabled);
    }

    /// <summary>
    /// Le négatif chaud échange les deux extrêmes : le fond du thème sombre est exactement
    /// l'encre du thème clair, et réciproquement. C'est de là que vient le OnAction du sombre,
    /// et c'est pourquoi il n'ajoute aucune couleur.
    /// </summary>
    [Fact]
    public void LesDeuxThemes_SontLeNegatifLUnDeLAutre()
    {
        Assert.NotEqual(Theme.LightPalette, Theme.DarkPalette);
        Assert.NotEqual(Theme.LightPalette.Paper, Theme.DarkPalette.Paper);

        Assert.Equal(Theme.LightPalette.Ink, Theme.DarkPalette.Paper);
        Assert.Equal(Theme.LightPalette.Paper, Theme.DarkPalette.Ink);
        Assert.Equal(Theme.LightPalette.Ink, Theme.DarkPalette.OnAction);
    }

    // ═══════════════════════════════════════════════════════════════
    // Ratios WCAG annoncés par la charte
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Les sept ratios que la charte chiffre, recalculés sur les valeurs réellement stockées.
    /// Ils tombent si un jeton bouge d'un octet, ce qu'aucune relecture visuelle n'attrape.
    /// </summary>
    [Fact]
    public void RatiosAnnoncesParLaCharte_SeRetrouventSurLesPalettes()
    {
        var clair = Theme.LightPalette;
        var sombre = Theme.DarkPalette;

        // action-fond, thème clair
        Assert.Equal(5.75, Contraste(clair.Action, clair.ActionFill), 2);
        Assert.Equal(14.60, Contraste(clair.Ink, clair.ActionFill), 2);
        Assert.Equal(1.14, Contraste(clair.ActionFill, clair.Paper), 2);

        // action-fond, thème sombre
        Assert.Equal(6.63, Contraste(sombre.Action, sombre.ActionFill), 2);
        Assert.Equal(14.45, Contraste(sombre.Ink, sombre.ActionFill), 2);
        Assert.Equal(6.60, Contraste(sombre.TextSecondary, sombre.ActionFill), 2);
        Assert.Equal(1.15, Contraste(sombre.ActionFill, sombre.Paper), 2);

        // Bouton primaire, l'asymétrie des deux thèmes
        Assert.Equal(6.96, Contraste(clair.OnAction, clair.Action), 2);
        Assert.Equal(7.64, Contraste(sombre.OnAction, sombre.Action), 2);
    }

    /// <summary>
    /// Les seuils que la charte pose en prose plutôt qu'en chiffres : texte lisible sur ses deux
    /// fonds, bordure de contrôle interactif au-dessus de 3:1, et bordure décorative en dessous
    /// de 2:1 — celle-là ne doit jamais pouvoir porter un état à elle seule.
    /// </summary>
    [Fact]
    public void SeuilsDeLaCharte_TiennentSurLesDeuxThemes()
    {
        AsserteSeuils(ThemeVariant.Light);
        AsserteSeuils(ThemeVariant.Dark);
    }

    private static void AsserteSeuils(ThemeVariant variant)
    {
        var p = Theme.ForVariant(variant);

        Assert.True(Contraste(p.Ink, p.Paper) >= 7.0, "encre sur fond de fenêtre");
        Assert.True(Contraste(p.Ink, p.Surface) >= 7.0, "encre sur surface");
        Assert.True(Contraste(p.TextSecondary, p.Paper) >= 4.5, "texte-2 sur fond de fenêtre");
        Assert.True(Contraste(p.TextSecondary, p.Surface) >= 4.5, "texte-2 sur surface");
        Assert.True(Contraste(p.Action, p.Paper) >= 4.5, "action sur fond de fenêtre");
        Assert.True(Contraste(p.Success, p.Paper) >= 4.5, "succès sur fond de fenêtre");
        Assert.True(Contraste(p.Warning, p.Paper) >= 4.5, "avertissement sur fond de fenêtre");
        Assert.True(Contraste(p.Error, p.Paper) >= 4.5, "erreur sur fond de fenêtre");

        // texte-2 sert de bordure aux contrôles interactifs, la charte exige 3:1.
        Assert.True(Contraste(p.TextSecondary, p.Surface) >= 3.0, "bordure de contrôle");

        // bordure décorative : délibérément discrète, donc jamais seule porteuse d'un état.
        Assert.True(Contraste(p.Border, p.Paper) < 2.0, "bordure décorative");
    }

    private static double Contraste(uint a, uint b)
    {
        double la = LuminanceRelative(a);
        double lb = LuminanceRelative(b);
        if (la < lb)
            (la, lb) = (lb, la);
        return (la + 0.05) / (lb + 0.05);
    }

    /// <summary>Luminance relative WCAG 2.x, calculée sur le COLORREF tel que GDI le lit.</summary>
    private static double LuminanceRelative(uint colorRef)
    {
        static double Canal(int octet)
        {
            double c = octet / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        int r = (int)(colorRef & 0xFF);
        int g = (int)((colorRef >> 8) & 0xFF);
        int b = (int)((colorRef >> 16) & 0xFF);
        return 0.2126 * Canal(r) + 0.7152 * Canal(g) + 0.0722 * Canal(b);
    }

    // ═══════════════════════════════════════════════════════════════
    // Contraste élevé
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// En Contraste élevé, aucun jeton custom ne s'applique. Le lecteur injecté rend un témoin
    /// distinct par indice, ce qui prouve la correspondance sans dépendre du schéma posé sur le
    /// poste qui exécute la suite — et sans avoir à en activer un.
    /// </summary>
    [Fact]
    public void ContrasteEleve_ChaqueJetonPointeVersSaCouleurSysteme()
    {
        uint Temoin(int index) => (uint)(0x00A00000 + index);

        var p = Theme.HighContrastPalette(Temoin);

        Assert.Equal(Temoin(Win32.COLOR_WINDOW), p.Paper);
        Assert.Equal(Temoin(Win32.COLOR_WINDOW), p.Surface);
        Assert.Equal(Temoin(Win32.COLOR_WINDOWTEXT), p.Ink);
        Assert.Equal(Temoin(Win32.COLOR_WINDOWFRAME), p.Border);
        Assert.Equal(Temoin(Win32.COLOR_HOTLIGHT), p.Action);
        Assert.Equal(Temoin(Win32.COLOR_HIGHLIGHT), p.ActionFill);
        Assert.Equal(Temoin(Win32.COLOR_HIGHLIGHTTEXT), p.OnAction);
        Assert.Equal(Temoin(Win32.COLOR_HIGHLIGHTTEXT), p.OnActionFill);
    }

    /// <summary>
    /// Le texte secondaire ne devient pas COLOR_GRAYTEXT : en contraste élevé, le gris est la
    /// couleur du désactivé, et un texte secondaire peint avec se lirait comme un contrôle
    /// inerte. Les trois couleurs sémantiques s'effacent elles aussi — un schéma à contraste
    /// élevé n'a ni vert de succès ni rouge d'erreur, et la charte veut de toute façon que le
    /// texte porte l'information.
    /// </summary>
    [Fact]
    public void ContrasteEleve_LeSecondaireEtLeSemantiqueValentLeTexteDeFenetre()
    {
        uint Temoin(int index) => (uint)(0x00A00000 + index);

        var p = Theme.HighContrastPalette(Temoin);
        uint texte = Temoin(Win32.COLOR_WINDOWTEXT);

        Assert.Equal(texte, p.TextSecondary);
        Assert.Equal(texte, p.Success);
        Assert.Equal(texte, p.Warning);
        Assert.Equal(texte, p.Error);

        // Le gris système est la couleur de l'inactif, et de lui seul : le texte secondaire
        // peint avec se lirait comme un contrôle inerte.
        Assert.NotEqual(Temoin(Win32.COLOR_GRAYTEXT), p.TextSecondary);
        Assert.Equal(Temoin(Win32.COLOR_GRAYTEXT), p.Disabled);
    }

    /// <summary>
    /// Aucune palette de la charte ne confond succès et encre — c'est ce qui rend l'invariant
    /// ci-dessous propre à la palette système, quel que soit le schéma du poste.
    /// </summary>
    [Fact]
    public void ContrasteEleveActif_CurrentNeSertPlusAucunJetonDeLaCharte()
    {
        Assert.NotEqual(Theme.LightPalette.Success, Theme.LightPalette.Ink);
        Assert.NotEqual(Theme.DarkPalette.Success, Theme.DarkPalette.Ink);

        using (Theme.OverrideForTests(ThemeVariant.Dark, highContrast: true))
        {
            Assert.True(Theme.IsHighContrast);
            Assert.Equal(Theme.Current.Ink, Theme.Current.Success);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // État courant et override de test
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Override_SertLaPaletteDemandee()
    {
        using (Theme.OverrideForTests(ThemeVariant.Dark))
        {
            Assert.Equal(ThemeVariant.Dark, Theme.Variant);
            Assert.Equal(Theme.DarkPalette, Theme.Current);
        }

        using (Theme.OverrideForTests(ThemeVariant.Light))
        {
            Assert.Equal(ThemeVariant.Light, Theme.Variant);
            Assert.Equal(Theme.LightPalette, Theme.Current);
        }
    }

    /// <summary>
    /// La restauration est portée par le scope et non par l'appelant, y compris quand le corps
    /// du using lève. Sans cela, un statique de processus traverse toute la suite et rend un
    /// test vert en isolation et rouge dans la suite entière — mesuré le 2026-08-18 sur
    /// <c>ConfigManager._lastErrorUtc</c>.
    /// </summary>
    [Fact]
    public void Override_RestaureLEtatPrecedent_MemeQuandLeCorpsLeve()
    {
        using (Theme.OverrideForTests(ThemeVariant.Light))
        {
            Assert.Throws<InvalidOperationException>((Action)(() =>
            {
                using (Theme.OverrideForTests(ThemeVariant.Dark))
                {
                    Assert.Equal(ThemeVariant.Dark, Theme.Variant);
                    throw new InvalidOperationException("témoin");
                }
            }));

            Assert.Equal(ThemeVariant.Light, Theme.Variant);
            Assert.False(Theme.IsHighContrast);
        }
    }

    /// <summary>Sous override, Refresh ne relit pas le système : un test qui force le thème
    /// sombre ne doit pas voir sa palette repasser au clair parce que le poste qui exécute la
    /// suite est en clair.</summary>
    [Fact]
    public void Refresh_SousOverride_NeToucheARien()
    {
        using (Theme.OverrideForTests(ThemeVariant.Dark))
        {
            Assert.False(Theme.Refresh());
            Assert.Equal(ThemeVariant.Dark, Theme.Variant);
        }
    }

    [Fact]
    public void Variant_HorsOverride_RendUneDesDeuxValeurs()
    {
        Assert.Contains(Theme.Variant, new[] { ThemeVariant.Light, ThemeVariant.Dark });
        Assert.Contains(Theme.TaskbarVariant, new[] { ThemeVariant.Light, ThemeVariant.Dark });
    }

    // ═══════════════════════════════════════════════════════════════
    // Filtre de WM_SETTINGCHANGE
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Windows diffuse WM_SETTINGCHANGE pour des dizaines de réglages sans rapport. Relire le
    /// registre à chaque message marcherait, mais ferait une lecture à chaque changement de
    /// résolution, de police système ou de fuseau horaire.
    /// </summary>
    [Theory]
    [InlineData("ImmersiveColorSet", true)]
    [InlineData("Environment", false)]
    [InlineData("Policy", false)]
    [InlineData("", false)]
    public void IsThemeSettingChange_NeRetientQueImmersiveColorSet(string section, bool attendu)
    {
        IntPtr lParam = Marshal.StringToHGlobalUni(section);
        try
        {
            Assert.Equal(attendu, Theme.IsThemeSettingChange(IntPtr.Zero, lParam));
        }
        finally
        {
            Marshal.FreeHGlobal(lParam);
        }
    }

    [Fact]
    public void IsThemeSettingChange_LParamNul_EstIgnore()
    {
        Assert.False(Theme.IsThemeSettingChange(IntPtr.Zero, IntPtr.Zero));
    }

    /// <summary>Le contraste élevé n'arrive pas par lParam mais par wParam, et il change la
    /// palette entière : il doit passer le filtre même sans section nommée.</summary>
    [Fact]
    public void IsThemeSettingChange_ContrasteEleve_PasseParWParam()
    {
        Assert.True(Theme.IsThemeSettingChange((IntPtr)Win32.SPI_SETHIGHCONTRAST, IntPtr.Zero));
    }

    // ═══════════════════════════════════════════════════════════════
    // Caches GDI
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Les caches rendent le même handle pour la même clé — c'est ce qui permet de reposer une
    /// brosse de fond de classe sans en fabriquer une nouvelle à chaque bascule de thème, et ce
    /// qui interdit à une fenêtre de détruire ce qu'elle reçoit.
    /// </summary>
    [Fact]
    public void CacheDeBrosses_RendLeMemeHandlePourLaMemeCouleur()
    {
        IntPtr a = Theme.Brush(Theme.LightPalette.Paper);
        IntPtr b = Theme.Brush(Theme.LightPalette.Paper);
        IntPtr autre = Theme.Brush(Theme.DarkPalette.Paper);

        Assert.NotEqual(IntPtr.Zero, a);
        Assert.Equal(a, b);
        Assert.NotEqual(a, autre);
    }

    [Fact]
    public void CacheDeStylos_DistingueLaLargeur()
    {
        IntPtr filet = Theme.Pen(Theme.LightPalette.Border);
        IntPtr anneau = Theme.Pen(Theme.LightPalette.Action, 2);

        Assert.NotEqual(IntPtr.Zero, filet);
        Assert.Equal(filet, Theme.Pen(Theme.LightPalette.Border, 1));
        Assert.NotEqual(anneau, Theme.Pen(Theme.LightPalette.Action, 1));
    }

    /// <summary>
    /// Le DPI entre dans la clé : l'application est PerMonitorV2, et deux fenêtres peuvent
    /// vivre au même instant sur deux écrans d'échelles différentes.
    /// </summary>
    [Fact]
    public void CacheDePolices_UneEntreeParRoleEtParEchelle()
    {
        IntPtr corps96 = Theme.Font(FontRole.Body, 96);
        IntPtr corps120 = Theme.Font(FontRole.Body, 120);
        IntPtr titre96 = Theme.Font(FontRole.SectionTitle, 96);

        Assert.NotEqual(IntPtr.Zero, corps96);
        Assert.Equal(corps96, Theme.Font(FontRole.Body, 96));
        Assert.NotEqual(corps96, corps120);
        Assert.NotEqual(corps96, titre96);
    }

    [Fact]
    public void CacheDePolices_UnDpiAbsurdeRetombeSur96()
    {
        Assert.Equal(Theme.Font(FontRole.Body, 96), Theme.Font(FontRole.Body, 0));
        Assert.Equal(Theme.Font(FontRole.Body, 96), Theme.Font(FontRole.Body, -1));
    }

    // ═══════════════════════════════════════════════════════════════
    // Échelle typographique
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Tailles candidates de la charte, qui se figent au chantier CH1 sur les deux fenêtres
    /// témoins. Le test existe pour que le jour où elles bougent, ce soit une décision et pas
    /// une dérive.
    /// </summary>
    [Fact]
    public void EchelleTypographique_EstCelleDeLaCharte()
    {
        var charte = new (FontRole Role, int Size, int Weight, string Face)[]
        {
            (FontRole.Body, 15, 400, "Segoe UI"),
            (FontRole.Secondary, 13, 400, "Segoe UI"),
            (FontRole.SectionTitle, 18, 600, "Segoe UI"),
            (FontRole.WindowTitle, 24, 600, "Segoe UI"),
            (FontRole.StatNumber, 28, 600, "Segoe UI"),
            (FontRole.Mono, 14, 400, "Consolas"),
        };

        // Les six rôles de l'énumération sont couverts : un rôle ajouté sans taille rend ce
        // test rouge, plutôt que de tomber en silence sur le repli de Metrics.
        Assert.Equal(Enum.GetValues<FontRole>().Length, charte.Length);

        foreach (var (role, size, weight, face) in charte)
        {
            var metrics = Theme.Metrics(role);
            Assert.True(size == metrics.Size && weight == metrics.Weight && face == metrics.Face,
                $"Rôle {role} : la charte dit {size}/{weight}/{face}, le socle rend " +
                $"{metrics.Size}/{metrics.Weight}/{metrics.Face}.");
        }
    }

    /// <summary>Aucune police n'est embarquée : la serif éditoriale reste au site, et l'app se
    /// contente des deux replis système officiels des fondations.</summary>
    [Fact]
    public void AucuneAutrePoliceQueSegoeUiEtConsolas()
    {
        var polices = Enum.GetValues<FontRole>()
            .Select(r => Theme.Metrics(r).Face)
            .Distinct()
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "Consolas", "Segoe UI" }, polices);
    }
}
