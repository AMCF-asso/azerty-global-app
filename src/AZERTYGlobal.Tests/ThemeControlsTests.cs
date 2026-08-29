using System;
using System.Collections.Generic;
using System.Linq;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Primitives de contrôle owner-draw — chantier CH0 de la refonte graphique v1.2.0.
///
/// Les tables d'états sont des fonctions pures, et c'est ce qui les rend éprouvables sans
/// fenêtre ni DC : la suite parcourt les trente-deux combinaisons de drapeaux, sur les deux
/// thèmes, et prouve qu'aucune ne fait sortir une couleur de la palette ni ne descend sous les
/// seuils de la charte. Un contrôle mal peint reste un contrôle valide : ni le compilateur ni
/// la relecture visuelle n'attrapent ces deux fautes-là.
///
/// Le rendu GDI lui-même n'est pas testé ici — il demande un DC et une fenêtre, et c'est
/// l'arrêt visuel d'Antoine au chantier CH1 qui le tranche, sur deux thèmes et trois échelles.
/// </summary>
public class ThemeControlsTests
{
    /// <summary>Les trente-deux combinaisons des cinq drapeaux, y compris les absurdes : une
    /// combinaison que l'application ne produit jamais aujourd'hui peut naître d'un refactor,
    /// et la table doit rendre quelque chose de lisible dans tous les cas.</summary>
    private static IEnumerable<ControlState> ToutesLesCombinaisons() =>
        Enumerable.Range(0, 32).Select(i => (ControlState)i);

    private static IEnumerable<Palette> LesDeuxThemes() =>
        new[] { Theme.LightPalette, Theme.DarkPalette };

    private static HashSet<uint> CouleursDe(Palette p) => new()
    {
        p.Paper, p.Surface, p.Ink, p.TextSecondary, p.Border, p.Action, p.Success, p.Warning,
        p.Error, p.SuccessFill, p.WarningFill, p.ErrorFill, p.ActionFill,
    };

    // ═══════════════════════════════════════════════════════════════
    // Aucune couleur inventée, quel que soit l'état
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void AucunEtatDeControle_NeProduitUneCouleurHorsPalette()
    {
        foreach (var palette in LesDeuxThemes())
        {
            var connues = CouleursDe(palette);

            foreach (var state in ToutesLesCombinaisons())
            {
                foreach (var kind in Enum.GetValues<ButtonKind>())
                    AsserteDansLaPalette(ThemeControls.ButtonPaint(kind, state, palette),
                        connues, $"bouton {kind} / {state}");

                AsserteDansLaPalette(ThemeControls.BoxPaint(state, palette),
                    connues, $"case / {state}");
                AsserteDansLaPalette(ThemeControls.FieldPaint(state, palette),
                    connues, $"champ / {state}");

                Assert.Contains(ThemeControls.LabelColor(state, palette), connues);
                Assert.Contains(ThemeControls.LinkColor(state, palette), connues);
            }
        }
    }

    private static void AsserteDansLaPalette(ControlPaint paint, HashSet<uint> connues, string cas)
    {
        Assert.True(connues.Contains(paint.Fill), $"{cas} : fond 0x{paint.Fill:X8} hors palette");
        Assert.True(connues.Contains(paint.Border), $"{cas} : bordure 0x{paint.Border:X8} hors palette");
        Assert.True(connues.Contains(paint.Text), $"{cas} : texte 0x{paint.Text:X8} hors palette");
        Assert.True(paint.BorderWidth == 1 || paint.BorderWidth == 2,
            $"{cas} : largeur de trait {paint.BorderWidth}, la charte n'en connaît que 1 et 2");
    }

