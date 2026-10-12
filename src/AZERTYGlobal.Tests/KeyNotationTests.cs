using System.Linq;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Notation « Mots » des Leçons (QCM du 11/10) : temps « Verr. Maj. éteint » avant une
/// minuscule, temps déjà faits, recette écrite, notation valide par état du clavier.
/// </summary>
public class KeyNotationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    // Depuis le 12/10, É devant une minuscule s'enseigne par la touche morte circonflexe
    // (Gesture.Of("É", 'c'), voir RegleCapitalesAccentueesTests). Ces témoins gardent pourtant le
    // temps « Verr. Maj. éteint » : ils appellent Gesture.Of("É") SANS contexte, donc la méthode
    // Verr. Maj. de l'index, pour vérifier que ForLesson l'éteint devant une minuscule (tout
    // geste Verr. Maj. suivi d'une minuscule : les autres capitales à Verr. Maj. en ont besoin).
    [Fact]
    public void VerrMaj_AvantUneMinuscule_AjouteLeTempsQuiLEteint()
    {
        var gesture = Gesture.Of("É");
        Assert.Equal(Gesture.CapsLockCode, gesture[0].KeyCode);

        var (steps, notes, done) = KeyNotation.ForLesson(gesture, 'c', capsLockActive: false, deadKeyArmed: false);

        Assert.Equal(gesture.Count + 1, steps.Count);
        Assert.Equal(Gesture.CapsLockCode, steps[^1].KeyCode);
        Assert.Equal(KeyNotationNote.TurnsOn, notes[0]);
        Assert.Equal(KeyNotationNote.TurnsOff, notes[^1]);
        Assert.Equal(0, done);
    }

    [Theory]
    [InlineData('C')]
    [InlineData(' ')]
    [InlineData('.')]
    public void VerrMaj_SansMinusculeEnSuivant_NeLEteintPas(char following)
    {
        var gesture = Gesture.Of("É");

        var (steps, notes, _) = KeyNotation.ForLesson(gesture, following, false, false);

        Assert.Equal(gesture.Count, steps.Count);
        Assert.DoesNotContain(KeyNotationNote.TurnsOff, notes);
    }

    [Fact]
    public void VerrMaj_DejaAllumee_PremierTempsFait()
    {
        var (_, _, done) = KeyNotation.ForLesson(Gesture.Of("É"), null, capsLockActive: true, deadKeyArmed: false);
        Assert.Equal(1, done);
    }

    [Fact]
    public void ToucheMorte_DejaArmee_PremierTempsFait()
    {
        var gesture = Gesture.Of("Ź");
        Assert.Equal(2, gesture.Count);

        Assert.Equal(1, KeyNotation.ForLesson(gesture, null, false, deadKeyArmed: true).Done);
        Assert.Equal(0, KeyNotation.ForLesson(gesture, null, false, deadKeyArmed: false).Done);
        // Un geste d'un seul temps ne se compte jamais à moitié fait.
        Assert.Equal(0, KeyNotation.ForLesson(Gesture.Of("é"), null, false, deadKeyArmed: true).Done);
    }

    [Fact]
    public void Recette_NomsDeTouchesEnGras_PuisAvantLeDernierTemps()
    {
        var steps = Gesture.Of("Ź");

        var runs = KeyNotation.Recipe(steps);

        var strong = runs.Where(r => r.Strong).Select(r => r.Text).ToList();
        Assert.Contains(steps[0].KeyLabel, strong);
        Assert.Contains(steps[1].KeyLabel, strong);
        Assert.Contains(L.Gesture_LineShift, strong);
        Assert.Contains(runs, r => r.Text == L.KeyNotation_RecipeThenLast);
        Assert.Equal(string.Concat(runs.Select(r => r.Text)), KeyNotation.PlainRecipe(steps));
    }

    [Fact]
    public void Recette_VerrMajEteinte_LeDit()
    {
        var (steps, notes, _) = KeyNotation.ForLesson(Gesture.Of("É"), 'c', false, false);

        string plain = KeyNotation.PlainRecipe(steps, notes);

        Assert.EndsWith(L.KeyNotation_RecipeTurnsOff, plain);
    }

    [Fact]
    public void NotationAbsenteDeLEtat_RevientAuxMots()
    {
        Assert.Equal(LessonNotation.Words, LessonNotations.Valid(LessonNotation.MiniKeyboard, keyboardVisible: true));
        Assert.Equal(LessonNotation.Words, LessonNotations.Valid(LessonNotation.NumberedKeyboard, keyboardVisible: false));
        Assert.Equal(LessonNotation.Recipe, LessonNotations.Valid(LessonNotation.Recipe, keyboardVisible: false));
        Assert.Equal(LessonNotation.Frames, LessonNotations.Valid(LessonNotation.Frames, keyboardVisible: false));
    }

    [Fact]
    public void Maitrise_BoiteQuatreEtPasEchue()
    {
        Assert.False(LessonNotations.IsMastered(null, T0));
        Assert.False(LessonNotations.IsMastered(new GestureMasteryEntry("é") { Box = 3, DueUtc = T0.AddDays(5) }, T0));
        Assert.False(LessonNotations.IsMastered(new GestureMasteryEntry("é") { Box = 4, DueUtc = T0.AddDays(-1) }, T0));
        Assert.True(LessonNotations.IsMastered(new GestureMasteryEntry("é") { Box = 4, DueUtc = T0.AddDays(5) }, T0));
    }
}
