using System;
using System.Runtime.InteropServices;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Habillage de fenêtre — chantier CH0 de la refonte graphique v1.2.0.
///
/// Poser un chrome ou une icône demande une fenêtre réelle, et c'est l'arrêt visuel d'Antoine
/// au chantier CH1 qui les tranche. Ce qui s'éprouve ici est ce qui se calcule avant l'appel
/// système : les couleurs de barre de titre, le décodage d'un WM_DPICHANGED, la taille du
/// masque d'icône, et le fait qu'aucune de ces fonctions ne lève sur une fenêtre absente.
/// </summary>
public class ThemeWindowTests
{
    // ═══════════════════════════════════════════════════════════════
    // Couleurs de chrome
    // ═══════════════════════════════════════════════════════════════

    /// <summary>La barre de titre porte le fond de fenêtre et son texte l'encre : la charte ne
    /// connaît pas de couleur de chrome propre, et en inventer une ferait de la barre la seule
    /// surface de l'application hors palette.</summary>
    [Fact]
    public void LaBarreDeTitre_PorteLeFondEtLEncreDuTheme()
    {
        foreach (var palette in new[] { Theme.LightPalette, Theme.DarkPalette })
        {
            var (caption, text) = ThemeWindow.CaptionColors(palette, highContrast: false);

            Assert.Equal(palette.Paper, caption);
            Assert.Equal(palette.Ink, text);
        }
    }

    /// <summary>En Contraste élevé, le chrome revient au système comme le reste de la
    /// palette : aucun jeton du produit ne s'applique.</summary>
    [Fact]
    public void ContrasteEleve_RendLaBarreDeTitreAuSysteme()
    {
        foreach (var palette in new[] { Theme.LightPalette, Theme.DarkPalette })
        {
            var (caption, text) = ThemeWindow.CaptionColors(palette, highContrast: true);

            Assert.Equal(Win32.DWMWA_COLOR_DEFAULT, caption);
            Assert.Equal(Win32.DWMWA_COLOR_DEFAULT, text);
        }
    }

    /// <summary>Le poste de développement est sous Windows 11, mais le test ne l'exige pas :
    /// il vérifie seulement que la détection ne se contredit pas, un poste Windows 10 devant
    /// pouvoir exécuter la suite sans la faire rougir.</summary>
    [Fact]
    public void LaDetectionDeWindows11_SuitLeNumeroDeBuild()
    {
        bool attendu = Environment.OSVersion.Version.Build >= 22000;

        Assert.Equal(attendu, ThemeWindow.SupportsCaptionColor);
    }

    // ═══════════════════════════════════════════════════════════════
    // DPI
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ApplyDpiChange_LitLEchelleDansLeMotDeFortPoidsDeWParam()
    {
        Assert.Equal(120, ThemeWindow.ApplyDpiChange(IntPtr.Zero, (IntPtr)(120 << 16), IntPtr.Zero));
        Assert.Equal(144, ThemeWindow.ApplyDpiChange(IntPtr.Zero, (IntPtr)(144 << 16), IntPtr.Zero));
        Assert.Equal(96, ThemeWindow.ApplyDpiChange(IntPtr.Zero, (IntPtr)(96 << 16), IntPtr.Zero));
    }

    /// <summary>wParam porte le DPI dans ses deux mots, X et Y. Lire le mot de faible poids
    /// rendrait la même valeur sur tout écran carré et une valeur fausse ailleurs : le test
    /// pose deux mots différents pour que la confusion se voie.</summary>
    [Fact]
    public void ApplyDpiChange_NeLitPasLeMotDeFaiblePoids()
    {
        IntPtr wParam = (IntPtr)((144 << 16) | 96);

        Assert.Equal(144, ThemeWindow.ApplyDpiChange(IntPtr.Zero, wParam, IntPtr.Zero));
    }

    [Fact]
    public void ApplyDpiChange_UnWParamVide_RetombeSurLEchelleDeLaFenetre()
    {
        Assert.Equal(96, ThemeWindow.ApplyDpiChange(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero));
    }

    [Fact]
    public void DpiOf_SansFenetre_Rend96()
    {
        Assert.Equal(96, ThemeWindow.DpiOf(IntPtr.Zero));
    }

    // ═══════════════════════════════════════════════════════════════
    // Masque d'icône
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Les lignes d'un bitmap à un bit par pixel sont alignées sur un mot de deux octets. La
    /// formule naïve largeur × hauteur / 8 tombe juste tant que la largeur est un multiple de
    /// 16 — 16, 32, 48 — et se met à mentir dès qu'elle ne l'est plus. Or la grande icône d'un
    /// poste à 125 % fait 40 px : GDI y lit 240 octets là où la formule naïve en fournit 200,
    /// donc 40 octets au-delà du tableau.
    /// </summary>
    [Fact]
    public void MaskByteCount_SuitLAlignementSurUnMot()
    {
        Assert.Equal(32, ThemeWindow.MaskByteCount(16));
        Assert.Equal(128, ThemeWindow.MaskByteCount(32));
        Assert.Equal(240, ThemeWindow.MaskByteCount(40));   // 200 avec la formule naïve
        Assert.Equal(96, ThemeWindow.MaskByteCount(24));    // 72 avec la formule naïve
        // 48 bits font 6 octets pleins : la ligne est deja alignee, pas de remplissage.
        Assert.Equal(288, ThemeWindow.MaskByteCount(48));
    }