    // ═══════════════════════════════════════════════════════════════
    // Seuils de lisibilité, état par état
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Le texte d'un contrôle reste au-dessus de 4,5:1 sur son propre fond dans tous les états,
    /// désactivé compris. WCAG dispense les contrôles inactifs, la charte non : un réglage
    /// grisé qu'on ne peut pas lire ne dit pas ce qu'il faudrait faire pour l'activer.
    /// </summary>
    [Fact]
    public void LeTexteDUnControle_ResteLisibleSurSonFond_DansTousLesEtats()
    {
        foreach (var palette in LesDeuxThemes())
        {
            foreach (var state in ToutesLesCombinaisons())
            {
                foreach (var kind in Enum.GetValues<ButtonKind>())
                    AsserteContraste(ThemeControls.ButtonPaint(kind, state, palette),
                        $"bouton {kind} / {state}");

                AsserteContraste(ThemeControls.BoxPaint(state, palette), $"case / {state}");
                AsserteContraste(ThemeControls.FieldPaint(state, palette), $"champ / {state}");

                double lien = Contraste(ThemeControls.LinkColor(state, palette), palette.Paper);
                Assert.True(lien >= 4.5, $"lien / {state} : {lien:F2}:1 sur le papier");

                double libelle = Contraste(ThemeControls.LabelColor(state, palette), palette.Paper);
                Assert.True(libelle >= 4.5, $"libellé / {state} : {libelle:F2}:1 sur le papier");
            }
        }
    }

    private static void AsserteContraste(ControlPaint paint, string cas)
    {
        double ratio = Contraste(paint.Text, paint.Fill);
        Assert.True(ratio >= 4.5, $"{cas} : texte à {ratio:F2}:1 sur son fond");
    }

    /// <summary>
    /// Un contrôle se voit sur le fond de la fenêtre, par son fond ou par sa bordure. Le cas
    /// qui rend ce test nécessaire est le bouton secondaire au repos : son fond est la surface
    /// blanche, à 1,05:1 du papier, et c'est sa seule bordure qui le délimite. Si quelqu'un la
    /// fait passer au jeton décoratif — 1,42:1 — le bouton disparaît sans qu'un seul autre test
    /// ne bouge.
    /// </summary>
    [Fact]
    public void UnControle_SeDetacheDuFondDeLaFenetre_ParSonFondOuParSaBordure()
    {
        foreach (var palette in LesDeuxThemes())
        {
            foreach (var state in ToutesLesCombinaisons())
            {
                foreach (var kind in Enum.GetValues<ButtonKind>())
                    AsserteDelimitation(ThemeControls.ButtonPaint(kind, state, palette), palette,
                        $"bouton {kind} / {state}");

                AsserteDelimitation(ThemeControls.BoxPaint(state, palette), palette, $"case / {state}");
                AsserteDelimitation(ThemeControls.FieldPaint(state, palette), palette, $"champ / {state}");
            }
        }
    }

    private static void AsserteDelimitation(ControlPaint paint, Palette palette, string cas)
    {
        double fond = Contraste(paint.Fill, palette.Paper);
        double bordure = Contraste(paint.Border, palette.Paper);
        Assert.True(fond >= 3.0 || bordure >= 3.0,
            $"{cas} : fond à {fond:F2}:1 et bordure à {bordure:F2}:1 du papier — le contrôle " +
            "n'a plus de contour visible");
    }

    // ═══════════════════════════════════════════════════════════════
    // Précédence des états
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Inactif l'emporte sur tout le reste. Sans cette précédence, un contrôle désactivé
    /// s'allumerait au passage du curseur et promettrait une action qu'il ne rend pas.
    /// </summary>
    [Fact]
    public void Desactive_LEmporteSurTousLesAutresEtats()
    {
        var tout = ControlState.Disabled | ControlState.Hovered | ControlState.Pressed
                   | ControlState.Checked | ControlState.Focused;

        foreach (var palette in LesDeuxThemes())
        {
            foreach (var kind in Enum.GetValues<ButtonKind>())
            {
                Assert.Equal(ThemeControls.ButtonPaint(kind, ControlState.Disabled, palette),
                    ThemeControls.ButtonPaint(kind, tout, palette));
            }

            Assert.Equal(ThemeControls.BoxPaint(ControlState.Disabled, palette),
                ThemeControls.BoxPaint(tout, palette));
            Assert.Equal(ThemeControls.FieldPaint(ControlState.Disabled, palette),
                ThemeControls.FieldPaint(tout, palette));

            Assert.Equal(palette.Disabled, ThemeControls.LabelColor(tout, palette));
            Assert.Equal(palette.Disabled, ThemeControls.LinkColor(tout, palette));
        }
    }

