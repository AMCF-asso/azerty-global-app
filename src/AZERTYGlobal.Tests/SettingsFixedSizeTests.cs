using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Taille fixe de la fenêtre Paramètres (décision d'Antoine du 2026-09-24) : la zone cliente
/// prend la hauteur de l'onglet « Général » pour tous les onglets, plafonnée à la zone de
/// travail, et la barre de défilement est réservée dès qu'un onglet dépasse, pour que la
/// largeur ne varie pas non plus.
/// </summary>
public class SettingsFixedSizeTests
{
    [Fact]
    public void La_hauteur_ne_depend_que_de_l_onglet_de_reference()
    {
        // Hauteurs mesurées à 100 % : Général 353 (message compris), Applications 285,
        // Langue et maintenance 276.
        var a = SettingsScrollState.FixedViewport(353, 700, 353, 285, 276);
        var b = SettingsScrollState.FixedViewport(353, 700, 276, 353, 285);
        Assert.Equal(353, a.Viewport);
        Assert.Equal(a, b);
        Assert.False(a.ReserveScrollBar);
    }

    [Fact]
    public void Un_onglet_plus_haut_que_la_reference_reserve_la_barre()
    {
        var result = SettingsScrollState.FixedViewport(353, 700, 353, 400, 276);
        Assert.Equal(353, result.Viewport);
        Assert.True(result.ReserveScrollBar);
    }

    [Fact]
    public void La_zone_de_travail_plafonne_et_arme_le_defilement()
    {
        // 200 % sur 1366×768 : Général 710 px pour environ 668 px de client disponible.
        var result = SettingsScrollState.FixedViewport(710, 668, 710, 570, 554);
        Assert.Equal(668, result.Viewport);
        Assert.True(result.ReserveScrollBar);
    }

    [Fact]
    public void Une_zone_nulle_ne_donne_jamais_un_viewport_vide()
    {
        Assert.Equal(1, SettingsScrollState.FixedViewport(0, 0).Viewport);
    }
}
