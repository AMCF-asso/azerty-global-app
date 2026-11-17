using System.Reflection;
using System.Runtime.InteropServices;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Un test par règle du module M6 (fenêtre Leçons) de docs/machines-a-etats.md (app 1.3.0). Le nom
/// de chaque méthode commence par l'id de la règle. Verdict : vert si le produit tient la règle ;
/// rouge si elle n'est pas tenue (le test échoue alors sur sa dernière assertion, les témoins
/// avant). La vraie fenêtre est créée mais jamais affichée par Show() : l'état « visible » se pose
/// par réflexion, comme dans FocusAndStateTests. Les minuteries se sondent par KillTimer, qui rend
/// faux quand la minuterie n'existe pas ; le sondage détruit la minuterie, d'où les réarmements.
/// </summary>
internal static class OutilsLecons130
{
    private const BindingFlags Statique = BindingFlags.Static | BindingFlags.NonPublic;

    internal static (LessonsWindow Fenetre, MockWin32Api Api) OuvrirAvecApi()
    {
        var layout = LayoutLoader.LoadFromResource();
        var api = new MockWin32Api();
        var mapper = new KeyMapper(layout, api);
        var hook = new KeyboardHook(mapper); // jamais installé
        ConfigManager.ClearWindowBounds(ConfigManager.LessonsWindowBoundsKey);
        return (new LessonsWindow(layout, mapper, hook), api);
    }

    internal static LessonsWindow Ouvrir() => OuvrirAvecApi().Fenetre;

    internal static LessonTypingSession Seance(LessonsWindow fenetre) =>
        BancCapture.Field<LessonTypingSession>(fenetre, "_session");

    /// <summary>Premier exercice de la deuxième leçon ordinaire : ni Initiation, ni Défi.</summary>
    internal static (string Module, string Lecon) ModuleOrdinaire(LessonsWindow fenetre, int rang = 1)
    {
        var catalogue = BancCapture.Field<LessonCatalog>(fenetre, "_catalog");
        var module = catalogue.Modules.Where(m => m.Id != DailyChallenge.ModuleId).ElementAt(rang);
        return (module.Id, module.Lessons[0].Id);
    }

    internal static void ChoisirOrdinaire(LessonsWindow fenetre, int exercice = 0)
    {
        var (module, lecon) = ModuleOrdinaire(fenetre);
        Assert.True((bool)BancCapture.Call(fenetre, "SelectExercise", module, lecon, exercice, false)!);
    }

    internal static void ChoisirInitiation(LessonsWindow fenetre) =>
        Assert.True((bool)BancCapture.Call(fenetre, "SelectExercise", LessonCatalogLoader.InitiationModuleId,
            LessonCatalogLoader.InitiationLessonId, 0, false)!);

    internal static void TaperUnCaractere(LessonsWindow fenetre)
    {
        char? suivant = Seance(fenetre).GetNextExpectedCharacter();
        Assert.True(suivant is char, "séance bloquée avant le caractère à taper");
        BancCapture.Call(fenetre, "OnChar", suivant!.Value);
    }

    internal static void TaperJusquAuBilan(LessonsWindow fenetre)
    {
        for (int garde = 0; garde < 5000 && !BancCapture.Field<bool>(fenetre, "_showSummary"); garde++)
            TaperUnCaractere(fenetre);
        Assert.True(BancCapture.Field<bool>(fenetre, "_showSummary"));
    }

    internal static void Mode(LessonsWindow fenetre, string mode)
    {
        var type = typeof(LessonsWindow).GetNestedType("WindowMode", BindingFlags.NonPublic)!;
        BancCapture.SetField(fenetre, "_mode", Enum.Parse(type, mode));
    }

    internal static void Page(LessonsWindow fenetre, string page)
    {
        var type = typeof(LessonsWindow).GetNestedType("HomePage", BindingFlags.NonPublic)!;
        BancCapture.SetField(fenetre, "_homePage", Enum.Parse(type, page));
    }

