using System.Reflection;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit UX du 09/11, fin du lot 8 : un nom par chose (L14, W23), le parcours de l'accueil
/// (L15), le Défi ouvert pendant le parcours (L16) et à cheval sur minuit (L17).
/// </summary>
public class Audit0911Lot8Tests
{
    private static LessonsWindow Ouvrir()
    {
        var layout = LayoutLoader.LoadFromResource();
        var mapper = new KeyMapper(layout, new MockWin32Api());
        var hook = new KeyboardHook(mapper); // jamais installé
        ConfigManager.ClearWindowBounds(ConfigManager.LessonsWindowBoundsKey);
        return new LessonsWindow(layout, mapper, hook);
    }

    private static List<string> CiblesDessinées(LessonsWindow fenêtre)
    {
        IntPtr hwnd = BancCapture.Handle(fenêtre);
        Win32.InvalidateRect(hwnd, IntPtr.Zero, false);
        Win32.SendMessageW(hwnd, Win32.WM_PAINT, IntPtr.Zero, IntPtr.Zero);
        return BancCapture.Field<List<LessonControlView>>(fenêtre, "_controlViews").Select(c => c.Name).ToList();
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

    private static string Page(LessonsWindow fenêtre) => BancCapture.Field<object>(fenêtre, "_tutorialPage").ToString()!;

    // ── L14, W23 : un nom par chose ──────────────────────────────────

    /// <summary>Toutes les chaînes sans paramètre de <see cref="L"/>, dans la langue courante.</summary>
    private static IEnumerable<(string Nom, string Texte)> Chaînes() =>
        typeof(L).GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(p => p.PropertyType == typeof(string) && p.GetIndexParameters().Length == 0 && p.Name != nameof(L.Language))
            .Select(p => (p.Name, (string?)p.GetValue(null) ?? ""));

    [Theory]
    [InlineData("fr", "mode libre")]
    [InlineData("en", "free mode")]
    [InlineData("fr", "clavier visuel")]
    [InlineData("en", "visual keyboard")]
    [InlineData("fr", "mode initiation")]
    [InlineData("en", "initiation mode")]
    [InlineData("fr", "bienvenue")]
    [InlineData("en", "welcome tour")]
    [InlineData("en", "welcome window")]
    // RA13 (QCM du 10/11) : le parcours de l'accueil s'appelle « Prise en main » / “Getting started”.
    [InlineData("fr", "initiation")]
    [InlineData("fr", "premiers pas")]
    [InlineData("fr", "parcours")]
    [InlineData("en", "first steps")]
    [InlineData("en", "walkthrough")]
    public void Un_seul_nom_par_chose(string langue, string ancienNom)
    {
        using var isolation = new BancCapture.Isolation();
        L.Language = langue;
        var restes = Chaînes().Where(c => c.Texte.Contains(ancienNom, StringComparison.OrdinalIgnoreCase)).Select(c => c.Nom).ToList();
        Assert.True(restes.Count == 0, $"« {ancienNom} » reste dans : {string.Join(", ", restes)}");
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("en")]
    public void Les_noms_partages_sont_la_meme_chaine(string langue)
    {
        using var isolation = new BancCapture.Isolation();
        L.Language = langue;
        Assert.Equal(L.LessonsPaper_FreeTyping, L.LessonsWin_FreeTitle);             // lien et titre de l'écran libre
        Assert.Equal(L.Notation_KeyboardUnderText, L.LessonsWin_ToggleKeyboard);     // le même réglage, deux fenêtres
        Assert.Equal(L.Layers_WindowTitleSuffix, L.Tray_MenuLayers);                 // le menu et la fenêtre qu'il ouvre
        Assert.Contains(L.LessonsPaper_FreeTyping.ToLowerInvariant(), L.LessonsWin_ToggleFreeStats.ToLowerInvariant());
        Assert.Contains(L.LessonsPaper_FreeTyping.ToLowerInvariant(), L.LessonsWin_BtnResetFreeStats.ToLowerInvariant());
        // Les 5 premières séances du Défi ne portent plus le nom du parcours de l'accueil.
        Assert.DoesNotContain(L.Learning_HeaderTitle, L.Challenge_SequenceLessonTitle(2, 5));
        Assert.NotEqual(L.Learning_HeaderTitle, L.Challenge_StatsOnboardingLabel);
        Assert.StartsWith(L.Challenge_StatsOnboardingLabel, L.Challenge_SequenceLessonTitle(2, 5));
        // Le produit n'est jamais écrit en dur : il suit ProductIdentity.
        Assert.Contains(ProductIdentity.DisplayName, L.Tray_MenuCompatInfo);
        Assert.Contains(ProductIdentity.DisplayName, L.Tray_TooltipSuspendedCompat);
    }

    /// <summary>RA13 (QCM du 10/11) : la fenêtre, le module et sa leçon portent le même nom ;
    /// le panneau de l'écran des usages, que le balayage ne voit pas (il prend un nombre), aussi.</summary>
    [Theory]
    [InlineData("fr", "Prise en main")]
    [InlineData("en", "Getting started")]
    public void Le_parcours_de_l_accueil_n_a_qu_un_nom(string langue, string nom)
    {
        using var isolation = new BancCapture.Isolation();
        L.Language = langue;
        Assert.Equal(nom, L.Learning_HeaderTitle);
        Assert.EndsWith(nom, L.Learning_WindowTitle);
        Assert.Equal(nom, L.Lessons_InitiationModuleTitle);
        Assert.Equal(nom, L.Lessons_InitiationLessonTitle);
        Assert.StartsWith(nom, L.Learning_PlanTitle(5));
        Assert.Contains(nom, L.Lessons_InitiationModuleDesc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(nom, L.Lessons_InitiationLessonDesc, StringComparison.OrdinalIgnoreCase);

        var module = LessonCatalogLoader.LoadFromResource().Modules.Single(m => m.Id == LessonCatalogLoader.InitiationModuleId);
        Assert.Equal(nom, module.Title);
        Assert.Equal(nom, module.Lessons.Single().Title);
    }

    /// <summary>QCM du 10/11 : « Recommencer » pendant la frappe, « Rejouer » au bilan ; les deux
    /// faisaient le même geste, côte à côte, sur le bilan.</summary>
    [Fact]
    public void Recommencer_pendant_la_frappe_Rejouer_au_bilan()
    {
        using var isolation = new BancCapture.Isolation();
        var fenêtre = Ouvrir();
        try
        {
            var module = BancCapture.Field<LessonCatalog>(fenêtre, "_catalog").Modules.Where(m => m.Id != DailyChallenge.ModuleId).ElementAt(1);
            Assert.True((bool)BancCapture.Call(fenêtre, "SelectExercise", module.Id, module.Lessons[0].Id, 0, false)!);

            var frappe = CiblesDessinées(fenêtre);
            Assert.Contains(L.LessonsWin_IconRestart, frappe);
            Assert.DoesNotContain(L.LessonsWin_Replay, frappe);

            Taper(fenêtre);
            var bilan = CiblesDessinées(fenêtre);
            Assert.Contains(L.LessonsWin_Replay, bilan);
            Assert.DoesNotContain(L.LessonsWin_IconRestart, bilan);
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    /// <summary>Le texte à partager du Défi restait dans la langue de la fin de séance.</summary>
    [Fact]
    public void Le_texte_a_partager_suit_la_langue()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.SetTrainingSequenceIndex(DailyChallenge.SequenceLength);
        var fenêtre = Ouvrir();
        try
        {
            var séance = BancCapture.Field<ChallengeSessionState>(fenêtre, "_challenge");
            séance.Request();
            BancCapture.Call(fenêtre, "RefreshChallengeModuleIfStale");
            var défi = BancCapture.Field<LessonCatalog>(fenêtre, "_catalog").Modules.Single(m => m.Id == DailyChallenge.ModuleId);
            Assert.True((bool)BancCapture.Call(fenêtre, "SelectExercise", DailyChallenge.ModuleId, défi.Lessons[0].Id,
                DailyChallenge.ExtractExerciseIndex, false)!);
            Taper(fenêtre);
            var jour = DateOnly.FromDateTime(DateTime.Now);
            Assert.StartsWith(L.Challenge_ShareTitle(jour), séance.ShareText);

            string français = séance.ShareText!;
            L.Language = "en";
            BancCapture.Call(fenêtre, "RefreshLanguage");

            Assert.NotEqual(français, séance.ShareText);
            Assert.StartsWith(L.Challenge_ShareTitle(jour), séance.ShareText); // le titre anglais
            Assert.False(séance.ShareCopied);
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    // ── L15 : le parcours de l'accueil ───────────────────────────────

    /// <summary>Au bilan d'un exercice bonus, « Passer cet exercice » restait à côté de
    /// « Suivant », qui fait la même chose.</summary>
    [Fact]
    public void Passer_cet_exercice_disparait_au_bilan()
    {
        using var isolation = new BancCapture.Isolation();
        var fenêtre = Ouvrir();
        try
        {
            fenêtre.BeginTutorial();
            BancCapture.Call(fenêtre, "ToggleTutorialUsage", 2); // Code
            BancCapture.Call(fenêtre, "ToggleTutorialUsage", 4); // Afrique
            BancCapture.Call(fenêtre, "StartTutorialRoute");
            var parcours = BancCapture.Field<TutorialSteps.RouteStep[]>(fenêtre, "_tutorialRoute");
            int bonus = Array.FindIndex(parcours, étape => étape.Skippable);
            Assert.True(bonus > 0, "le parcours n'a pas d'exercice bonus");
            for (int i = 0; i < bonus; i++) BancCapture.Call(fenêtre, "AdvanceTutorialRoute");
            Assert.Equal(bonus, BancCapture.Field<int>(fenêtre, "_tutorialRoutePos"));

            Assert.Contains(L.Learning_BtnSkip, CiblesDessinées(fenêtre));
            Taper(fenêtre);
            var bilan = CiblesDessinées(fenêtre);
            Assert.DoesNotContain(L.Learning_BtnSkip, bilan);
            Assert.Contains(L.LessonsWin_IconNext, bilan);
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    /// <summary>L'écran final n'offrait aucun retour, et son « Terminer » fermait la fenêtre
    /// alors que « Terminer » mène ailleurs à Aujourd'hui : « Fermer », et « ‹ Précédent ».</summary>
    [Fact]
    public void L_ecran_final_permet_de_revenir_et_dit_Fermer()
    {
        using var isolation = new BancCapture.Isolation();
        var fenêtre = Ouvrir();
        bool? clos = null;
        fenêtre.TutorialClosed = terminé => clos = terminé;
        try
        {
            fenêtre.BeginTutorial();
            BancCapture.Call(fenêtre, "StartTutorialRoute");
            int étapes = BancCapture.Field<TutorialSteps.RouteStep[]>(fenêtre, "_tutorialRoute").Length;
            for (int i = 0; i < étapes; i++) BancCapture.Call(fenêtre, "AdvanceTutorialRoute");
            Assert.Equal("Final", Page(fenêtre));

            var final = CiblesDessinées(fenêtre);
            string précédent = "‹ " + L.LessonsWin_IconPrevious;
            Assert.Contains(précédent, final);
            Assert.Contains(L.Learning_BtnFinish, final);
            Assert.NotEqual(L.LessonsHome_Finish, L.Learning_BtnFinish);

            BancCapture.Call(fenêtre, "PreviousExercise"); // au clavier comme au clic
            Assert.Equal("None", Page(fenêtre));
            Assert.Equal(étapes - 1, BancCapture.Field<int>(fenêtre, "_tutorialRoutePos"));
            Assert.True(fenêtre.IsTutorialActive);
            Assert.Null(clos); // le parcours n'est pas clos par le retour

            Taper(fenêtre);
            BancCapture.Call(fenêtre, "NextExercise");
            Assert.Equal("Final", Page(fenêtre));
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    // ── L16, L17 : le Défi ───────────────────────────────────────────

    /// <summary>Le Défi demandé (menu, rappel) pendant le parcours s'ouvrait dans le parcours :
    /// en-tête « Prise en main », bilan sans chiffres, frappe hors statistiques.</summary>
    [Fact]
    public void Le_Defi_demande_pendant_le_parcours_clot_le_parcours()
    {
        using var isolation = new BancCapture.Isolation();
        var fenêtre = Ouvrir();
        bool? clos = null;
        fenêtre.TutorialClosed = terminé => clos = terminé;
        try
        {
            fenêtre.BeginTutorial();
            BancCapture.Call(fenêtre, "StartTutorialRoute");
            Assert.True(fenêtre.IsTutorialActive);

            Assert.True(fenêtre.OpenChallenge());

            Assert.False(fenêtre.IsTutorialActive);
            Assert.False(clos); // l'accueil est prévenu : parcours quitté avant la fin
            Assert.False(fenêtre.TutorialSummaryIsMessageOnly);
            Assert.False(BancCapture.Field<bool>(fenêtre, "_tutorialExcludesTyping"));
            Assert.Equal(DailyChallenge.ModuleId, ((LessonModule)BancCapture.Call(fenêtre, "get_CurrentModule")!).Id);
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    /// <summary>Fenêtre restée ouverte après minuit sur le Défi de la veille : le finir datait la
    /// séance du nouveau jour, donc l'extrait d'hier comptait deux fois, ou à la place du Défi
    /// du jour. La séance est datée du jour de son module.</summary>
    [Fact]
    public void Le_Defi_de_la_veille_fini_apres_minuit_compte_pour_la_veille()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.SetTrainingEnabled(true);
        ConfigManager.SetTrainingSequenceIndex(DailyChallenge.SequenceLength);
        var hier = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);
        var veille = new ChallengeSessionState();
        veille.LoadCatalog();
        typeof(ChallengeSessionState).GetField("_moduleDate", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(veille, hier); // module construit la veille

        Assert.False(veille.AlreadyCounted);
        veille.Complete(new LessonAttemptStats(), previousBestWpm: null);
        Assert.Equal(hier.ToString("yyyy-MM-dd"), ConfigManager.TrainingLastSessionDate);
        Assert.Equal(1L, UsageStats.ChallengesCompletedCount);
        Assert.True(veille.AlreadyCounted); // « Rejouer » l'extrait d'hier ne recompte pas

        veille.Complete(new LessonAttemptStats(), previousBestWpm: null);
        Assert.Equal(1L, UsageStats.ChallengesCompletedCount);

        var jour = new ChallengeSessionState();
        jour.LoadCatalog();
        Assert.False(jour.AlreadyCounted); // le Défi du nouveau jour reste à faire
    }

    // ── W24 : le titre daté du Défi tient dans l'en-tête ─────────────

    /// <summary>"Challenge for September 10, 2026" demandait 220 px pour 198 dans l'en-tête des
    /// Leçons, et la coupe mangeait le jour : mois abrégé en anglais (QCM du 10/11).</summary>
    [Theory]
    [InlineData("fr", "Défi du 10 septembre 2026")]
    [InlineData("en", "Challenge for Sep 10, 2026")]
    public void Le_titre_du_Defi_garde_le_jour_en_entier(string langue, string attendu)
    {
        using var isolation = new BancCapture.Isolation();
        isolation.SetLanguage(langue);
        Assert.Equal(attendu, L.Challenge_DailyLessonTitle(new DateOnly(2026, 9, 10)));
    }
}
