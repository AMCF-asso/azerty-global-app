using System.Runtime.InteropServices;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Témoins du clavier du tutoriel, dessiné depuis le lot 9 par <see cref="KeyboardRenderer"/>
/// (profil Onboarding) au lieu de sa copie du rendu (audit du 25/09, L-01). Le clavier est
/// peint dans un bitmap en mémoire, sans fenêtre, et chaque témoin lit un pixel qui dit une
/// règle du tutoriel : genre de surlignage, pastille, Verr. Maj. gardée, modificateur à
/// presser et déjà tenu, Retour arrière terne, touche enfoncée. Les mêmes règles, profil
/// Leçons, gardent le rendu des Leçons.
/// </summary>
public class TutorielClavierTests : IDisposable
{
    // Couleurs COLORREF de KeyboardRenderer.
    private const uint Bordure = DarkTheme.Border;
    private const uint FondContexte = DarkTheme.KeyContext;
    private const uint FondTerne = DarkTheme.KeyDisabled;
    private const uint ModificateurActif = DarkTheme.KeyPressed;
    private const uint DirectContour = DarkTheme.HighlightDirect;
    private const uint DirectFond = DarkTheme.HighlightDirectFill;
    private const uint Etape1Contour = DarkTheme.HighlightStep1;
    private const uint Etape2Contour = DarkTheme.HighlightStep2;

    private const uint SC_Q = 0x10; // touche A de l'AZERTY
    private const uint SC_RETOUR = 0x0E;

    private static readonly KeyboardPlacement Place = new(0, 0, 58f);
    private readonly Layout _layout = LayoutLoader.LoadFromResource();

    [DllImport("gdi32.dll")]
    private static extern uint GetPixel(IntPtr hdc, int x, int y);

    public TutorielClavierTests() => L.Language = "fr";
    public void Dispose() => L.Language = "fr";

    /// <summary>Peint le clavier et rend la couleur lue par <paramref name="lire"/>, à qui l'on
    /// donne le cadre des touches.</summary>
    private uint Peindre(KeyboardRenderProfile profil, KeyboardRenderState etat,
        Func<Func<string, uint?, Win32.RECT>, (int X, int Y)> lire)
    {
        var cadres = KeyboardRenderer.BuildHitTestRects(Place).ToList();
        Win32.RECT Cadre(string libelle, uint? scancode) => cadres
            .First(k => scancode is uint sc ? k.Scancode == sc : k.Label == libelle).Rect;
        var (x, y) = lire(Cadre);

        IntPtr ecran = Win32.GetDC(IntPtr.Zero);
        IntPtr dc = Win32.CreateCompatibleDC(ecran);
        IntPtr bitmap = Win32.CreateCompatibleBitmap(ecran, 1000, 360);
        Win32.ReleaseDC(IntPtr.Zero, ecran);
        IntPtr ancien = Win32.SelectObject(dc, bitmap);
        IntPtr police = Win32.CreateFontW(20, 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 4, 0, "Consolas");
        try
        {
            var fonts = new KeyboardFonts(police, police, police, police, police, police);
            KeyboardRenderer.DrawKeys(dc, Place, _layout, profil, etat, fonts);
            return GetPixel(dc, x, y);
        }
        finally
        {
            Win32.SelectObject(dc, ancien);
            Win32.DeleteObject(police);
            Win32.DeleteObject(bitmap);
            Win32.DeleteDC(dc);
        }
    }

    // Points de lecture : le contour gauche, un coin du fond, la place de la pastille.
    private static (int, int) Contour(Win32.RECT r) => (r.left, (r.top + r.bottom) / 2);
    private static (int, int) Fond(Win32.RECT r) => (r.left + 3, r.bottom - 3);
    private static (int, int) Pastille(Win32.RECT r) => (r.right - 11, r.top + 2);

    [Fact]
    public void L_etape_1_se_dit_en_orange_avec_sa_pastille()
    {
        var etat = new KeyboardRenderState { HighlightKind = KeyHighlight.Step1 };
        etat.HighlightedScancodes.Add(SC_Q);

        Assert.Equal(Etape1Contour, Peindre(KeyboardRenderProfile.Onboarding, etat, c => Contour(c("", SC_Q))));
        Assert.Equal(Etape1Contour, Peindre(KeyboardRenderProfile.Onboarding, etat, c => Pastille(c("", SC_Q))));
        // Les Leçons n'ont qu'un genre, en vert, et pas de pastille.
        Assert.Equal(DirectContour, Peindre(KeyboardRenderProfile.Lesson, etat, c => Contour(c("", SC_Q))));
    }