    /// <summary>Le focus ajoute l'anneau et ne touche à rien d'autre : c'est ce qui permet de
    /// le dessiner par-dessus n'importe quel contrôle sans table supplémentaire.</summary>
    [Fact]
    public void LeFocus_NeChangeAucuneCouleurDuControle()
    {
        foreach (var palette in LesDeuxThemes())
        {
            foreach (var state in ToutesLesCombinaisons())
            {
                var sans = state & ~ControlState.Focused;
                var avec = state | ControlState.Focused;

                foreach (var kind in Enum.GetValues<ButtonKind>())
                {
                    Assert.Equal(ThemeControls.ButtonPaint(kind, sans, palette),
                        ThemeControls.ButtonPaint(kind, avec, palette));
                }

                Assert.Equal(ThemeControls.BoxPaint(sans, palette),
                    ThemeControls.BoxPaint(avec, palette));
            }
        }
    }

    /// <summary>
    /// Un bouton enfoncé ne redevient pas « survolé » parce que le curseur ne bouge plus.
    /// </summary>
    [Fact]
    public void Enfonce_LEmporteSurLeSurvol()
    {
        foreach (var palette in LesDeuxThemes())
        {
            foreach (var kind in Enum.GetValues<ButtonKind>())
            {
                Assert.Equal(ThemeControls.ButtonPaint(kind, ControlState.Pressed, palette),
                    ThemeControls.ButtonPaint(kind,
                        ControlState.Pressed | ControlState.Hovered, palette));
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Ce que chaque état dit
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Le survol d'un lien ne change pas sa couleur, il ajoute le soulignement. Demander une
    /// seconde nuance d'accent pour le survol est exactement ce qui a produit l'orange fantôme
    /// de l'application actuelle : le bleu Windows recopié à octets inversés servait de couleur
    /// de survol des liens d'À propos.
    /// </summary>
    [Fact]
    public void UnLienSurvole_GardeLaCouleurDeLAccent()
    {
        foreach (var palette in LesDeuxThemes())
        {
            Assert.Equal(palette.Action, ThemeControls.LinkColor(ControlState.None, palette));
            Assert.Equal(palette.Action, ThemeControls.LinkColor(ControlState.Hovered, palette));
            Assert.Equal(palette.Action, ThemeControls.LinkColor(ControlState.Pressed, palette));
            Assert.Equal(palette.Disabled, ThemeControls.LinkColor(ControlState.Disabled, palette));
        }
    }

    /// <summary>
    /// Une case cochée se remplit de l'accent, et sa coche prend la couleur du texte sur accent
    /// — la même que celle d'un bouton primaire, pour que les deux se lisent comme une seule
    /// famille.
    /// </summary>
    [Fact]
    public void UneCaseCochee_SeLitCommeUnBoutonPrimaire()
    {
        foreach (var palette in LesDeuxThemes())
        {
            var cochee = ThemeControls.BoxPaint(ControlState.Checked, palette);
            var primaire = ThemeControls.ButtonPaint(ButtonKind.Primary, ControlState.None, palette);

            Assert.Equal(primaire.Fill, cochee.Fill);
            Assert.Equal(primaire.Text, cochee.Text);
        }
    }

    /// <summary>
    /// Une case au repos et une case cochée ne se distinguent pas par leur seule bordure : la
    /// charte interdit qu'une bordure porte un état à elle seule, et le fond change bien ici.
    /// </summary>
    [Fact]
    public void CocheeEtNonCochee_SeDistinguentParLeFond()
    {
        foreach (var palette in LesDeuxThemes())
        {
            var repos = ThemeControls.BoxPaint(ControlState.None, palette);
            var cochee = ThemeControls.BoxPaint(ControlState.Checked, palette);

            Assert.NotEqual(repos.Fill, cochee.Fill);
            Assert.True(Contraste(repos.Fill, cochee.Fill) >= 3.0,
                "les deux fonds d'une case doivent se distinguer d'au moins 3:1");
        }
    }

    /// <summary>Le champ de saisie ne signale pas son focus par la seule épaisseur du trait :
    /// sa couleur passe aussi à l'accent, ce qui reste perceptible sans distinguer deux
    /// épaisseurs voisines.</summary>
    [Fact]
    public void UnChampFocalise_ChangeDeCouleurDeTrait_PasSeulementDEpaisseur()
    {
        foreach (var palette in LesDeuxThemes())
        {
            var repos = ThemeControls.FieldPaint(ControlState.None, palette);
            var focus = ThemeControls.FieldPaint(ControlState.Focused, palette);

            Assert.NotEqual(repos.Border, focus.Border);
            Assert.Equal(palette.Action, focus.Border);
            Assert.True(focus.BorderWidth > repos.BorderWidth);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Géométrie
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Scale_SuitLEchelleDeLEcran()
    {
        Assert.Equal(3, ThemeControls.Scale(3, 96));
        Assert.Equal(4, ThemeControls.Scale(3, 120));   // 3,75 arrondi au plus loin de zéro
        Assert.Equal(3, ThemeControls.Scale(2, 120));   // 2,5 idem
        Assert.Equal(20, ThemeControls.Scale(16, 120));
        Assert.Equal(24, ThemeControls.Scale(16, 144));
    }

    /// <summary>
    /// Une largeur de trait ne tombe jamais à zéro : un trait arrondi à 0 disparaît, et une
    /// bordure absente est le seul cas où l'échelle d'affichage changerait ce que le contrôle
    /// veut dire. Zéro reste zéro, lui, parce qu'un écart nul est une intention.
    /// </summary>
    [Fact]
    public void Scale_NeFaitJamaisDisparaitreUnTrait()
    {
        Assert.Equal(1, ThemeControls.Scale(1, 96));
        Assert.Equal(1, ThemeControls.Scale(1, 48));
        Assert.Equal(1, ThemeControls.Scale(1, 1));
        Assert.Equal(0, ThemeControls.Scale(0, 240));
    }

    [Fact]
    public void Scale_UnDpiAbsurdeRetombeSur96()
    {
        Assert.Equal(ThemeControls.Scale(16, 96), ThemeControls.Scale(16, 0));
        Assert.Equal(ThemeControls.Scale(16, 96), ThemeControls.Scale(16, -1));
    }

    /// <summary>L'anneau déborde du contrôle : la mise en page doit réserver cette marge,
    /// sinon il mord sur le voisin.</summary>
    [Fact]
    public void MargeDeFocus_VautLEcartPlusLAnneau()
    {
        Assert.Equal(ThemeControls.BaseFocusGap + ThemeControls.BaseFocusRing,
            ThemeControls.FocusMargin(96));
        Assert.True(ThemeControls.FocusMargin(144) > ThemeControls.FocusMargin(96));
    }

    // ═══════════════════════════════════════════════════════════════
    // Outils
    // ═══════════════════════════════════════════════════════════════

    private static double Contraste(uint a, uint b)
    {
        double la = LuminanceRelative(a);
        double lb = LuminanceRelative(b);
        if (la < lb)
            (la, lb) = (lb, la);
        return (la + 0.05) / (lb + 0.05);
    }

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
    // Cadre d'un champ
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Le contour d'un champ doit tomber entièrement hors du contrôle. DrawOutline trace à
    /// right - 1 et bottom - 1 : un cadre posé sur editRect ± width y ramenait ses bords droit
    /// et bas, que WS_CLIPCHILDREN écrête ensuite. Mesuré le 2026-08-29 sur Couches maintenables,
    /// où seuls les bords haut et gauche survivaient.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void CadreDeChamp_TombeEntierementHorsDuControle(int width)
    {
        var champ = new Win32.RECT { left = 270, top = 405, right = 360, bottom = 440 };
        var cadre = ThemeControls.FieldFrameRect(champ, width);

        // Les quatre traits que DrawOutline dessine réellement. Deux décalages se cumulent sur
        // les bords droit et bas : DrawOutline passe right - inset - 1 à RoundRect, et RoundRect
        // trace son contour jusqu'à r - 1. Le premier jet de ce témoin n'en comptait qu'un et
        // restait vert quand on retirait le pixel corrigé.
        int inset = width / 2;
        int gauche = cadre.left + inset;
        int haut = cadre.top + inset;
        int droite = cadre.right - inset - 2;
        int bas = cadre.bottom - inset - 2;

        Assert.True(gauche < champ.left, $"bord gauche à {gauche}, champ à {champ.left}");
        Assert.True(haut < champ.top, $"bord haut à {haut}, champ à {champ.top}");
        Assert.True(droite >= champ.right, $"bord droit à {droite}, champ jusqu'à {champ.right - 1}");
        Assert.True(bas >= champ.bottom, $"bord bas à {bas}, champ jusqu'à {champ.bottom - 1}");
    }

}
