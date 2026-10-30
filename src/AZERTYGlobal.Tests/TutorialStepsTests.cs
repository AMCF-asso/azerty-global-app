using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Témoins des huit exercices du tutoriel, en un seul exemplaire depuis le lot 9 (audit du
/// 25/09, L-12) : <see cref="TutorialSteps"/>, lu par le tutoriel et par le module Initiation
/// des Leçons. Les textes sont recopiés ici exprès : la progression déjà enregistrée tient à
/// eux (clé et empreinte de chaque exercice, rang pour l'import de l'accueil). Un texte changé
/// efface la réussite de cet exercice chez chaque utilisateur. Les six premiers datent d'avant
/// le choix des usages (06/10) et n'ont pas bougé.
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
        "À table\u202F! Ça refroidit\u202F!",
        "ɛ ɔ ŋ ɲ ɓ ɗ",
    };

    // Empreintes des six exercices d'avant le 06/10, consignes françaises, calculées sur
    // ac4ff72 : les réussites déjà enregistrées dans les Leçons en dépendent.
    private static readonly string[] EmpreintesAnciennes =
    {
        "f0f75ceff81450e879bf1da2ccdeddb0c8db0729624e004ebe6e50f0efc5b340",
        "89d0b9a59748397dc1cfb2e7b6a57873a438e3c0b57404a4e48796462a9f949a",
        "9a6f1c811322674e12f737b1b2320b93c6230802bf2c732889b146d2e16772fd",
        "2a16fbaab281062ab70597b6fce396c8e0fc6cce8ebbb124ceba6ac739d22e72",
        "93601276c20d43e79f0a2db49ae8401417aca12d420e5f926106219aaac086fc",
        "e8c7b5939f3361125ce4fc4e274d01d6d905a90ae870dbac92bbddda24559193",
    };

    public TutorialStepsTests() => L.Language = "fr";
    public void Dispose() => L.Language = "fr";

    [Fact]
    public void Les_huit_textes_et_leurs_reglages_ne_bougent_pas()
    {
        Assert.Equal(Textes, TutorialSteps.All.Select(s => s.Target));
        Assert.Equal(new[] { true, true, false, false, false, false, false, false }, TutorialSteps.All.Select(s => s.KeepCapsLock));
        Assert.Equal(3, TutorialSteps.DoneThreshold);
        Assert.Equal(
            new[] { 3, 4, 5, 6, 7 },
            new[] { TutorialSteps.Documents, TutorialSteps.Code, TutorialSteps.Languages, TutorialSteps.Messages, TutorialSteps.Africa });
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("en")]
    public void Le_module_Initiation_reprend_les_memes_exercices_et_empreintes(string langue)
    {
        L.Language = langue;
        string[] consignes =
        {
            L.Lessons_Init0, L.Lessons_Init1, L.Lessons_Init2, L.Lessons_Init3,
            L.Lessons_Init4, L.Lessons_Init5, L.Lessons_Init6, L.Lessons_Init7,
        };

        var initiation = LessonCatalogLoader.LoadFromResource().Modules[0].Lessons[0].Exercises;

        Assert.Equal(Textes.Length, initiation.Count);
        for (int i = 0; i < Textes.Length; i++)
        {
            Assert.Equal(Textes[i], initiation[i].Content);
            Assert.Equal(i, initiation[i].ExerciseIndex);
            Assert.Equal(LessonCatalogLoader.ComputeExerciseHash("practice", consignes[i], Textes[i]), initiation[i].Hash);
        }
    }

    [Fact]
    public void Les_empreintes_francaises_des_six_premiers_exercices_sont_inchangees()
    {
        L.Language = "fr";
        var initiation = LessonCatalogLoader.LoadFromResource().Modules[0].Lessons[0].Exercises;
        Assert.Equal(EmpreintesAnciennes, initiation.Take(6).Select(e => e.Hash));
    }

    [Fact]
    public void Aucun_usage_coche_donne_le_tronc_commun_puis_Documents()
    {
        var parcours = TutorialSteps.BuildRoute(Array.Empty<string>());

        Assert.Equal(new[] { 0, 1, 2, TutorialSteps.Documents }, parcours.Select(e => e.Index));
        Assert.All(parcours, e => Assert.False(e.Skippable));
        Assert.Equal(parcours.Select(e => e.Index), TutorialSteps.BuildRoute(null).Select(e => e.Index));
    }

    [Fact]
    public void Le_plus_particulier_des_usages_donne_l_exercice_principal_les_autres_en_bonus()
    {
        var parcours = TutorialSteps.BuildRoute(new[]
        {
            TutorialSteps.UsageMessages, TutorialSteps.UsageAfrica, TutorialSteps.UsageCode,
        });

        Assert.Equal(new[] { 0, 1, 2, TutorialSteps.Africa, TutorialSteps.Code, TutorialSteps.Messages },
            parcours.Select(e => e.Index));
        Assert.Equal(new[] { false, false, false, false, true, true }, parcours.Select(e => e.Skippable));
    }

    [Fact]
    public void L_ecran_final_recapitule_les_textes_reussis_dans_l_ordre_du_parcours()
    {
        var parcours = TutorialSteps.BuildRoute(new[] { TutorialSteps.UsageCode, TutorialSteps.UsageAfrica });
        var reussis = new HashSet<int> { 0, 1, 2, TutorialSteps.Africa };

        // Le bonus Code est passé : il ne figure pas dans la liste.
        Assert.Equal(
            new[] { TutorialSteps.All[0].Target, TutorialSteps.All[1].Target, TutorialSteps.All[2].Target,
                    TutorialSteps.All[TutorialSteps.Africa].Target },
            TutorialSteps.RecapTargets(parcours, reussis.Contains));
        Assert.Empty(TutorialSteps.RecapTargets(parcours, _ => false));
    }

    [Fact]
    public void Chaque_ligne_de_l_ecran_final_donne_le_geste_de_son_premier_caractere_particulier()
    {
        // « É » seul : Verr. Maj. puis la touche é ; la suite de la ligne n'est pas lue.
        string? e = TutorialSteps.RecapGesture(TutorialSteps.All[0].Target);
        Assert.NotNull(e);
        Assert.StartsWith("É", e);
        Assert.Contains("é", e);

        // « contact@exemple.com » : les lettres se tapent sur leur touche, l'arobase non.
        string? mail = TutorialSteps.RecapGesture("contact@exemple.com");
        Assert.NotNull(mail);
        Assert.StartsWith("@", mail);
        Assert.Contains(L.Gesture_KeyTopLeft, mail); // la place de la touche, pas sa gravure

        // Un texte entièrement sur ses touches gravées (espaces compris) n'a pas de geste à dire.
        Assert.Null(TutorialSteps.RecapGesture("abc def"));
        Assert.Null(TutorialSteps.RecapGesture(""));
    }

    [Fact]
    public void Un_seul_usage_coche_n_est_jamais_facultatif()
    {
        foreach (var (usage, step) in TutorialSteps.UsagesByPriority)
        {
            var parcours = TutorialSteps.BuildRoute(new[] { usage });
            Assert.Equal(new[] { 0, 1, 2, step }, parcours.Select(e => e.Index));
            Assert.All(parcours, e => Assert.False(e.Skippable));
        }
    }

    [Fact]
    public void Les_usages_enregistres_sont_filtres_et_ranges_dans_l_ordre_de_l_ecran()
    {
        Assert.Equal(
            new[] { TutorialSteps.UsageDocuments, TutorialSteps.UsageCode, TutorialSteps.UsageAfrica },
            TutorialSteps.NormalizeUsages(new[] { "afrique", "inconnu", "code", "documents", "code" }));
        Assert.Empty(TutorialSteps.NormalizeUsages(null));
    }
}