    [Fact]
    public void Verr_Maj_gardee_reste_directe_et_sans_pastille()
    {
        var etat = new KeyboardRenderState { HighlightKind = KeyHighlight.Step1, KeepCapsLockHighlight = true };
        etat.HighlightedLabels.Add("Verr. Maj.");

        Assert.Equal(DirectContour, Peindre(KeyboardRenderProfile.Onboarding, etat, c => Contour(c("Verr. Maj.", null))));
        Assert.Equal(FondContexte, Peindre(KeyboardRenderProfile.Onboarding, etat, c => Pastille(c("Verr. Maj.", null))));
    }

    [Fact]
    public void Un_modificateur_a_presser_et_deja_tenu_se_remplit()
    {
        var etat = new KeyboardRenderState { AltGr = true };
        etat.HighlightedLabels.Add("AltGr");

        Assert.Equal(DirectFond, Peindre(KeyboardRenderProfile.Onboarding, etat, c => Fond(c("AltGr", null))));
        Assert.Equal(ModificateurActif, Peindre(KeyboardRenderProfile.Lesson, etat, c => Fond(c("AltGr", null))));
    }

    [Fact]
    public void Retour_arriere_reste_terne_tant_qu_il_ne_sert_pas()
    {
        Assert.Equal(FondTerne, Peindre(KeyboardRenderProfile.Onboarding, new KeyboardRenderState(), c => Fond(c("", SC_RETOUR))));

        var etat = new KeyboardRenderState { HighlightKind = KeyHighlight.Step2 };
        etat.HighlightedScancodes.Add(SC_RETOUR);
        Assert.Equal(FondContexte, Peindre(KeyboardRenderProfile.Onboarding, etat, c => Fond(c("", SC_RETOUR))));
        Assert.Equal(Etape2Contour, Peindre(KeyboardRenderProfile.Onboarding, etat, c => Contour(c("", SC_RETOUR))));
        // Les Leçons ne le ternissent pas.
        Assert.Equal(FondContexte, Peindre(KeyboardRenderProfile.Lesson, new KeyboardRenderState(), c => Fond(c("", SC_RETOUR))));
    }

    [Fact]
    public void La_touche_enfoncee_perd_son_contour_dans_le_tutoriel_seulement()
    {
        var etat = new KeyboardRenderState { PressedScancode = SC_Q };
        etat.HighlightedScancodes.Add(SC_Q);

        Assert.Equal(Bordure, Peindre(KeyboardRenderProfile.Onboarding, etat, c => Contour(c("", SC_Q))));
        Assert.Equal(DirectContour, Peindre(KeyboardRenderProfile.Lesson, etat, c => Contour(c("", SC_Q))));
    }

    [Fact]
    public void L_exercice_6_revele_ses_aides_que_le_clavier_simplifie_cache()
    {
        var aides = new HashSet<string>(StringComparer.Ordinal) { "dk:stroke", "¿", "¡" };

        Assert.False(KeyboardRenderer.IsSlotVisible(KeyboardRenderProfile.Onboarding, 0x32, 3, "¿"));
        Assert.True(KeyboardRenderer.IsSlotVisible(KeyboardRenderProfile.Onboarding, 0x32, 3, "¿", aides));
        Assert.False(KeyboardRenderer.IsSlotVisible(KeyboardRenderProfile.Onboarding, 0x08, 2, "dk_stroke"));
        Assert.True(KeyboardRenderer.IsSlotVisible(KeyboardRenderProfile.Onboarding, 0x08, 2, "dk_stroke", aides));
    }