    /// <summary>Séance composée de l'accueil (révision, touche, texte), sur l'écran d'exercice.</summary>
    internal static void DemarrerComposee(LessonsWindow fenetre)
    {
        var (module, lecon) = ModuleOrdinaire(fenetre);
        var catalogue = BancCapture.Field<LessonCatalog>(fenetre, "_catalog");
        var source = catalogue.Modules.Single(m => m.Id == module).Lessons.Single(l => l.Id == lecon).Exercises[0];
        BancCapture.Call(fenetre, "HomeStartComposed", source,
            (Func<(string Module, string Lesson, string Instruction)>)(() => ("Révision", "Révision", source.Instruction)),
            (IReadOnlyList<string>)new[] { "a" });
        Assert.NotNull(BancCapture.Field<object?>(fenetre, "_homeComposed"));
    }

    internal static uint Minuterie(string nom) =>
        (uint)typeof(LessonsWindow).GetField(nom, Statique)!.GetRawConstantValue()!;

    /// <summary>Vrai si la minuterie existait ; la détruit (témoin ou preuve selon le moment).</summary>
    internal static bool Sonder(LessonsWindow fenetre, string nom)
    {
        IntPtr hwnd = BancCapture.Handle(fenetre);
        Assert.NotEqual(IntPtr.Zero, hwnd);
        return Win32.KillTimer(hwnd, (UIntPtr)Minuterie(nom));
    }
}

/// <summary>Séance de frappe (M6.1) et mode de fenêtre et bilan (M6.2).</summary>
public sealed class Regles130LeconsSeanceTests
{
    // M6.1 I1 : une frappe n'est jamais prise en compte hors de l'état EnCours. Deux états hors
    // EnCours : LigneTerminée (ligne 1 finie sans avance) et ExerciceTerminé ; dans chacun, la
    // frappe est refusée et ni les statistiques ni la position ne bougent.
    [Fact]
    public void M6_1_I1_FrappeHorsEnCours_EstIgnoree()
    {
        var souple = new LessonExercise("m", "l", 0, "practice", "Tape", "ab\ncd", LessonTypingMode.Flexible);
        var ligne = new LessonTypingSession(souple);
        ligne.TypeChar('a');
        Assert.True(ligne.TypeChar('b').LineCompleted);                          // témoin : on est en LigneTerminée
        Assert.True(ligne.IsLineComplete);
        Assert.False(ligne.IsExerciseComplete);
        var avantLigne = (ligne.Stats.ProductiveKeystrokes, ligne.Stats.CorrectKeystrokes, ligne.Stats.ErrorCount,
            ligne.CursorPosition, ligne.LineIndex);
        foreach (char c in new[] { 'x', 'c', ' ' })
            Assert.False(ligne.TypeChar(c).Accepted, $"LigneTerminée : '{c}' acceptée");

        var strict = new LessonExercise("m", "l", 1, "practice", "Tape", "ab", LessonTypingMode.Strict);
        var fin = new LessonTypingSession(strict);
        fin.TypeChar('a');
        Assert.True(fin.TypeChar('b').ExerciseCompleted);                        // témoin : on est en ExerciceTerminé
        Assert.True(fin.IsExerciseComplete);
        var avantFin = (fin.Stats.ProductiveKeystrokes, fin.Stats.CorrectKeystrokes, fin.Stats.ErrorCount,
            fin.CursorPosition, fin.LineIndex);
        foreach (char c in new[] { 'x', 'a', ' ' })
            Assert.False(fin.TypeChar(c).Accepted, $"ExerciceTerminé : '{c}' acceptée");

        Assert.Equal(avantLigne, (ligne.Stats.ProductiveKeystrokes, ligne.Stats.CorrectKeystrokes, ligne.Stats.ErrorCount,
            ligne.CursorPosition, ligne.LineIndex));
        Assert.Equal(avantFin, (fin.Stats.ProductiveKeystrokes, fin.Stats.CorrectKeystrokes, fin.Stats.ErrorCount,
            fin.CursorPosition, fin.LineIndex));
    }

