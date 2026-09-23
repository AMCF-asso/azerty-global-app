using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Lot d'accessibilité 1.3.0 (K1 à K6, N8, N10, D1, D3) — les décisions pures du lot.
/// Source : <c>docs/audit-2026-09-22-v1.3.0/feu-vert/accessibilite-1.3.0.md</c>.
///
/// Ce qui n'est pas éprouvé ici, et reste à la recette : que Windows envoie les messages
/// attendus, que le focus se voie à l'écran et que le Narrateur lise les noms posés.
/// </summary>
public class Accessibilite130Tests
{
    // ── K3 : cycle de tabulation des exercices ─────────────────────────
    // Arrêts du module d'exercices : 0 = surface de frappe, 1 = « Quitter », 2 = « Passer ».

    [Fact]
    public void K3_Tab_VaDeLaSurfaceAuxBoutonsPuisRevient()
    {
        Assert.Equal(1, DialogNavigation.NextFocusStop(0, 3, backwards: false));
        Assert.Equal(2, DialogNavigation.NextFocusStop(1, 3, backwards: false));
        // ⛔ Le retour à la surface : sans lui, le focus resterait sur « Passer » et plus
        // aucune frappe n'arriverait à l'exercice.
        Assert.Equal(0, DialogNavigation.NextFocusStop(2, 3, backwards: false));
    }

    [Fact]
    public void K3_MajTab_ParcourtLeCycleALEnvers()
    {
        Assert.Equal(2, DialogNavigation.NextFocusStop(0, 3, backwards: true));
        Assert.Equal(1, DialogNavigation.NextFocusStop(2, 3, backwards: true));
        Assert.Equal(0, DialogNavigation.NextFocusStop(1, 3, backwards: true));
    }

    [Fact]
    public void K3_DépartHorsListe_EntreParLePremierOuLeDernierArrêt()
    {
        Assert.Equal(0, DialogNavigation.NextFocusStop(-1, 3, backwards: false));
        Assert.Equal(2, DialogNavigation.NextFocusStop(-1, 3, backwards: true));
        Assert.Equal(0, DialogNavigation.NextFocusStop(7, 3, backwards: false));
    }

    [Fact]
    public void K3_ExerciceNonPassable_CycleÀDeuxArrêts()
    {
        // « Passer » masqué : surface et « Quitter » seulement.
        Assert.Equal(1, DialogNavigation.NextFocusStop(0, 2, backwards: false));
        Assert.Equal(0, DialogNavigation.NextFocusStop(1, 2, backwards: false));
        Assert.Equal(1, DialogNavigation.NextFocusStop(0, 2, backwards: true));
    }

    [Fact]
    public void K3_ListeVide_NeDésigneAucunArrêt()
    {
        Assert.Equal(-1, DialogNavigation.NextFocusStop(-1, 0, backwards: false));
        Assert.Equal(-1, DialogNavigation.NextFocusStop(0, 0, backwards: true));
        // Réciproque : un seul arrêt reste désigné, dans les deux sens.
        Assert.Equal(0, DialogNavigation.NextFocusStop(0, 1, backwards: false));
        Assert.Equal(0, DialogNavigation.NextFocusStop(0, 1, backwards: true));
    }

    // ── K4 : infobulle de la cible focalisée (Leçons) ──────────────────

    private static Win32.RECT R(int left, int top, int right, int bottom) =>
        new() { left = left, top = top, right = right, bottom = bottom };

    private static readonly List<(Win32.RECT, string, bool, bool)> Icônes = new()
    {
        (R(0, 0, 30, 30), "Indice", true, true),
        (R(40, 0, 70, 30), "Réglages", true, true),
    };

    [Fact]
    public void K4_BoutonIcône_RetrouveSonInfobulle()
    {
        Assert.Equal(1, LessonsWindow.FindHoverAreaFor(R(40, 0, 70, 30), Icônes));
        Assert.Equal(0, LessonsWindow.FindHoverAreaFor(R(0, 0, 30, 30), Icônes));
    }