    /// <summary>
    /// L'invariant, plutôt que les cinq tailles connues : chaque ligne tient au moins les bits
    /// de sa largeur, et fait un nombre pair d'octets. Une taille non ronde née d'une échelle
    /// d'affichage inhabituelle passe alors le même garde.
    /// </summary>
    [Fact]
    public void MaskByteCount_TientTouteTaille()
    {
        for (int size = 1; size <= 256; size++)
        {
            int total = ThemeWindow.MaskByteCount(size);
            int stride = total / size;

            Assert.True(total % size == 0, $"taille {size} : {total} octets ne se divise pas en lignes");
            Assert.True(stride * 8 >= size, $"taille {size} : {stride} octets par ligne, insuffisant");
            Assert.True(stride % 2 == 0, $"taille {size} : {stride} octets par ligne, non aligné sur un mot");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Absence de fenêtre
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Toutes ces fonctions sont appelées juste après CreateWindowExW, qui peut échouer. Aucune
    /// ne doit lever sur un handle nul : une fenêtre qui n'a pas pu naître ferait sinon tomber
    /// le constructeur de sa propriétaire, et le tray avec.
    /// </summary>
    [Fact]
    public void SansFenetre_AucuneFonctionNeLeve()
    {
        ThemeWindow.ApplyChrome(IntPtr.Zero);
        ThemeWindow.ApplyChrome(IntPtr.Zero, Theme.DarkPalette, ThemeVariant.Dark, highContrast: false);
        ThemeWindow.ApplyClassBackground(IntPtr.Zero, Theme.LightPalette.Paper);
        ThemeWindow.ApplyProductIcon(IntPtr.Zero);

        Assert.Null(ThemeWindow.ReadImmersiveDarkMode(IntPtr.Zero));
    }

    /// <summary>L'ancien point d'entrée des dix fenêtres délègue désormais au helper, sans plus
    /// forcer le mode sombre quel que soit le thème de Windows.</summary>
    [Fact]
    public void EnableDarkTitleBar_SansFenetre_NeLevePas()
    {
        Win32.EnableDarkTitleBar(IntPtr.Zero);
    }

    // ═══════════════════════════════════════════════════════════════
    // Fond de classe
    // ═══════════════════════════════════════════════════════════════

    private const uint OBJ_BRUSH = 2;

    [DllImport("gdi32.dll")]
    private static extern uint GetObjectType(IntPtr h);

    /// <summary>Garde le délégué en vie aussi longtemps que la classe de fenêtre du témoin.</summary>
    private static readonly Win32.WNDPROC TestWndProc = Win32.DefWindowProcW;

    /// <summary>
    /// Une brosse posée en fond de classe appartient au système, qui la détruit au
    /// désenregistrement de la classe. Le socle la prenait dans le cache de <see cref="Theme"/> :
    /// la fenêtre suivante recevait alors un handle mort, son fond restait blanc et ses étiquettes
    /// grises — mesuré sur Durée de pause le 2026-08-29, où GetObjectType tombe de 2 à 0. Le cache
    /// doit survivre au cycle complet d'une fenêtre.
    /// </summary>
    [Fact]
    public void FondDeClasse_NeConsommePasLaBrosseDuCacheDeTheme()
    {
        uint couleur = Theme.LightPalette.Paper;
        IntPtr brosse = Theme.Brush(couleur);
        Assert.Equal(OBJ_BRUSH, GetObjectType(brosse));

        const string classe = "AZERTYGlobal.Tests.FondDeClasse";
        IntPtr hInstance = Win32.GetModuleHandleW(null);
        var wc = new Win32.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEXW>(),
            lpfnWndProc = TestWndProc,
            hInstance = hInstance,
            lpszClassName = classe,
        };
        Win32.RegisterClassExW(ref wc);

        IntPtr hwnd = Win32.CreateWindowExW(0, classe, string.Empty, Win32.WS_OVERLAPPED,
            0, 0, 10, 10, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        Assert.NotEqual(IntPtr.Zero, hwnd);

        try
        {
            ThemeWindow.ApplyClassBackground(hwnd, couleur);
            ThemeWindow.ForgetClassBackground(hwnd);
        }
        finally
        {
            Win32.DestroyWindow(hwnd);
            Win32.UnregisterClassW(classe, hInstance);
        }

        Assert.Equal(OBJ_BRUSH, GetObjectType(brosse));
        Assert.Equal(brosse, Theme.Brush(couleur));
    }
}