    // M6.2 I1 : CompleteExercise ne s'exécute qu'une fois par séance. Un second appel (même séance,
    // bilan déjà affiché) n'enregistre pas une seconde réussite dans le magasin.
    [Fact]
    public void M6_2_I1_CompleteExercise_NeS_ExecuteQuUneFoisParSeance()
    {
        using var iso = new BancCapture.Isolation();
        var fenetre = OutilsLecons130.Ouvrir();
        try
        {
            OutilsLecons130.ChoisirOrdinaire(fenetre);
            OutilsLecons130.TaperJusquAuBilan(fenetre);
            var exercice = (LessonExercise)BancCapture.Call(fenetre, "get_CurrentExercise")!;
            var magasin = BancCapture.Field<LessonProgressStore>(fenetre, "_progress");
            Assert.Equal(1, magasin.GetValidProgress(exercice)!.SuccessfulAttempts);   // témoin : la séance a bien compté

            BancCapture.Call(fenetre, "CompleteExercise");

            Assert.Equal(1, magasin.GetValidProgress(exercice)!.SuccessfulAttempts);
        }
        finally { BancCapture.Teardown(fenetre); }
    }

    // M6.2 I2 : toute nouvelle séance repart de StartCurrentSession : « Rejouer », GoTo (Suivant)
    // et SelectExercise donnent une séance neuve, au curseur 0, sans bilan.
    [Fact]
    public void M6_2_I2_NouvelleSeance_RepartDeStartCurrentSession()
    {
        using var iso = new BancCapture.Isolation();
        var fenetre = OutilsLecons130.Ouvrir();
        try
        {
            OutilsLecons130.ChoisirOrdinaire(fenetre);
            var (module, lecon) = OutilsLecons130.ModuleOrdinaire(fenetre);
            int m = BancCapture.Field<int>(fenetre, "_moduleIndex");
            int l = BancCapture.Field<int>(fenetre, "_lessonIndex");

            // Rejouer, depuis un bilan.
            OutilsLecons130.TaperJusquAuBilan(fenetre);
            var avantRejeu = OutilsLecons130.Seance(fenetre);
            BancCapture.Call(fenetre, "ReplayExercise");
            Assert.NotSame(avantRejeu, OutilsLecons130.Seance(fenetre));
            Assert.Equal(0, OutilsLecons130.Seance(fenetre).CursorPosition);
            Assert.False(BancCapture.Field<bool>(fenetre, "_showSummary"));

            // GoTo (« Suivant »), depuis une séance entamée.
            OutilsLecons130.TaperUnCaractere(fenetre);
            Assert.Equal(1, OutilsLecons130.Seance(fenetre).CursorPosition);      // témoin : la séance est entamée
            var avantGoTo = OutilsLecons130.Seance(fenetre);
            BancCapture.Call(fenetre, "GoTo", (m, l, 1));
            Assert.Equal(1, BancCapture.Field<int>(fenetre, "_exerciseIndex"));
            Assert.NotSame(avantGoTo, OutilsLecons130.Seance(fenetre));
            Assert.Equal(0, OutilsLecons130.Seance(fenetre).CursorPosition);
            Assert.False(BancCapture.Field<bool>(fenetre, "_showSummary"));

            // SelectExercise, depuis un bilan.
            OutilsLecons130.TaperJusquAuBilan(fenetre);
            var avantSelection = OutilsLecons130.Seance(fenetre);
            Assert.True((bool)BancCapture.Call(fenetre, "SelectExercise", module, lecon, 0, false)!);
            Assert.NotSame(avantSelection, OutilsLecons130.Seance(fenetre));
            Assert.Equal(0, OutilsLecons130.Seance(fenetre).CursorPosition);
            Assert.False(BancCapture.Field<bool>(fenetre, "_showSummary"));
        }
        finally { BancCapture.Teardown(fenetre); }
    }
}

/// <summary>Parcours de l'accueil (M6.3), accueil (M6.4), indices (M6.5), mode Libre (M6.6) et fenêtre (M6.7).</summary>
public sealed class Regles130LeconsFenetreTests
{
    private static readonly FieldInfo ProfondeurExclue =
        typeof(UsageStats).GetField("_excludedTypingDepth", BindingFlags.Static | BindingFlags.NonPublic)!;

