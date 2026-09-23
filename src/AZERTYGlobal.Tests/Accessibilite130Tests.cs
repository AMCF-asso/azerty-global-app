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
}
