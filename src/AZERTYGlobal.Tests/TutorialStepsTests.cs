using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Témoins des six exercices du tutoriel, en un seul exemplaire depuis le lot 9 (audit du
/// 25/09, L-12) : <see cref="TutorialSteps"/>, lu par le tutoriel et par le module Initiation
/// des Leçons. Les textes sont recopiés ici exprès : la progression déjà enregistrée tient à
/// eux (clé et empreinte de chaque exercice, rang pour l'import de l'accueil). Un texte changé
/// efface la réussite de cet exercice chez chaque utilisateur.
/// </summary>
public class TutorialStepsTests : IDisposable
{
    private static readonly string[] Textes =
    {
        "É",
        "ÇA GÈLE À MONTRÉAL, PAS À ABIDJAN\u202F!",
        "contact@exemple.com",
        "«\u00A0Un chef-d\u2019œuvre\u202F!\u00A0» — Maman",
        "if (a || b) { t[0] = \"\\n\"; }",
        "María et Niccolò arrivent à São Paulo.",
    };

    public TutorialStepsTests() => L.Language = "fr";
    public void Dispose() => L.Language = "fr";

    [Fact]
    public void Les_six_textes_et_leurs_reglages_ne_bougent_pas()
    {
        Assert.Equal(Textes, TutorialSteps.All.Select(s => s.Target));
        Assert.Equal(new[] { true, true, false, false, false, false }, TutorialSteps.All.Select(s => s.KeepCapsLock));
        Assert.Equal(new[] { false, false, false, false, true, true }, TutorialSteps.All.Select(s => s.Skippable));
        Assert.Equal(3, TutorialSteps.DoneThreshold);
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("en")]
    public void Le_module_Initiation_reprend_les_memes_exercices_et_empreintes(string langue)
    {
        L.Language = langue;
        string[] consignes = { L.Lessons_Init0, L.Lessons_Init1, L.Lessons_Init2, L.Lessons_Init3, L.Lessons_Init4, L.Lessons_Init5 };

        var initiation = LessonCatalogLoader.LoadFromResource().Modules[0].Lessons[0].Exercises;

        Assert.Equal(Textes.Length, initiation.Count);
        for (int i = 0; i < Textes.Length; i++)
        {
            Assert.Equal(Textes[i], initiation[i].Content);
            Assert.Equal(LessonCatalogLoader.ComputeExerciseHash("practice", consignes[i], Textes[i]), initiation[i].Hash);
        }
    }
}