    // M6.3 I1 : EndTutorial est idempotent. Une plage d'exclusion ouverte hors de la fenêtre
    // (profondeur 1) doit survivre à deux EndTutorial : le second ne referme pas la plage d'autrui.
    [Fact]
    public void M6_3_I1_EndTutorial_EstIdempotent()
    {
        using var iso = new BancCapture.Isolation();
        object profondeurAvant = ProfondeurExclue.GetValue(null)!;
        UsageStats.BeginExcludedTyping();                                         // plage d'un appelant extérieur
        var fenetre = OutilsLecons130.Ouvrir();
        int fermetures = 0;
        fenetre.TutorialClosed = _ => fermetures++;
        try
        {
            fenetre.BeginTutorial();
            Assert.True(BancCapture.Field<bool>(fenetre, "_tutorialExcludesTyping"));   // témoin : le parcours a sa plage
            Assert.True(fenetre.IsTutorialActive);

            BancCapture.Call(fenetre, "EndTutorial", false);
            Assert.False(fenetre.IsTutorialActive);
            Assert.Equal(1, fermetures);
            Assert.True(UsageStats.IsTypingExcluded);                                   // témoin : la plage extérieure reste

            BancCapture.Call(fenetre, "EndTutorial", false);

            Assert.Equal(1, fermetures);
            Assert.True(UsageStats.IsTypingExcluded, "M6.3 I1 : le second EndTutorial a refermé la plage d'exclusion d'un autre");
        }
        finally
        {
            BancCapture.Teardown(fenetre);
            ProfondeurExclue.SetValue(null, profondeurAvant);                           // statique partagé : jamais laissé modifié
        }
    }

    // M6.3 I2 : fermer sur l'écran final vaut réussite (TutorialClosed(true)) ; fermer ailleurs
    // dans le parcours ne vaut pas réussite.
    [Fact]
    public void M6_3_I2_FermerSurLEcranFinal_ValaitReussite()
    {
        using var iso = new BancCapture.Isolation();
        var fenetre = OutilsLecons130.Ouvrir();
        bool? clos = null;
        fenetre.TutorialClosed = reussi => clos = reussi;
        try
        {
            // Témoin : fermer sur l'écran des usages n'est pas une réussite.
            fenetre.BeginTutorial();
            BancCapture.Call(fenetre, "Hide");
            Assert.False(clos);

            clos = null;
            fenetre.BeginTutorial();
            BancCapture.Call(fenetre, "StartTutorialRoute");
            foreach (var etape in BancCapture.Field<TutorialSteps.RouteStep[]>(fenetre, "_tutorialRoute")!)
                ConfigManager.RecordLearningExerciseCompleted(etape.Index);
            BancCapture.Call(fenetre, "AdvanceTutorialRoute");
            Assert.Equal("Final", BancCapture.Field<object>(fenetre, "_tutorialPage")!.ToString());
            Assert.Null(clos);                                                          // témoin : rien n'est encore clos

            BancCapture.Call(fenetre, "Hide");

            Assert.True(clos, "M6.3 I2 : la fermeture sur l'écran final n'a pas valu réussite");
        }
        finally { BancCapture.Teardown(fenetre); }
    }

    // M6.4 I2 : changer de module ou de leçon sort de la séance composée.
    [Fact]
    public void M6_4_I2_ChangerDeModuleOuDeLecon_SortDeLaSeanceComposee()
    {
        using var iso = new BancCapture.Isolation();
        var fenetre = OutilsLecons130.Ouvrir();
        try
        {
            Assert.True(BancCapture.Field<LessonCatalog>(fenetre, "_catalog").Modules.Count > 2);
            OutilsLecons130.ChoisirOrdinaire(fenetre);

            OutilsLecons130.DemarrerComposee(fenetre);                                  // témoin dans l'assistant : composée non nulle
            BancCapture.Call(fenetre, "SelectModule", 2);
            Assert.Null(BancCapture.Field<object?>(fenetre, "_homeComposed"));

            OutilsLecons130.ChoisirOrdinaire(fenetre);
            OutilsLecons130.DemarrerComposee(fenetre);
            BancCapture.Call(fenetre, "SelectLesson", 0);
            Assert.Null(BancCapture.Field<object?>(fenetre, "_homeComposed"));
        }
        finally { BancCapture.Teardown(fenetre); }
    }

