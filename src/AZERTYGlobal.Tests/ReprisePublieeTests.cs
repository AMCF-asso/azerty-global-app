using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Mise à jour depuis une version publiée (audit de régression du 10/11/2026, RA2) : les
/// exercices déjà réussis le restent, même quand leur texte à taper a changé depuis. Les
/// témoins de `Fixtures/` sont le fichier de progression qu'écrit une ancienne version, tous
/// exercices réussis ; leurs empreintes sont calculées hors de l'app, depuis les sources de
/// cette version (`scripts/build-lesson-progress-fixtures.py`) :
/// 039733c = sources du 27/07/2026 (leçons de la 1.0.0, consignes de l'Initiation en dur),
/// 452aab0 = sources du 15/08/2026 (base de la 1.2.0), en français et en anglais.
/// </summary>
public class ReprisePublieeTests : IDisposable
{
    // Exercices dont le texte a changé depuis ces versions (6eedab4 et d02f46e).
    private static readonly string[] TextesChanges =
    {
        "initiation/premiers-pas/1/",
        "initiation/premiers-pas/2/",
        "initiation/premiers-pas/3/",
        "initiation/premiers-pas/4/",
        "initiation/premiers-pas/5/",
        "majuscules-accentuees/caps-e-acute/1/",
        "majuscules-accentuees/caps-e-grave/1/",
        "majuscules-accentuees/caps-c-cedilla/1/",
        "majuscules-accentuees/caps-a-grave/1/",
    };

    private readonly string _tempDir;

