using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Règle des capitales accentuées (décision d'Antoine du 12/10, même règle que le testeur du
/// site, tester-search.js) : É, È, Ç, À qui commencent un mot suivi d'une minuscule s'enseignent
/// par la touche morte circonflexe puis la minuscule ; sinon Verr. Maj. + la touche.
/// </summary>
public class RegleCapitalesAccentueesTests
{
    private static CharacterIndex.Entry Entree(string c) => CharacterIndex.Shared.ByCharacter[c];

    [Theory]
    [InlineData("É", "Digit2")]
    [InlineData("È", "Digit7")]
    [InlineData("Ç", "Digit9")]
    [InlineData("À", "Digit0")]
    public void Capitale_devant_une_minuscule_prend_la_touche_morte_circonflexe(string capitale, string touche)
    {
        var methode = Entree(capitale).PreferredBefore('c')!;

        Assert.True(methode.IsDeadKey);
        Assert.Equal("dk_circumflex", methode.DeadKey);
        Assert.Equal(touche, methode.Key);
        Assert.Equal("Base", methode.Layer);
    }

    [Theory]
    [InlineData("É")]
    [InlineData("È")]
    [InlineData("Ç")]
    [InlineData("À")]
    public void Capitale_sans_minuscule_derriere_garde_la_methode_recommandee(string capitale)
    {
        var recommandee = Entree(capitale).Preferred!;
        Assert.StartsWith("Caps", recommandee.Layer);

        // Espace, capitale, ponctuation, chiffre, fin de texte ou suite inconnue.
        foreach (char? suivant in new char?[] { ' ', 'C', '.', '1', '\'', null })
            Assert.Same(recommandee, Entree(capitale).PreferredBefore(suivant));
    }

    [Theory]
    [InlineData("É")]
    [InlineData("È")]
    [InlineData("Ç")]
    [InlineData("À")]
    public void Verr_Maj_deja_allumee_garde_la_methode_Verr_Maj(string capitale)
    {
        var recommandee = Entree(capitale).Preferred!;

        Assert.Same(recommandee, Entree(capitale).PreferredBefore('c', capsLockActive: true));
        Assert.Equal(Gesture.Of(capitale), Gesture.Of(capitale, 'c', capsLockActive: true));
        Assert.Same(recommandee, new LessonHintProvider().GetRecommendedMethod(capitale[0], 'c', capsLockActive: true));
        // Verr. Maj. éteinte : la circonflexe, comme sans le paramètre.
        Assert.Equal("dk_circumflex", Entree(capitale).PreferredBefore('c', capsLockActive: false)!.DeadKey);
    }

    [Fact]
    public void Un_autre_caractere_est_inchange()
    {
        foreach (var c in new[] { "é", "E", "Ź", "€", "a" })
        {
            Assert.Same(Entree(c).Preferred, Entree(c).PreferredBefore('c'));
            Assert.Same(Entree(c).Preferred, Entree(c).PreferredBefore(null));
        }
    }

    [Fact]
    public void Minuscule_au_sens_du_site()
    {
        // ToLowerInvariant() == c et ToUpperInvariant() != c.
        Assert.True(CharacterIndex.Entry.IsLowercaseLetter('c'));
        Assert.True(CharacterIndex.Entry.IsLowercaseLetter('é'));
        Assert.False(CharacterIndex.Entry.IsLowercaseLetter('C'));
        Assert.False(CharacterIndex.Entry.IsLowercaseLetter('É'));
        Assert.False(CharacterIndex.Entry.IsLowercaseLetter(' '));
        Assert.False(CharacterIndex.Entry.IsLowercaseLetter('1'));
        Assert.False(CharacterIndex.Entry.IsLowercaseLetter('.'));
    }

    [Fact]
    public void Gesture_Of_avec_contexte_ecrit_deux_temps_sans_Verr_Maj()
    {
        var circonflexe = Gesture.Of("É", 'c');
        var activation = CharacterIndex.Shared.DeadKeyActivations["dk_circumflex"];

        Assert.Equal(2, circonflexe.Count);
        Assert.Equal(activation.Key, circonflexe[0].KeyCode);
        Assert.Equal("Digit2", circonflexe[1].KeyCode);
        Assert.DoesNotContain(circonflexe, s => s.KeyCode == Gesture.CapsLockCode);

        // Sans contexte, ou sans minuscule derrière : Verr. Maj. d'abord, comme avant.
        Assert.Equal(Gesture.CapsLockCode, Gesture.Of("É")[0].KeyCode);
        Assert.Equal(Gesture.CapsLockCode, Gesture.Of("É", ' ')[0].KeyCode);
        Assert.Equal(Gesture.CapsLockCode, Gesture.Of("É", null)[0].KeyCode);
        Assert.Equal(Gesture.Of("É"), Gesture.Of("É", 'C'));
        // Un caractère absent de l'index n'a pas de geste.
        Assert.Empty(Gesture.Of("\u0001", 'c'));
    }

    [Fact]
    public void Notation_des_Lecons_ne_demande_pas_d_eteindre_Verr_Maj_avec_la_circonflexe()
    {
        var geste = Gesture.Of("É", 'c');

        var (temps, notes, fait) = KeyNotation.ForLesson(geste, 'c', capsLockActive: false, deadKeyArmed: false);

        Assert.Equal(2, temps.Count);
        Assert.All(notes, n => Assert.Equal(KeyNotationNote.None, n));
        Assert.Equal(0, fait);

        // Touche morte déjà armée : le premier temps est fait.
        Assert.Equal(1, KeyNotation.ForLesson(geste, 'c', false, deadKeyArmed: true).Done);
    }

    [Fact]
    public void Indices_des_Lecons_suivent_le_caractere_suivant()
    {
        var indices = new LessonHintProvider();

        var avantMinuscule = indices.GetRecommendedMethod('É', 'c')!;
        Assert.Equal("dk_circumflex", avantMinuscule.DeadKey);
        Assert.Equal("dk_circumflex", indices.GetRecommendedMethod('À', 'p')!.DeadKey);

        Assert.StartsWith("Caps", indices.GetRecommendedMethod('É', ' ')!.Layer);
        Assert.StartsWith("Caps", indices.GetRecommendedMethod('É', null)!.Layer);
        // Sans contexte : inchangé.
        Assert.Same(indices.GetRecommendedMethod('É'), indices.GetRecommendedMethod('É', 'C'));
        Assert.Same(indices.GetRecommendedMethod('e'), indices.GetRecommendedMethod('e', 'c'));
        Assert.Null(indices.GetRecommendedMethod('\u0001', 'c'));
    }

    [Fact]
    public void Surlignage_arme_la_touche_morte_puis_montre_la_minuscule()
    {
        var methode = new LessonHintProvider().GetRecommendedMethod('É', 'c')!;
        var activation = VirtualKeyboard.KeyCodeToScancode[CharacterIndex.Shared.DeadKeyActivations["dk_circumflex"].Key];

        var etape1 = new KeyboardRenderState();
        LessonHintProvider.Guide(etape1, methode, "É", null, capsLockActive: false, GuideOptions.Lessons);
        Assert.Equal(KeyHighlight.Step1, etape1.HighlightKind);
        Assert.Equal(new[] { activation }, etape1.HighlightedScancodes);

        var etape2 = new KeyboardRenderState();
        LessonHintProvider.Guide(etape2, methode, "É", "dk_circumflex", capsLockActive: false, GuideOptions.Lessons);
        Assert.Equal(KeyHighlight.Step2, etape2.HighlightKind);
        Assert.Equal(new[] { VirtualKeyboard.KeyCodeToScancode["Digit2"] }, etape2.HighlightedScancodes);
    }
}
