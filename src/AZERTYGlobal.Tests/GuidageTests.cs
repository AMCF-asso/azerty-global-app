using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Témoins du guidage unique (lot 9, audit du 25/09 L-02) : <see cref="LessonHintProvider.Guide"/>
/// calcule la touche suivante pour le tutoriel et pour les Leçons, avec les vraies méthodes de
/// character-index.json. Le tutoriel guide pas à pas selon l'état du clavier ; les Leçons
/// montrent la méthode recommandée. Chaque témoin fixe une règle que le tutoriel suivait avant
/// le lot, et vérifie que les Leçons n'en héritent pas.
/// </summary>
public class GuidageTests
{
    private static readonly GuideOptions Tutoriel = GuideOptions.Tutorial(keepCapsLock: false);
    private static uint Touche(string code) => VirtualKeyboard.KeyCodeToScancode[code];
    private static MethodData Recommandee(string c) => CharacterIndex.Shared.ByCharacter[c].Preferred!;

    private static KeyboardRenderState Guider(MethodData methode, string caractere, string? toucheMorte,
        bool verrMaj, GuideOptions reglage)
    {
        var etat = new KeyboardRenderState();
        LessonHintProvider.Guide(etat, methode, caractere, toucheMorte, verrMaj, reglage);
        return etat;
    }

    [Fact]
    public void Une_majuscule_par_Verr_Maj_demande_d_abord_Verr_Maj()
    {
        // É de l'exercice 1 : touche é sur la couche Caps.
        var methode = CharacterIndex.Shared.ByCharacter["É"].Caps!;

        var eteinte = Guider(methode, "É", null, verrMaj: false, Tutoriel);
        Assert.Equal(new[] { "Verr. Maj." }, eteinte.HighlightedLabels);
        Assert.Empty(eteinte.HighlightedScancodes);

        var allumee = Guider(methode, "É", null, verrMaj: true, Tutoriel);
        Assert.Equal(new[] { Touche("Digit2") }, allumee.HighlightedScancodes);
        Assert.Equal(KeyHighlight.Direct, allumee.HighlightKind);

        // Les Leçons montrent tout de suite la touche et Verr. Maj.
        var lecons = Guider(methode, "É", null, verrMaj: false, GuideOptions.Lessons);
        Assert.Equal(new[] { Touche("Digit2") }, lecons.HighlightedScancodes);
        Assert.Contains("Verr. Maj.", lecons.HighlightedLabels);
    }

    [Fact]
    public void Une_touche_morte_passe_de_l_armement_a_la_touche_finale()
    {
        // ã : accent circonflexe (touche ^), puis p.
        var methode = Recommandee("ã");

        var etape1 = Guider(methode, "ã", null, false, Tutoriel);
        Assert.Equal(KeyHighlight.Step1, etape1.HighlightKind);
        Assert.Equal(new[] { Touche(methode.DkActivationKey) }, etape1.HighlightedScancodes);

        var etape2 = Guider(methode, "ã", methode.DeadKey, false, Tutoriel);
        Assert.Equal(KeyHighlight.Step2, etape2.HighlightKind);
        Assert.Equal(new[] { Touche("KeyP") }, etape2.HighlightedScancodes);
    }

    [Fact]
    public void Une_autre_touche_morte_armee_mene_a_sa_touche_ou_au_retour_arriere()
    {
        var methode = Recommandee("ã");

        // Le tilde donne aussi ã, par la touche a.
        var tilde = Guider(methode, "ã", "dk_tilde", false, Tutoriel);
        Assert.Equal(KeyHighlight.Step2, tilde.HighlightKind);
        Assert.Equal(new[] { Touche("KeyQ") }, tilde.HighlightedScancodes);

        // L'accent aigu ne le donne pas : Retour arrière, en étape 2.
        var aigu = Guider(methode, "ã", "dk_acute", false, Tutoriel);
        Assert.Equal(KeyHighlight.Step2, aigu.HighlightKind);
        Assert.Equal(new[] { 0x0Eu }, aigu.HighlightedScancodes);

        // Les Leçons reviennent à l'armement de la bonne touche morte.
        var lecons = Guider(methode, "ã", "dk_acute", false, GuideOptions.Lessons);
        Assert.Equal(KeyHighlight.Step1, lecons.HighlightKind);
        Assert.Equal(new[] { Touche(methode.DkActivationKey) }, lecons.HighlightedScancodes);
    }

    [Fact]
    public void Une_couche_Caps_avec_AltGr_surligne_aussi_AltGr()
    {
        // Espace fine de l'exercice 2 : AltGr + Espace, Verr. Maj. gardée (couche Caps+AltGr).
        var methode = CharacterIndex.Shared.ByCharacter[" "].Caps!;
        Assert.Equal("Caps+AltGr", methode.Layer);

        var allumee = Guider(methode, " ", null, verrMaj: true, GuideOptions.Tutorial(keepCapsLock: true));
        Assert.Equal(new[] { Touche("Space") }, allumee.HighlightedScancodes);
        Assert.Contains("AltGr", allumee.HighlightedLabels);
        Assert.Contains("Verr. Maj.", allumee.HighlightedLabels);
    }

    [Fact]
    public void Verr_Maj_active_remplace_Maj_pour_une_lettre_dans_le_tutoriel()
    {
        var methode = Recommandee("L"); // Maj + l

        var tutoriel = Guider(methode, "L", null, verrMaj: true, Tutoriel);
        Assert.Contains("Verr. Maj.", tutoriel.HighlightedLabels);
        Assert.Empty(tutoriel.HighlightedContextIds);

        var lecons = Guider(methode, "L", null, verrMaj: true, GuideOptions.Lessons);
        Assert.Equal(new[] { VirtualKeyboard.ContextShiftLeft }, lecons.HighlightedContextIds);
    }

    [Fact]
    public void Les_exercices_1_et_2_gardent_Verr_Maj_surlignee()
    {
        var virgule = Recommandee(",");

        var garde = Guider(virgule, ",", null, false, GuideOptions.Tutorial(keepCapsLock: true));
        Assert.Contains("Verr. Maj.", garde.HighlightedLabels);
        Assert.DoesNotContain("Verr. Maj.", Guider(virgule, ",", null, false, Tutoriel).HighlightedLabels);
    }

    [Fact]
    public void Une_couche_se_lit_en_modificateurs()
    {
        Assert.Equal(HintLayers.None, LessonHintProvider.ParseLayers("Base"));
        Assert.Equal(HintLayers.Shift | HintLayers.AltGr, LessonHintProvider.ParseLayers("Shift+AltGr"));
        Assert.Equal(HintLayers.Caps | HintLayers.Shift, LessonHintProvider.ParseLayers("Caps+Shift"));
        Assert.Equal(HintLayers.AltGr, LessonHintProvider.ParseLayers("AltGr"));
    }
}