    // M6.4 I5 : un Défi périmé efface la séance composée. NON GARANTI par le produit (doc) : test
    // de CARACTÉRISATION, vert aujourd'hui. RefreshChallengeModuleIfStale recharge le catalogue
    // mais ne touche pas _homeComposed ; la séance composée survit.
    [Fact]
    public void M6_4_I5_DefiPerime_NEffacePasLaSeanceComposee_Caracterisation()
    {
        using var iso = new BancCapture.Isolation();
        var fenetre = OutilsLecons130.Ouvrir();
        try
        {
            OutilsLecons130.ChoisirOrdinaire(fenetre);
            OutilsLecons130.DemarrerComposee(fenetre);
            var catalogueAvant = BancCapture.Field<LessonCatalog>(fenetre, "_catalog");
            BancCapture.Field<ChallengeSessionState>(fenetre, "_challenge").Invalidate();   // le Défi est périmé

            BancCapture.Call(fenetre, "RefreshChallengeModuleIfStale");

            Assert.NotSame(catalogueAvant, BancCapture.Field<LessonCatalog>(fenetre, "_catalog"));   // témoin : le rechargement a eu lieu
            Assert.NotNull(BancCapture.Field<object?>(fenetre, "_homeComposed"));                   // caractérisation : la séance composée reste
            Assert.True(fenetre.HomeIsComposedSession);
        }
        finally { BancCapture.Teardown(fenetre); }
    }

    // M6.5 I1 : Hide coupe les minuteries d'indice (effacement et clignotement).
    [Fact]
    public void M6_5_I1_Hide_CoupeLesMinuteriesDIndice()
    {
        using var iso = new BancCapture.Isolation();
        var fenetre = OutilsLecons130.Ouvrir();
        try
        {
            OutilsLecons130.ChoisirInitiation(fenetre);
            BancCapture.Call(fenetre, "ShowHintCore", false);
            Assert.True(BancCapture.Field<bool>(fenetre, "_hintButtonActive"));         // témoin : un indice est affiché
            Assert.NotNull(BancCapture.Field<object?>(fenetre, "_hintMethod"));
            Assert.True(OutilsLecons130.Sonder(fenetre, "TIMER_HINT_CLEAR"));           // témoin : les deux minuteries existent
            Assert.True(OutilsLecons130.Sonder(fenetre, "TIMER_HINT_FLASH_CLEAR"));
            BancCapture.Call(fenetre, "ShowHintCore", false);                           // le sondage les a détruites : on les réarme
            Assert.True(BancCapture.Field<bool>(fenetre, "_hintButtonActive"));

            BancCapture.Call(fenetre, "Hide");

            Assert.False(OutilsLecons130.Sonder(fenetre, "TIMER_HINT_CLEAR"), "M6.5 I1 : TIMER_HINT_CLEAR vit encore après Hide");
            Assert.False(OutilsLecons130.Sonder(fenetre, "TIMER_HINT_FLASH_CLEAR"), "M6.5 I1 : TIMER_HINT_FLASH_CLEAR vit encore après Hide");
        }
        finally { BancCapture.Teardown(fenetre); }
    }

    // M6.5 I2 : un WM_TIMER déjà posté avant KillTimer arrive après Hide ; il est sans effet nuisible
    // (aucune exception, aucune minuterie réarmée, aucun indice rallumé sur une fenêtre masquée).
    [Fact]
    public void M6_5_I2_WmTimerApresHide_EstSansEffet()
    {
        using var iso = new BancCapture.Isolation();
        ConfigManager.SetLessonAutoHints(true);
        var fenetre = OutilsLecons130.Ouvrir();
        try
        {
            OutilsLecons130.ChoisirInitiation(fenetre);
            OutilsLecons130.Page(fenetre, "Exercise");
            BancCapture.SetField(fenetre, "_visible", true);
            BancCapture.Call(fenetre, "UpdateAutoHintIfEnabled");
            Assert.NotNull(BancCapture.Field<object?>(fenetre, "_hintMethod"));         // témoin : fenêtre visible, l'indice automatique s'allume
            Assert.True(OutilsLecons130.Sonder(fenetre, "TIMER_HINT_FLASH_CLEAR"));     // témoin : sa minuterie existe

            BancCapture.Call(fenetre, "Hide");
            Assert.False(BancCapture.Field<bool>(fenetre, "_visible"));

            // TIMER_HINT_CLEAR en dernier : son gestionnaire est le seul qui puisse réarmer une minuterie
            // (indice automatique) ; le tick de clignotement, lui, détruit la sienne.
            foreach (string nom in new[] { "TIMER_HINT_FLASH_CLEAR", "TIMER_FREE_STATS", "TIMER_HINT_CLEAR" })
                BancCapture.Call(fenetre, "OnTimer", OutilsLecons130.Minuterie(nom));   // aucune exception ne doit sortir

            Assert.Null(BancCapture.Field<object?>(fenetre, "_hintMethod"));
            Assert.False(OutilsLecons130.Sonder(fenetre, "TIMER_FREE_STATS"));
            Assert.False(OutilsLecons130.Sonder(fenetre, "TIMER_HINT_CLEAR"));
            Assert.False(OutilsLecons130.Sonder(fenetre, "TIMER_HINT_FLASH_CLEAR"), "M6.5 I2 : un WM_TIMER après Hide a réarmé une minuterie d'indice");
        }
        finally { BancCapture.Teardown(fenetre); }
    }

