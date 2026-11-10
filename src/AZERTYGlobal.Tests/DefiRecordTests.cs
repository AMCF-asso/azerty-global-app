using System.Text;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit UX du 09/11, L10 : le « Record » du Défi était celui de l'exercice du jour, donc neuf
/// chaque matin ; le compteur « x/N exercices » comptait les deux exercices du Défi et baissait
/// le lendemain ; rien ne disait qu'une séance déjà faite ne compte plus. Et L13 : « Réinitialiser
/// la progression » efface aussi le record et les gestes, ce que la confirmation doit dire.
/// </summary>
public class DefiRecordTests
{
    private static readonly DateOnly Hier = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);
    private const int Imbattable = 2_000_000_000;

    private static LessonsWindow Ouvrir()
    {
        var layout = LayoutLoader.LoadFromResource();
        var mapper = new KeyMapper(layout, new MockWin32Api());
        var hook = new KeyboardHook(mapper); // jamais installé
        ConfigManager.ClearWindowBounds(ConfigManager.LessonsWindowBoundsKey);
        return new LessonsWindow(layout, mapper, hook);
    }

    /// <summary>Progression écrite par une séance passée : l'échauffement et l'extrait d'hier.</summary>
    private static void ÉcrireLeDéfiDHier(int vitesseÉchauffement, int vitesseExtrait)
    {
        var leçon = DailyChallenge.BuildModule(Hier, DailyChallenge.SequenceLength)!.Lessons[0];
        string Entrée(LessonExercise exercice, int vitesse) =>
            $"    \"{exercice.StableKey}\": {{ \"hash\": \"{exercice.Hash}\", \"completed\": true, " +
            $"\"successfulAttempts\": 1, \"bestWpm\": {vitesse}, \"bestAccuracyPercent\": 95, " +
            "\"bestSeconds\": 30, \"lastCompletedUtc\": null, \"hintsUsed\": 0 }";
        Directory.CreateDirectory(ConfigManager.LogDirectory);
        File.WriteAllText(Path.Combine(ConfigManager.LogDirectory, LessonProgressStore.FileName),
            "{\n  \"version\": 1,\n  \"exercises\": {\n" +
            Entrée(leçon.Exercises[0], vitesseÉchauffement) + ",\n" +
            Entrée(leçon.Exercises[DailyChallenge.ExtractExerciseIndex], vitesseExtrait) + "\n" +
            "  }\n}\n", new UTF8Encoding(false));
    }

    /// <summary>Ouvre le Défi commun du jour sur l'exercice demandé et le tape sans faute.</summary>
    private static void JouerLeDéfi(LessonsWindow fenêtre, int exercice)
    {
        BancCapture.Field<ChallengeSessionState>(fenêtre, "_challenge").Request();
        BancCapture.Call(fenêtre, "RefreshChallengeModuleIfStale");
        var défi = BancCapture.Field<LessonCatalog>(fenêtre, "_catalog").Modules.Single(m => m.Id == DailyChallenge.ModuleId);
        Assert.True((bool)BancCapture.Call(fenêtre, "SelectExercise", DailyChallenge.ModuleId, défi.Lessons[0].Id, exercice, false)!);
        Taper(fenêtre);
    }

    private static void Taper(LessonsWindow fenêtre)
    {
        for (int garde = 0; garde < 5000 && !BancCapture.Field<bool>(fenêtre, "_showSummary"); garde++)
        {
            var séance = BancCapture.Field<LessonTypingSession>(fenêtre, "_session");
            Assert.True(séance.GetNextExpectedCharacter() is char, "séance bloquée avant le bilan");
            BancCapture.Call(fenêtre, "OnChar", séance.GetNextExpectedCharacter()!.Value);
        }
        Assert.True(BancCapture.Field<bool>(fenêtre, "_showSummary"));
    }

    [Fact]
    public void Le_record_reprend_les_extraits_des_jours_passes_pas_les_echauffements()
    {
        using var isolation = new BancCapture.Isolation();
        ÉcrireLeDéfiDHier(vitesseÉchauffement: 90, vitesseExtrait: 57);

        Assert.Equal(57, new LessonProgressStore().BestChallengeWpm());
        Assert.Equal(57, ChallengeSessionState.PersonalBestWpm(new LessonProgressStore()));

        ConfigManager.SetTrainingBestWpm(60);
        Assert.Equal(60, ChallengeSessionState.PersonalBestWpm(new LessonProgressStore()));
    }

    [Fact]
    public void Sans_extrait_chronometre_il_n_y_a_pas_de_record()
    {
        using var isolation = new BancCapture.Isolation();
        Assert.Null(ChallengeSessionState.PersonalBestWpm(new LessonProgressStore()));
    }

    [Fact]
    public void Au_bilan_de_l_extrait_le_record_est_celui_de_tous_les_Defis()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.SetTrainingSequenceIndex(DailyChallenge.SequenceLength);
        // Imbattable, même par la frappe instantanée du banc (des millions de mots/min).
        ÉcrireLeDéfiDHier(vitesseÉchauffement: 90, vitesseExtrait: Imbattable);
        var fenêtre = Ouvrir();
        try
        {
            JouerLeDéfi(fenêtre, DailyChallenge.ExtractExerciseIndex);

            Assert.Equal(Imbattable, BancCapture.Field<int?>(fenêtre, "_summaryBestWpm"));
            Assert.False(BancCapture.Field<bool>(fenêtre, "_summaryIsNewBest"));
            Assert.Equal((uint)Imbattable, ConfigManager.TrainingBestWpm); // record d'avant la 1.3.0 repris en config
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    [Fact]
    public void Un_record_battu_est_garde_en_config()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.SetTrainingSequenceIndex(DailyChallenge.SequenceLength);
        ConfigManager.SetTrainingBestWpm(1);
        var fenêtre = Ouvrir();
        try
        {
            JouerLeDéfi(fenêtre, DailyChallenge.ExtractExerciseIndex);

            int vitesse = BancCapture.Field<LessonTypingSession>(fenêtre, "_session").Stats.Wpm ?? 0;
            Assert.True(vitesse > 1, $"vitesse mesurée : {vitesse}");
            Assert.Equal(vitesse, BancCapture.Field<int?>(fenêtre, "_summaryBestWpm"));
            Assert.True(BancCapture.Field<bool>(fenêtre, "_summaryIsNewBest"));
            Assert.Equal((uint)vitesse, ConfigManager.TrainingBestWpm);
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    [Fact]
    public void L_echauffement_n_a_pas_de_record()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.SetTrainingSequenceIndex(DailyChallenge.SequenceLength);
        var fenêtre = Ouvrir();
        try
        {
            JouerLeDéfi(fenêtre, 0);
            Assert.Null(BancCapture.Field<int?>(fenêtre, "_summaryBestWpm"));
            Assert.Equal(0u, ConfigManager.TrainingBestWpm);
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    [Fact]
    public void Le_compteur_de_la_liste_ne_compte_pas_le_Defi()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.SetTrainingSequenceIndex(DailyChallenge.SequenceLength);
        var fenêtre = Ouvrir();
        try
        {
            var sansDéfi = ((int Done, int Total))BancCapture.Call(fenêtre, "PickerExerciseCount")!;
            JouerLeDéfi(fenêtre, 0);
            var catalogue = BancCapture.Field<LessonCatalog>(fenêtre, "_catalog");
            var avecDéfi = ((int Done, int Total))BancCapture.Call(fenêtre, "PickerExerciseCount")!;

            Assert.Equal(sansDéfi, avecDéfi); // ni x ni N ne bougent avec le Défi
            Assert.Equal(catalogue.TotalExerciseCount - 2, avecDéfi.Total);
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    [Fact]
    public void Le_defi_commun_deja_fait_aujourd_hui_le_dit_avant_la_frappe()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.SetTrainingSequenceIndex(DailyChallenge.SequenceLength);
        var fenêtre = Ouvrir();
        try
        {
            JouerLeDéfi(fenêtre, 0);
            BancCapture.Call(fenêtre, "ReplayExercise");
            Assert.DoesNotContain(L.Challenge_AlreadyDone, (string)BancCapture.Call(fenêtre, "BuildTypingStatus")!);

            JouerLeDéfi(fenêtre, DailyChallenge.ExtractExerciseIndex); // la séance du jour est comptée
            BancCapture.Call(fenêtre, "ReplayExercise");
            Assert.StartsWith(L.Challenge_AlreadyDone, (string)BancCapture.Call(fenêtre, "BuildTypingStatus")!);
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    [Fact]
    public void Pendant_la_prise_en_main_seul_le_rejeu_est_deja_compte()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.SetTrainingEnabled(true);
        var jouée = new ChallengeSessionState();
        jouée.LoadCatalog();
        Assert.False(jouée.AlreadyCounted);

        jouée.Complete(new LessonAttemptStats(), previousBestWpm: null);
        Assert.True(jouée.AlreadyCounted); // « Rejouer » : même module, étape déjà dépassée

        var suivante = new ChallengeSessionState();
        suivante.LoadCatalog();
        Assert.False(suivante.AlreadyCounted); // l'étape suivante compte, même le même jour
    }

    [Fact]
    public void Reinitialiser_la_progression_efface_aussi_le_record_du_Defi()
    {
        using var isolation = new BancCapture.Isolation();
        ÉcrireLeDéfiDHier(vitesseÉchauffement: 90, vitesseExtrait: 57);
        ConfigManager.SetTrainingBestWpm(60);
        var fenêtre = Ouvrir();
        try
        {
            BancCapture.Call(fenêtre, "ApplyProgressReset");

            Assert.Equal(0u, ConfigManager.TrainingBestWpm);
            Assert.Null(ChallengeSessionState.PersonalBestWpm(BancCapture.Field<LessonProgressStore>(fenêtre, "_progress")));
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("en")]
    public void La_confirmation_dit_que_les_records_et_la_carte_sont_effaces(string langue)
    {
        using var isolation = new BancCapture.Isolation();
        L.Language = langue;
        Assert.Contains(L.LessonsHome_YourMap, L.LessonsWin_ResetProgressConfirm);
        Assert.Contains(langue == "fr" ? "records" : "best scores", L.LessonsWin_ResetProgressConfirm);
    }

    private static List<string> CiblesDessinées(LessonsWindow fenêtre)
    {
        IntPtr hwnd = BancCapture.Handle(fenêtre);
        Win32.InvalidateRect(hwnd, IntPtr.Zero, false);
        Win32.SendMessageW(hwnd, Win32.WM_PAINT, IntPtr.Zero, IntPtr.Zero);
        return BancCapture.Field<List<LessonControlView>>(fenêtre, "_controlViews").Select(c => c.Name).ToList();
    }

    /// <summary>L13 : Paramètres ouverts, la liste des leçons et ‹ › changeaient l'exercice
    /// caché derrière le panneau. L'en-tête ne garde que les liens de droite.</summary>
    [Fact]
    public void Parametres_ouverts_l_en_tete_n_a_ni_liste_ni_chevrons()
    {
        using var isolation = new BancCapture.Isolation();
        var fenêtre = Ouvrir();
        try
        {
            var catalogue = BancCapture.Field<LessonCatalog>(fenêtre, "_catalog");
            var module = catalogue.Modules.Where(m => m.Id != DailyChallenge.ModuleId).ElementAt(1);
            Assert.True((bool)BancCapture.Call(fenêtre, "SelectExercise", module.Id, module.Lessons[0].Id, 1, false)!);
            string liste = $"{module.Title} {module.Lessons[0].Title}";

            var avant = CiblesDessinées(fenêtre);
            Assert.Contains(liste, avant);
            Assert.Contains(L.LessonsWin_IconPrevious, avant);
            Assert.Contains(L.LessonsWin_IconNext, avant);

            BancCapture.Call(fenêtre, "ToggleSettings");
            var ouverts = CiblesDessinées(fenêtre);
            Assert.DoesNotContain(liste, ouverts);
            Assert.DoesNotContain(L.LessonsWin_IconPrevious, ouverts);
            Assert.DoesNotContain(L.LessonsWin_IconNext, ouverts);
            Assert.Contains(L.LessonsWin_Title, ouverts); // le lien qui ramène à l'exercice
            Assert.Equal(1, BancCapture.Field<int>(fenêtre, "_exerciseIndex"));

            BancCapture.Call(fenêtre, "ToggleSettings");
            Assert.Contains(liste, CiblesDessinées(fenêtre));
        }
        finally { BancCapture.Teardown(fenêtre); }
    }
}
