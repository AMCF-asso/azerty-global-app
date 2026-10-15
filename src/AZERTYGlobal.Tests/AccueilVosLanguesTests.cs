using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Accueil 1.3.0, direction C : l'étape « Écrire le peul, le wolof, le bambara ». Ces témoins
/// fixent le nombre d'étapes, l'essai ù puis b = ɓ, et le fait que chaque lettre montrée
/// existe dans character-index.json en deux frappes au plus.
/// </summary>
public class AccueilVosLanguesTests : IDisposable
{
    public AccueilVosLanguesTests() => L.Language = "fr";
    public void Dispose() => L.Language = "fr";

    [Fact]
    public void LAccueilCompteQuatreEtapesDontLesLanguesEnDeuxieme()
    {
        Assert.Equal(4, OnboardingStep.Count);
        Assert.Equal(3, OnboardingStep.Last);
        Assert.Equal(1, OnboardingStep.Languages);
        Assert.Equal("Étape 2 sur 4", OnboardingStep.Label(OnboardingStep.Languages));
        L.Language = "en";
        Assert.Equal("Step 4 of 4", OnboardingStep.Label(OnboardingStep.Last));
    }

    [Theory]
    [InlineData("ɓ")]
    [InlineData("Ɓ")]
    [InlineData("bɓ")]
    [InlineData("ɓ ")]
    public void LEssaiReconnaitLeBCrochete(string texte)
    {
        Assert.Equal(LetterTrialResult.Match, LetterTrial.Evaluate(texte));
        Assert.Equal($"C’est bien {LetterTrial.LastLetter(texte)}.", LetterTrial.Message(texte));
    }

    [Theory]
    [InlineData("b")]
    [InlineData("ùb")]
    [InlineData("ɗ")]
    [InlineData("ɓb")]
    public void UneAutreLettreEstSignalee(string texte)
    {
        Assert.Equal(LetterTrialResult.Other, LetterTrial.Evaluate(texte));
        Assert.Equal(L.Onboarding_TrialOther, LetterTrial.Message(texte));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("\r\n")]
    public void UnChampVideNAfficheRien(string? texte)
    {
        Assert.Equal(LetterTrialResult.Empty, LetterTrial.Evaluate(texte));
        Assert.Equal("", LetterTrial.Message(texte));
    }

    [Fact]
    public void LaDerniereLettreGardeLAccentCombinant()
    {
        Assert.Equal("é", LetterTrial.LastLetter("aé"));
        Assert.Equal("ŋ", LetterTrial.LastLetter("ɓ ŋ "));
    }

    [Fact]
    public void LeMessageAnglaisEstTraduit()
    {
        L.Language = "en";
        Assert.Equal("That’s ɓ.", LetterTrial.Message("ɓ"));
        Assert.StartsWith("That isn’t ɓ.", LetterTrial.Message("b"));
    }

    /// <summary>Les gestes montrés, lus dans l'index : ɓ = ù (aigu) puis b ; ñ = ^ puis n ;
    /// ɛ = AltGr + 6 (latin étendu) puis e.</summary>
    [Theory]
    [InlineData("ɓ", "Quote", false, "KeyB")]
    [InlineData("ñ", "BracketLeft", false, "KeyN")]
    [InlineData("ɛ", "Digit6", true, "KeyE")]
    public void LeGesteDeChaqueExempleEstJuste(string lettre, string morte, bool altGr, string touche)
    {
        var steps = Gesture.Of(lettre);
        Assert.Equal(2, steps.Count);
        Assert.Equal(morte, steps[0].KeyCode);
        Assert.Equal(altGr, steps[0].AltGr);
        Assert.False(steps[0].Shift);
        Assert.Equal(touche, steps[1].KeyCode);
        Assert.False(steps[1].Shift);
        Assert.False(steps[1].AltGr);
    }

    [Fact]
    public void LesExemplesSontCeuxDeLaMaquette()
    {
        Assert.Equal(new[] { "ɓ", "ñ", "ɛ" }, OnboardingLanguages.All.Select(l => l.Example));
        Assert.Equal(LetterTrial.Target, OnboardingLanguages.All[0].Example);
        foreach (var language in OnboardingLanguages.All)
            Assert.Contains(language.Example, OnboardingLanguages.LettersOf(language));
    }

    [Fact]
    public void ChaqueLettreMontreeSeTapeEnDeuxFrappesAuPlus()
    {
        foreach (var language in OnboardingLanguages.All)
            foreach (string lettre in OnboardingLanguages.LettersOf(language))
            {
                Assert.True(CharacterIndex.Shared.ByCharacter.ContainsKey(lettre), $"{lettre} absent de l'index");
                int frappes = Gesture.Of(lettre).Count;
                Assert.InRange(frappes, 1, 2);
            }
    }

    [Fact]
    public void LeResumeAccessibleNommeChaqueLangueEtSonGeste()
    {
        string resume = OnboardingLanguages.AccessibleSummary();
        foreach (var language in OnboardingLanguages.All)
        {
            Assert.Contains(language.Name(), resume);
            Assert.Contains(language.Letters, resume);
            Assert.Contains(Gesture.PlainSentence(Gesture.Of(language.Example)), resume);
        }
    }
}