    // M6.6 I2 : la minuterie de statistiques libres ne tourne que si mode Libre, début posé, option
    // visible, fenêtre visible et réglages fermés ; son tick ne se réarme que sous les mêmes
    // conditions ; Hide la coupe.
    [Fact]
    public void M6_6_I2_MinuterieDeStatistiquesLibres_SelonModeDebutEtOption()
    {
        using var iso = new BancCapture.Isolation();
        var fenetre = OutilsLecons130.Ouvrir();
        try
        {
            bool Armee()
            {
                BancCapture.Call(fenetre, "ArmFreeStatsTimerIfNeeded");
                return OutilsLecons130.Sonder(fenetre, "TIMER_FREE_STATS");
            }
            BancCapture.SetField(fenetre, "_visible", true);
            OutilsLecons130.Mode(fenetre, "Free");
            Assert.True(ConfigManager.LessonFreeStatsVisible);
            Assert.False(Armee(), "témoin : sans début posé, la minuterie ne tourne pas");

            BancCapture.Call(fenetre, "OnChar", 'a');                                   // première frappe
            Assert.True(BancCapture.Field<DateTimeOffset?>(fenetre, "_freeStartedAt").HasValue);
            Assert.True(OutilsLecons130.Sonder(fenetre, "TIMER_FREE_STATS"), "témoin : la première frappe arme la minuterie");
            Assert.True(Armee(), "témoin : toutes conditions vraies, la minuterie tourne");

            ConfigManager.SetLessonFreeStatsVisible(false);
            Assert.False(Armee(), "option masquée");
            ConfigManager.SetLessonFreeStatsVisible(true);

            BancCapture.SetField(fenetre, "_settingsOpen", true);
            Assert.False(Armee(), "réglages ouverts");
            BancCapture.SetField(fenetre, "_settingsOpen", false);

            OutilsLecons130.Mode(fenetre, "Lessons");
            Assert.False(Armee(), "mode Leçons");
            OutilsLecons130.Mode(fenetre, "Free");

            BancCapture.SetField(fenetre, "_visible", false);
            Assert.False(Armee(), "fenêtre masquée");
            BancCapture.SetField(fenetre, "_visible", true);

            var debut = BancCapture.Field<DateTimeOffset?>(fenetre, "_freeStartedAt");
            BancCapture.SetField(fenetre, "_freeStartedAt", null);
            Assert.False(Armee(), "début effacé");
            BancCapture.SetField(fenetre, "_freeStartedAt", debut);
            Assert.True(Armee(), "témoin : conditions rétablies, la minuterie tourne de nouveau");

            // Le tick se réarme sous les conditions, et seulement alors.
            BancCapture.Call(fenetre, "OnTimer", OutilsLecons130.Minuterie("TIMER_FREE_STATS"));
            Assert.True(OutilsLecons130.Sonder(fenetre, "TIMER_FREE_STATS"), "le tick doit réarmer la minuterie");
            Assert.True(Armee());
            ConfigManager.SetLessonFreeStatsVisible(false);
            BancCapture.Call(fenetre, "OnTimer", OutilsLecons130.Minuterie("TIMER_FREE_STATS"));
            Assert.False(OutilsLecons130.Sonder(fenetre, "TIMER_FREE_STATS"), "le tick ne doit pas réarmer option masquée");
            ConfigManager.SetLessonFreeStatsVisible(true);

            Assert.True(Armee());                                                       // témoin avant Hide
            BancCapture.Call(fenetre, "Hide");

            Assert.False(OutilsLecons130.Sonder(fenetre, "TIMER_FREE_STATS"), "M6.6 I2 : la minuterie tourne encore après Hide");
        }
        finally { BancCapture.Teardown(fenetre); }
    }