    /// <summary>Largeur de l'encre (pixels autres que le fond de touche) dans le quart bas-droit
    /// de la touche 7, où l'exercice 6 montre la touche morte barre « ◌/ ».</summary>
    private int EncreDuBarre(bool superpose)
    {
        var etat = new KeyboardRenderState();
        etat.LessonVisibleCharacters.Add("dk:stroke");
        var cle = KeyboardRenderer.BuildHitTestRects(Place).First(k => k.Scancode == 0x08).Rect;

        IntPtr ecran = Win32.GetDC(IntPtr.Zero);
        IntPtr dc = Win32.CreateCompatibleDC(ecran);
        IntPtr bitmap = Win32.CreateCompatibleBitmap(ecran, 1000, 360);
        Win32.ReleaseDC(IntPtr.Zero, ecran);
        IntPtr ancien = Win32.SelectObject(dc, bitmap);
        IntPtr police = Win32.CreateFontW(25, 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 4, 0, "Consolas");
        IntPtr segoe = Win32.CreateFontW(25, 0, 0, 0, 600, 0, 0, 0, 0, 0, 0, 4, 0, "Segoe UI");
        try
        {
            IntPtr couche = superpose ? segoe : IntPtr.Zero;
            var fonts = new KeyboardFonts(police, police, police, police, police, police, couche, couche);
            KeyboardRenderer.DrawKeys(dc, Place, _layout, KeyboardRenderProfile.Onboarding, etat, fonts);
            int gauche = int.MaxValue, droite = -1;
            for (int x = (cle.left + cle.right) / 2; x < cle.right - 1; x++)
                for (int y = (cle.top + cle.bottom) / 2; y < cle.bottom - 1; y++)
                    if (GetPixel(dc, x, y) != DarkTheme.Key)
                    {
                        gauche = Math.Min(gauche, x);
                        droite = Math.Max(droite, x);
                    }
            return droite - gauche + 1;
        }
        finally
        {
            Win32.SelectObject(dc, ancien);
            Win32.DeleteObject(police);
            Win32.DeleteObject(segoe);
            Win32.DeleteObject(bitmap);
            Win32.DeleteDC(dc);
        }
    }

    [Fact]
    public void Le_cercle_et_la_barre_se_superposent_dans_le_tutoriel()
    {
        // Décision d'Antoine du 26/09 : le ◌ barré d'avant le lot 9, pas « ◌/ » côte à côte.
        Assert.True(KeyboardRenderer.IsOverlaidDottedCircle("◌/"));
        Assert.False(KeyboardRenderer.IsOverlaidDottedCircle("◌\u0323")); // accent combinant : collé
        Assert.False(KeyboardRenderer.IsOverlaidDottedCircle("/"));
        int superpose = EncreDuBarre(superpose: true);
        int coteACote = EncreDuBarre(superpose: false);
        Assert.True(superpose > 0 && superpose < coteACote, $"superposé {superpose} px, côte à côte {coteACote} px");
    }

    [Fact]
    public void Les_infobulles_du_tutoriel_gardent_leurs_textes()
    {
        var etat = new KeyboardRenderState { ShowInvisibleMarkers = false };

        Assert.Equal(L.Learning_TooltipBackspaceDisabled,
            KeyboardRenderer.BuildTooltipText(_layout, KeyboardRenderProfile.Onboarding, etat, SC_RETOUR, "⌫"));
        Assert.Equal(L.Keyboard_TooltipBackspace,
            KeyboardRenderer.BuildTooltipText(_layout, KeyboardRenderProfile.Lesson, etat, SC_RETOUR, "⌫"));

        // Touche C11 : accent aigu en Base, grave en Maj.
        string tutoriel = KeyboardRenderer.BuildTooltipText(_layout, KeyboardRenderProfile.Onboarding, etat, 0x28, "");
        Assert.Contains("Base : ´ — touche morte ACCENT AIGU", tutoriel);
        string lecons = KeyboardRenderer.BuildTooltipText(_layout, KeyboardRenderProfile.Lesson, etat, 0x28, "");
        Assert.Contains("Base : ´ — touche morte ACCENT AIGU", lecons);

        // Touche E03, AltGr : le point souscrit. Le tutoriel montre l'accent seul de la
        // disposition, les Leçons le symbole du tray.
        Assert.Contains("AltGr : ◌\u0323 — touche morte POINT SOUSCRIT",
            KeyboardRenderer.BuildTooltipText(_layout, KeyboardRenderProfile.Onboarding, etat, 0x04, ""));
        Assert.Contains("AltGr : . — touche morte POINT SOUSCRIT",
            KeyboardRenderer.BuildTooltipText(_layout, KeyboardRenderProfile.Lesson, etat, 0x04, ""));
    }
}