    [Fact]
    public void K4_ChaqueBordDuRectangleCompte()
    {
        // Réciproque, un bord à la fois : une cible voisine sans infobulle (bouton à texte)
        // ne doit pas emprunter celle d'une icône qui lui ressemble.
        Assert.Equal(-1, LessonsWindow.FindHoverAreaFor(R(41, 0, 70, 30), Icônes));
        Assert.Equal(-1, LessonsWindow.FindHoverAreaFor(R(40, 1, 70, 30), Icônes));
        Assert.Equal(-1, LessonsWindow.FindHoverAreaFor(R(40, 0, 71, 30), Icônes));
        Assert.Equal(-1, LessonsWindow.FindHoverAreaFor(R(40, 0, 70, 31), Icônes));
    }

    [Fact]
    public void K4_InfobulleVide_NeCompteRien_EtLaPremièreGagne()
    {
        var zones = new List<(Win32.RECT, string, bool, bool)>
        {
            (R(0, 0, 30, 30), "", true, true),
            (R(0, 0, 30, 30), "Indice", true, true),
            (R(0, 0, 30, 30), "Doublon", false, false),
        };
        Assert.Equal(1, LessonsWindow.FindHoverAreaFor(R(0, 0, 30, 30), zones));
        Assert.Equal(-1, LessonsWindow.FindHoverAreaFor(R(0, 0, 30, 30), new List<(Win32.RECT, string, bool, bool)>()));
    }

    // ── K5 : cadre de focus des liens STATIC ───────────────────────────

    [Fact]
    public void K5_LeCadreEntoureLeTexte_PasToutLeContrôle()
    {
        // Un lien de l'accueil mesure 240 px de large pour un texte bien plus court : un
        // cadre sur tout le contrôle entourerait du vide.
        var cadre = GdiHelpers.FocusRectForLink(R(0, 0, 80, 20), R(0, 0, 240, 28));
        Assert.Equal((0, 0, 82, 21), (cadre.left, cadre.top, cadre.right, cadre.bottom));
    }

    [Fact]
    public void K5_LeCadreResteDansLeContrôle()
    {
        // DT_CALCRECT élargit le rectangle quand le mot le plus long déborde : le cadre ne
        // doit pas sortir du contrôle, ni à droite ni en bas.
        var cadre = GdiHelpers.FocusRectForLink(R(0, 0, 300, 40), R(0, 0, 240, 28));
        Assert.Equal(240, cadre.right);
        Assert.Equal(28, cadre.bottom);
        var serré = GdiHelpers.FocusRectForLink(R(0, 0, 239, 28), R(0, 0, 240, 28));
        Assert.Equal(240, serré.right);
        Assert.Equal(28, serré.bottom);
    }

    [Fact]
    public void K5_TexteSansMesure_CadreSurToutLeContrôle()
    {
        var client = R(0, 0, 240, 28);
        Assert.Equal(client, GdiHelpers.FocusRectForLink(R(0, 0, 0, 0), client));
        Assert.Equal(client, GdiHelpers.FocusRectForLink(R(0, 0, 80, 0), client));
    }

    // ── K6 : nom accessible du drapeau de langue ───────────────────────

    [Fact]
    public void K6_LeDrapeauPorteLEndonymeDeLaLangueCible()
    {
        string avant = L.Language;
        try
        {
            // Interface française : le drapeau mène à l'anglais, et le dit en anglais.
            L.Language = "fr";
            Assert.Equal("English", OnboardingWindow.FlagButtonName);
            // Réciproque : le nom suit la langue, il n'est pas figé à la création.
            L.Language = "en";
            Assert.Equal("Français", OnboardingWindow.FlagButtonName);
        }
        finally
        {
            L.Language = avant;
        }
    }
}