    // M6.7 I1 : Hide éteint Verr. Maj. : une touche Verr. Maj. injectée (appui puis relâchement)
    // part vers le système quand il l'a allumée.
    [Fact]
    public void M6_7_I1_Hide_EteintVerrMaj()
    {
        using var iso = new BancCapture.Isolation();
        var (fenetre, api) = OutilsLecons130.OuvrirAvecApi();
        try
        {
            const int VkCapital = 0x14;
            var layout = LayoutLoader.LoadFromResource();
            var mapperTemoin = new KeyMapper(layout, api);
            api.KeyStateScript[VkCapital] = 1;                                          // le système a Verr. Maj. allumé
            mapperTemoin.RequestCapsLockOff();
            Assert.Contains(api.AllInputs, saisie => saisie.u.ki.wVk == VkCapital);     // témoin : le banc voit l'émission
            api.SendInputCalls.Clear();
            Assert.DoesNotContain(api.AllInputs, saisie => saisie.u.ki.wVk == VkCapital);

            BancCapture.Call(fenetre, "Hide");

            Assert.Contains(api.AllInputs, saisie => saisie.u.ki.wVk == VkCapital);
        }
        finally { BancCapture.Teardown(fenetre); }
    }

    /// <summary>Hôte UI Automation minimal : une fenêtre réelle, un bouton, pas de surface.</summary>
    private sealed class HoteAutomation : ILessonsAutomationHost
    {
        public IntPtr Handle { get; set; }
        public LessonSurfaceView? SurfaceView => null;
        public IReadOnlyList<LessonControlView> Controls { get; } = new[]
        {
            new LessonControlView("Paramètres", LessonControlKind.Button,
                new Win32.RECT { left = 500, top = 10, right = 580, bottom = 40 }),
        };
        public bool WindowHasFocus => true;
        public bool SurfaceHasFocus => true;
        public int FocusedControl => -1;
        public void FocusSurface() { }
        public void FocusControl(int index) { }
        public void InvokeControl(int index, string name) { }
    }

    private static string Nom(Variant valeur)
    {
        try { return Marshal.PtrToStringBSTR(valeur.Value); }
        finally { Marshal.FreeBSTR(valeur.Value); }
    }

    // M6.7 I3 : Dispose libère les fournisseurs UI Automation : après Dispose, la surface et un
    // contrôle déjà remis à UIA répondent UIA_E_ELEMENTNOTAVAILABLE.
    [Fact]
    public void M6_7_I3_Dispose_LiberLesFournisseursUiAutomation()
    {
        IntPtr fenetre = Win32.CreateWindowExW(0, "STATIC", "", Win32.WS_POPUP, 0, 0, 10, 10,
            IntPtr.Zero, IntPtr.Zero, Win32.GetModuleHandleW(null), IntPtr.Zero);
        Assert.NotEqual(IntPtr.Zero, fenetre);
        try
        {
            var automation = new LessonsAutomation(new HoteAutomation { Handle = fenetre });
            automation.OnPainted();
            var surface = automation.Surface;
            var controle = automation.Control(0);
            Assert.Equal("Paramètres", Nom(controle.GetPropertyValue(LessonsAutomation.NamePropertyId)));   // témoin : vivants
            Assert.Equal("", Nom(surface.GetPropertyValue(LessonsAutomation.NamePropertyId)));
            const int ElementNonDisponible = unchecked((int)0x80040201);

            automation.Dispose();

            var surfaceMorte = Assert.Throws<COMException>(() => surface.GetPropertyValue(LessonsAutomation.NamePropertyId));
            Assert.Equal(ElementNonDisponible, surfaceMorte.HResult);
            var controleMort = Assert.Throws<COMException>(() => controle.GetPropertyValue(LessonsAutomation.NamePropertyId));
            Assert.Equal(ElementNonDisponible, controleMort.HResult);
        }
        finally { Win32.DestroyWindow(fenetre); }
    }
}