    public ReprisePublieeTests()
    {
        L.Language = "fr";
        _tempDir = Path.Combine(Path.GetTempPath(), "AZGREPRISE_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        ConfigManager.OverrideConfigPathForTests(Path.Combine(_tempDir, "config.json"));
    }

    public void Dispose()
    {
        L.Language = "fr";
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    public static IEnumerable<object[]> TemoinsEtLangues() =>
        from temoin in new[] { "039733c-fr", "452aab0-fr", "452aab0-en" }
        from langue in new[] { "fr", "en" }
        select new object[] { temoin, langue };

    [Theory]
    [MemberData(nameof(TemoinsEtLangues))]
    public void Une_progression_publiee_relue_garde_ses_exercices_reussis(string temoin, string langue)
    {
        L.Language = langue;
        var catalogue = LessonCatalogLoader.LoadFromResource();
        var magasin = new LessonProgressStore(CopieDuTemoin(temoin));
        var rangsDAlors = magasin.Exercises.Keys.Select(Prefixe).ToHashSet(StringComparer.Ordinal);

        // Tout exercice actuel qui existait dans la version d'alors (même module, leçon, rang).
        var repris = catalogue.Exercises.Where(e => rangsDAlors.Contains(e.KeyPrefix)).ToList();
        Assert.Equal(75, repris.Count); // 76 exercices d'alors, moins caps-a-grave/2 disparu
        Assert.All(repris, e => Assert.True(magasin.IsCompleted(e), $"{e.KeyPrefix} repasse « à faire » ({temoin}, {langue})"));

        // Rien d'autre n'est réussi : les exercices ajoutés depuis restent « à faire ».
        Assert.Equal(75, magasin.CountCompleted(catalogue));
    }

    [Theory]
    [InlineData("039733c-fr")]
    [InlineData("452aab0-fr")]
    [InlineData("452aab0-en")]
    public void Un_exercice_au_texte_change_est_repris_reussi_sans_scores(string temoin)
    {
        var catalogue = LessonCatalogLoader.LoadFromResource();
        var magasin = new LessonProgressStore(CopieDuTemoin(temoin));

        foreach (string prefixe in TextesChanges)
        {
            var progression = magasin.GetValidProgress(catalogue.Exercises.Single(e => e.KeyPrefix == prefixe));
            Assert.NotNull(progression);
            Assert.True(progression!.Completed, prefixe);
            Assert.Equal(1, progression.SuccessfulAttempts);
            Assert.Null(progression.BestWpm);
            Assert.Null(progression.BestAccuracyPercent);
            Assert.Null(progression.BestSeconds);
        }

        // Texte inchangé : la progression revient entière, scores compris.
        var inchange = magasin.GetValidProgress(catalogue.Exercises.Single(e => e.KeyPrefix == "majuscules-accentuees/caps-e-acute/0/"));
        Assert.Equal(30, inchange!.BestWpm);
        Assert.Equal(2, inchange.SuccessfulAttempts);
    }

    [Fact]
    public void Un_texte_inconnu_reste_a_faire()
    {
        var catalogue = LessonCatalogLoader.LoadFromResource();
        string chemin = Path.Combine(_tempDir, "lessons-progress.json");
        var entrees = new List<string>();
        var vises = TextesChanges.Select(prefixe => catalogue.Exercises.Single(e => e.KeyPrefix == prefixe)).ToList();
        foreach (var exercice in vises)
        {
            // Réussi sous un texte que l'app n'a jamais publié à ce rang, avec la consigne
            // d'alors (ancienne empreinte) puis sans consigne (empreinte actuelle).
            string texte = exercice.Content + " jamais publié";
            string ancienne = LessonCatalogLoader.ComputeLegacyExerciseHash(exercice.Type, exercice.Instruction, texte);
            string actuelle = LessonCatalogLoader.ComputeExerciseHash(exercice.Type, texte);
            entrees.Add(Entree(exercice.KeyPrefix + ancienne, ancienne, reussi: true));
            entrees.Add(Entree(exercice.KeyPrefix + actuelle, actuelle, reussi: true));
        }
        File.WriteAllText(chemin, Fichier(entrees));

        var magasin = new LessonProgressStore(chemin);
        Assert.Equal(2 * vises.Count, magasin.Exercises.Count);
        Assert.All(vises, e => Assert.Null(magasin.GetValidProgress(e)));
        Assert.Equal(0, magasin.CountCompleted(catalogue));
    }

    [Fact]
    public void Un_ancien_texte_commence_sans_etre_reussi_reste_a_faire()
    {
        var catalogue = LessonCatalogLoader.LoadFromResource();
        var exercice = catalogue.Exercises.Single(e => e.KeyPrefix == "majuscules-accentuees/caps-e-acute/1/");
        string cle = exercice.SupersededKeys[0];
        string chemin = Path.Combine(_tempDir, "lessons-progress.json");
        File.WriteAllText(chemin, Fichier(new[] { Entree(cle, cle[(cle.LastIndexOf('/') + 1)..], reussi: false) }));

        Assert.False(new LessonProgressStore(chemin).IsCompleted(exercice));
    }

    [Fact]
    public void L_exercice_disparu_caps_a_grave_2_est_repris_par_le_rang_1_qui_porte_son_texte()
    {
        var catalogue = LessonCatalogLoader.LoadFromResource();
        var rang1 = catalogue.Exercises.Single(e => e.KeyPrefix == "majuscules-accentuees/caps-a-grave/1/");
        Assert.DoesNotContain(catalogue.Exercises, e => e.KeyPrefix == "majuscules-accentuees/caps-a-grave/2/");

        // Seul l'ancien rang 2 est réussi dans le témoin réduit à cette entrée.
        var temoin = new LessonProgressStore(CopieDuTemoin("452aab0-fr"));
        var ancien = temoin.Exercises.Single(pair => pair.Key.StartsWith("majuscules-accentuees/caps-a-grave/2/", StringComparison.Ordinal));
        string chemin = Path.Combine(_tempDir, "seul.json");
        File.WriteAllText(chemin, Fichier(new[] { Entree(ancien.Key, ancien.Value.Hash, reussi: true) }));

        var progression = new LessonProgressStore(chemin).GetValidProgress(rang1);
        Assert.True(progression?.Completed);
        Assert.Null(progression!.BestWpm);
    }

    [Fact]
    public void La_premiere_ecriture_garde_la_reprise_et_les_anciennes_entrees()
    {
        var catalogue = LessonCatalogLoader.LoadFromResource();
        string chemin = CopieDuTemoin("452aab0-fr");
        var exercice = catalogue.Exercises.Single(e => e.KeyPrefix == "initiation/premiers-pas/3/");

        var magasin = new LessonProgressStore(chemin);
        int avant = magasin.Exercises.Count;
        magasin.MarkCompletedNeutral(exercice);

        var relu = new LessonProgressStore(chemin);
        Assert.True(relu.Exercises[exercice.StableKey].Completed);
        Assert.Equal(avant + 1, relu.Exercises.Count); // une version précédente relit les siennes
        Assert.Equal(75, relu.CountCompleted(catalogue));
    }

    [Fact]
    public void La_table_des_anciennes_formes_est_embarquee()
    {
        Assert.Equal(TextesChanges.Length, LessonContentHistory.Count);
        Assert.All(TextesChanges, prefixe => Assert.NotEmpty(LessonContentHistory.For(prefixe.TrimEnd('/'))));
    }

    private static string Prefixe(string cle) => cle[..(cle.LastIndexOf('/') + 1)];

    private string CopieDuTemoin(string temoin)
    {
        string copie = Path.Combine(_tempDir, $"lessons-progress-{temoin}.json");
        File.Copy(Path.Combine(DossierDesTemoins(), $"lessons-progress-{temoin}.json"), copie, overwrite: true);
        return copie;
    }

    private static string DossierDesTemoins()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidat = Path.Combine(dir.FullName, "src", "AZERTYGlobal.Tests", "Fixtures");
            if (Directory.Exists(candidat))
                return candidat;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Dossier Fixtures introuvable depuis les tests.");
    }

    private static string Fichier(IEnumerable<string> entrees) =>
        "{\n  \"version\": 1,\n  \"exercises\": {\n" + string.Join(",\n", entrees) + "\n  }\n}\n";

    private static string Entree(string cle, string empreinte, bool reussi) =>
        $"    \"{cle}\": {{ \"hash\": \"{empreinte}\", \"completed\": {(reussi ? "true" : "false")}, " +
        "\"successfulAttempts\": 1, \"bestWpm\": 30, \"bestAccuracyPercent\": 95, \"bestSeconds\": 10, " +
        "\"lastCompletedUtc\": null, \"hintsUsed\": 0 }";
}
