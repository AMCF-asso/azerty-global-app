using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit de régression du 10/11 (1.3.0), fenêtre des Leçons : RA5 (pause : la navigation passe,
/// pas la frappe), RA8 (écran des usages : le focus de l'ouverture ne désigne plus une case sans
/// repère), RA12 (langue changée à chaud : titre de la fenêtre, séance composée, séance du Défi),
/// RA6 (bilan exposé au lecteur d'écran) et RA7 (faute de frappe annoncée).
/// </summary>
public class AuditRegression1011LeconsTests
{
    private const int VK_BACK = 0x08, VK_TAB = 0x09, VK_RETURN = 0x0D, VK_ESCAPE = 0x1B, VK_SPACE = 0x20;

    private static LessonsWindow Ouvrir()
    {
        var layout = LayoutLoader.LoadFromResource();
        var mapper = new KeyMapper(layout, new MockWin32Api());
        var hook = new KeyboardHook(mapper); // jamais installé
        ConfigManager.ClearWindowBounds(ConfigManager.LessonsWindowBoundsKey);
        return new LessonsWindow(layout, mapper, hook);
    }

    private static void Peindre(LessonsWindow fenêtre)
    {
        IntPtr hwnd = BancCapture.Handle(fenêtre);
        Win32.InvalidateRect(hwnd, IntPtr.Zero, false);
        Win32.SendMessageW(hwnd, Win32.WM_PAINT, IntPtr.Zero, IntPtr.Zero);
    }

    private static void Envoyer(LessonsWindow fenêtre, uint message, int valeur) =>
        Win32.SendMessageW(BancCapture.Handle(fenêtre), message, (IntPtr)valeur, IntPtr.Zero);

    private static LessonTypingSession Séance(LessonsWindow fenêtre) =>
        BancCapture.Field<LessonTypingSession>(fenêtre, "_session");

    private static List<LessonControlView> Cibles(LessonsWindow fenêtre) =>
        BancCapture.Field<List<LessonControlView>>(fenêtre, "_controlViews");

    private static string Page(LessonsWindow fenêtre) =>
        BancCapture.Field<object>(fenêtre, "_homePage").ToString()!;

    private static object Énum(string type, string valeur) =>
        Enum.Parse(typeof(LessonsWindow).GetNestedType(type, System.Reflection.BindingFlags.NonPublic)
            ?? throw new MissingMemberException(nameof(LessonsWindow), type), valeur);

    private static string Titre(LessonsWindow fenêtre)
    {
        var texte = new System.Text.StringBuilder(256);
        Win32.GetWindowTextW(BancCapture.Handle(fenêtre), texte, texte.Capacity);
        return texte.ToString();
    }

    /// <summary>Un exercice ordinaire du catalogue, hors Défi, à l'écran d'exercice.</summary>
    private static void OuvrirUnExercice(LessonsWindow fenêtre)
    {
        var module = BancCapture.Field<LessonCatalog>(fenêtre, "_catalog").Modules.First(m => m.Id != DailyChallenge.ModuleId);
        Assert.True((bool)BancCapture.Call(fenêtre, "SelectExercise", module.Id, module.Lessons[0].Id, 0, false)!);
        Peindre(fenêtre);
    }

    // ── RA5 : en pause ───────────────────────────────────────────────

    [Theory]
    [InlineData(VK_ESCAPE, false, false, false)] // fermer
    [InlineData(VK_TAB, false, false, false)]    // Tab et Maj+Tab : changer de cible
    [InlineData(VK_RETURN, true, false, false)]  // Entrée sur un bouton
    [InlineData(VK_SPACE, true, true, false)]    // Espace sur un bouton, là où elle active
    [InlineData(VK_RETURN, false, false, true)]  // Entrée sans bouton focalisé : rien à activer
    [InlineData(VK_SPACE, true, false, true)]    // Espace dans un exercice : une frappe
    [InlineData(VK_BACK, true, true, true)]      // Retour arrière : la frappe de l'exercice
    [InlineData(0x41, true, true, true)]         // une lettre
    [InlineData(0x25, false, false, true)]       // flèche gauche (champ, carte)
    public void EnPause_SeuleLaNavigationPasse(int touche, bool boutonFocalisé, bool espaceActive, bool bloqué)
    {
        Assert.Equal(bloqué, LessonsWindow.BlockedWhilePaused(touche, boutonFocalisé, espaceActive));
    }

    [Fact]
    public void EnPause_TabEntreeSurUnBoutonEtEchapAgissent_PasLaFrappe()
    {
        using var isolation = new BancCapture.Isolation();
        var fenêtre = Ouvrir();
        try
        {
            OuvrirUnExercice(fenêtre);
            fenêtre.SetInputPaused(true);

            // La frappe de l'exercice reste bloquée.
            char attendu = Séance(fenêtre).GetNextExpectedCharacter()!.Value;
            Envoyer(fenêtre, Win32.WM_CHAR, attendu);
            Envoyer(fenêtre, Win32.WM_KEYDOWN, VK_BACK);
            Assert.Equal(0, Séance(fenêtre).CursorPosition);
            Assert.Equal(0, Séance(fenêtre).Stats.ProductiveKeystrokes);

            // Tab quitte la surface pour un bouton.
            Assert.Equal(-1, BancCapture.Field<int>(fenêtre, "_focusedActionIndex"));
            Envoyer(fenêtre, Win32.WM_KEYDOWN, VK_TAB);
            Assert.True(BancCapture.Field<int>(fenêtre, "_focusedActionIndex") >= 0, "Tab n'a pas déplacé le focus");

            // Entrée active le bouton focalisé.
            Peindre(fenêtre);
            int aujourdHui = Cibles(fenêtre).FindIndex(c => c.Name == L.LessonsHome_Today);
            Assert.True(aujourdHui >= 0, "lien « Aujourd'hui » absent de l'en-tête");
            BancCapture.SetField(fenêtre, "_focusedActionIndex", aujourdHui);
            Assert.Equal("Exercise", Page(fenêtre));
            Envoyer(fenêtre, Win32.WM_KEYDOWN, VK_RETURN);
            Assert.Equal("Today", Page(fenêtre));

            // Échap referme ce qui est ouvert.
            BancCapture.Call(fenêtre, "ToggleSettings");
            Assert.True(BancCapture.Field<bool>(fenêtre, "_settingsOpen"));
            Envoyer(fenêtre, Win32.WM_KEYDOWN, VK_ESCAPE);
            Assert.False(BancCapture.Field<bool>(fenêtre, "_settingsOpen"));

            // Pause levée : la frappe reprend.
            fenêtre.SetInputPaused(false);
            OuvrirUnExercice(fenêtre);
            Envoyer(fenêtre, Win32.WM_CHAR, Séance(fenêtre).GetNextExpectedCharacter()!.Value);
            Assert.Equal(1, Séance(fenêtre).CursorPosition);
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    // ── RA8 : écran des usages ───────────────────────────────────────

    [Fact]
    public void Usages_LeFocusDeLOuvertureVaAContinuer_PasAUneCase()
    {
        using var isolation = new BancCapture.Isolation();
        var fenêtre = Ouvrir();
        try
        {
            fenêtre.BeginTutorial();
            Peindre(fenêtre);

            int focus = BancCapture.Field<int>(fenêtre, "_focusedActionIndex");
            var cibles = Cibles(fenêtre);
            Assert.InRange(focus, 0, cibles.Count - 1);
            Assert.Equal(L.Learning_BtnContinue, cibles[focus].Name);
            Assert.NotEqual(LessonControlKind.CheckBox, cibles[focus].Kind);

            // Espace sans Tab : aucune case ne change, le parcours continue.
            bool[] avant = (bool[])BancCapture.Field<bool[]>(fenêtre, "_tutorialUsages").Clone();
            BancCapture.Call(fenêtre, "OnKeyDown", VK_SPACE);
            Assert.Equal(avant, BancCapture.Field<bool[]>(fenêtre, "_tutorialUsages"));
            Assert.NotNull(BancCapture.Field<TutorialSteps.RouteStep[]?>(fenêtre, "_tutorialRoute"));
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    // ── RA12 : langue changée à chaud ────────────────────────────────

    private static void ChangerDeLangue(LessonsWindow fenêtre, string langue)
    {
        L.Language = langue; // comme les Paramètres, avant SetAppLanguage
        BancCapture.Call(fenêtre, "RefreshLanguage");
    }

    [Fact]
    public void LangueChangee_LeTitreDeLaFenetreSuit_DansLeParcoursAussi()
    {
        using var isolation = new BancCapture.Isolation();
        isolation.SetLanguage("fr");
        var fenêtre = Ouvrir();
        try
        {
            Assert.Equal("AZERTY Global — Leçons", Titre(fenêtre));
            ChangerDeLangue(fenêtre, "en");
            Assert.Equal("AZERTY Global — Lessons", Titre(fenêtre));

            fenêtre.BeginTutorial();
            Assert.Equal("AZERTY Global — Getting started", Titre(fenêtre));
            ChangerDeLangue(fenêtre, "fr");
            Assert.Equal("AZERTY Global — Prise en main", Titre(fenêtre));
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    [Fact]
    public void LangueChangee_LaSeanceComposeeEstRetraduite_LaFrappeFaiteReste()
    {
        using var isolation = new BancCapture.Isolation();
        isolation.SetLanguage("fr");
        var fenêtre = Ouvrir();
        try
        {
            BancCapture.Call(fenêtre, "HomeShowPage", Énum("HomePage", "Text"));
            Peindre(fenêtre);
            foreach (char c in "abc def")
                BancCapture.Call(fenêtre, "OnChar", c);
            BancCapture.Call(fenêtre, "HomeStartText");
            Assert.True(fenêtre.HomeIsComposedSession);
            var séance = Séance(fenêtre);
            BancCapture.Call(fenêtre, "OnChar", séance.GetNextExpectedCharacter()!.Value);
            string titreFrançais = ((LessonModule)BancCapture.Call(fenêtre, "get_CurrentModule")!).Title;
            string consigneFrançaise = ((LessonExercise)BancCapture.Call(fenêtre, "get_CurrentExercise")!).Instruction;

            ChangerDeLangue(fenêtre, "en");

            var module = (LessonModule)BancCapture.Call(fenêtre, "get_CurrentModule")!;
            var exercice = (LessonExercise)BancCapture.Call(fenêtre, "get_CurrentExercise")!;
            Assert.Equal(L.LessonsHome_YourText, module.Title);
            Assert.Equal(L.LessonsHome_YourText, module.Lessons[0].Title);
            Assert.Equal(L.LessonsHome_TextInstruction, exercice.Instruction);
            Assert.NotEqual(titreFrançais, module.Title);
            Assert.NotEqual(consigneFrançaise, exercice.Instruction);
            // Même texte, même clé : la séance continue, la frappe faite reste.
            Assert.Same(séance, Séance(fenêtre));
            Assert.Equal(séance.Exercise.StableKey, exercice.StableKey);
            Assert.Equal(1, séance.CursorPosition);
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    [Fact]
    public void LangueChangee_LeDefiRechargeRelanceLaSeance_SiLExerciceAChange()
    {
        using var isolation = new BancCapture.Isolation();
        isolation.SetLanguage("fr");
        var fenêtre = Ouvrir();
        try
        {
            // Défi de la prise en main (étape 0), extrait entamé.
            BancCapture.Field<ChallengeSessionState>(fenêtre, "_challenge").Request();
            BancCapture.Call(fenêtre, "RefreshChallengeModuleIfStale");
            var défi = BancCapture.Field<LessonCatalog>(fenêtre, "_catalog").Modules.Single(m => m.Id == DailyChallenge.ModuleId);
            Assert.True((bool)BancCapture.Call(fenêtre, "SelectExercise", DailyChallenge.ModuleId, défi.Lessons[0].Id,
                DailyChallenge.ExtractExerciseIndex, false)!);
            for (int i = 0; i < 3; i++)
                BancCapture.Call(fenêtre, "OnChar", Séance(fenêtre).GetNextExpectedCharacter()!.Value);
            var entamée = Séance(fenêtre);
            Assert.Equal(3, entamée.CursorPosition);

            // Le Défi du moment n'est plus le même (ici l'étape ; en vrai, minuit passé) : le
            // changement de langue recharge le catalogue.
            ConfigManager.SetTrainingSequenceIndex(DailyChallenge.SequenceLength);
            ChangerDeLangue(fenêtre, "en");

            var affiché = (LessonExercise)BancCapture.Call(fenêtre, "get_CurrentExercise")!;
            Assert.Equal(DailyChallenge.ModuleId, affiché.ModuleId);
            Assert.NotEqual(entamée.Exercise.StableKey, affiché.StableKey); // sinon le test ne prouve rien
            Assert.NotSame(entamée, Séance(fenêtre));
            Assert.Equal(affiché.StableKey, Séance(fenêtre).Exercise.StableKey);
            Assert.Equal(0, Séance(fenêtre).CursorPosition);
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    [Fact]
    public void LangueChangee_MemeExercice_LaSeanceDuCatalogueContinue()
    {
        using var isolation = new BancCapture.Isolation();
        isolation.SetLanguage("fr");
        var fenêtre = Ouvrir();
        try
        {
            OuvrirUnExercice(fenêtre);
            var séance = Séance(fenêtre);
            BancCapture.Call(fenêtre, "OnChar", séance.GetNextExpectedCharacter()!.Value);

            ChangerDeLangue(fenêtre, "en");

            Assert.Same(séance, Séance(fenêtre));
            Assert.Equal(1, séance.CursorPosition);
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    // ── RA6 et RA7 : lecteur d'écran ─────────────────────────────────

    /// <summary>Branche un fournisseur UIA comme le ferait un lecteur d'écran (WM_GETOBJECT), la
    /// fenêtre au premier plan, et relève les événements décidés.</summary>
    private static (LessonsAutomation Automation, List<(string Kind, string? Text)> Événements) Écouter(LessonsWindow fenêtre)
    {
        var événements = new List<(string, string?)>();
        var automation = new LessonsAutomation(fenêtre) { Observer = (kind, text) => événements.Add((kind, text)) };
        BancCapture.SetField(fenêtre, "_automation", automation);
        BancCapture.SetField(fenêtre, "_hasFocus", true);
        return (automation, événements);
    }

    private static LessonSurfaceView? Surface(LessonsWindow fenêtre) =>
        BancCapture.Field<LessonSurfaceView?>(fenêtre, "_surfaceView");

    private static void TaperJusquAuBilan(LessonsWindow fenêtre)
    {
        for (int garde = 0; garde < 5000 && !BancCapture.Field<bool>(fenêtre, "_showSummary"); garde++)
            BancCapture.Call(fenêtre, "OnChar", Séance(fenêtre).GetNextExpectedCharacter()!.Value);
        Assert.True(BancCapture.Field<bool>(fenêtre, "_showSummary"));
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("en")]
    public void Bilan_DesLecons_LaSurfaceDitLeTitreEtLesChiffres_LeLecteurEstPrevenu(string langue)
    {
        using var isolation = new BancCapture.Isolation();
        isolation.SetLanguage(langue);
        var fenêtre = Ouvrir();
        try
        {
            OuvrirUnExercice(fenêtre);
            Assert.False(fenêtre.IsTutorialActive);
            var (automation, événements) = Écouter(fenêtre);
            Peindre(fenêtre);
            automation.RaiseChanges();
            Assert.Equal(L.LessonsWin_AutomationLineName, Surface(fenêtre)!.Name);
            événements.Clear();

            TaperJusquAuBilan(fenêtre);
            Peindre(fenêtre);
            automation.RaiseChanges();

            var bilan = Surface(fenêtre);
            Assert.NotNull(bilan);
            Assert.Equal(L.LessonsWin_ExerciseSuccess, bilan!.Name);
            var stats = BancCapture.Field<LessonAttemptStats>(fenêtre, "_lastCompletedStats");
            Assert.Contains(L.LessonsAutomation_Metric(L.LessonsWin_MetricSpeed, ""), bilan.Text);
            Assert.Contains(L.LessonsAutomation_Metric(L.LessonsWin_MetricAccuracy, ""), bilan.Text);
            Assert.Contains(L.LessonsAutomation_Metric(L.LessonsWin_MetricErrors, stats.ErrorCount.ToString()) + ".", bilan.Text);
            // La surface garde le focus, hors du cycle de Tab : l'événement de focus la fait relire.
            ILessonsAutomationHost hôte = fenêtre;
            Assert.True(hôte.SurfaceHasFocus);
            Assert.Contains(("focus", L.LessonsWin_ExerciseSuccess), événements);

            // Réglage « afficher le bilan » coupé : le titre reste dit, pas les chiffres cachés.
            ConfigManager.SetLessonSummaryVisible(false);
            Peindre(fenêtre);
            Assert.Equal(L.LessonsWin_ExerciseSuccess, Surface(fenêtre)!.Name);
            Assert.Equal("", Surface(fenêtre)!.Text);
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    [Fact]
    public void Bilan_DuParcours_LaSurfaceDitLEncouragementEtLExplication_SansChiffres()
    {
        using var isolation = new BancCapture.Isolation();
        isolation.SetLanguage("fr");
        var fenêtre = Ouvrir();
        try
        {
            fenêtre.BeginTutorial();
            BancCapture.Call(fenêtre, "StartTutorialRoute");
            Assert.True(fenêtre.IsTutorialActive);
            var (automation, événements) = Écouter(fenêtre);
            Peindre(fenêtre);
            automation.RaiseChanges();
            événements.Clear();

            TaperJusquAuBilan(fenêtre);
            Peindre(fenêtre);
            automation.RaiseChanges();

            var bilan = Surface(fenêtre);
            Assert.NotNull(bilan);
            Assert.Equal(L.LessonsWin_ExerciseSuccess, bilan!.Name);
            Assert.StartsWith(L.LessonsWin_TutorialEncouragement, bilan.Text);
            Assert.Contains(L.LessonsPaper_WhyHereLabel, bilan.Text);
            Assert.DoesNotContain(L.LessonsWin_MetricSpeed, bilan.Text);
            Assert.Contains(("focus", L.LessonsWin_ExerciseSuccess), événements);
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("en")]
    public void Faute_LeLecteurDEcranLEntend_AvecLaToucheTapeeEtLAttendue(string langue)
    {
        using var isolation = new BancCapture.Isolation();
        isolation.SetLanguage(langue);
        var fenêtre = Ouvrir();
        try
        {
            OuvrirUnExercice(fenêtre);
            var (_, événements) = Écouter(fenêtre);
            char attendu = Séance(fenêtre).GetNextExpectedCharacter()!.Value;
            char faux = attendu == '#' ? '@' : '#';

            BancCapture.Call(fenêtre, "OnChar", faux);

            var annonces = événements.Where(e => e.Kind == "notification").ToList();
            var annonce = Assert.Single(annonces);
            bool retourArrière = Séance(fenêtre).NeedsBackspaceCorrection;
            Assert.Equal(LessonsWindow.TypingErrorAnnouncement(faux, attendu, retourArrière), annonce.Text);
            Assert.Contains(L.LessonsPaper_CharacterFallback(faux.ToString()), annonce.Text);
            Assert.Contains(L.LessonsPaper_CharacterFallback(attendu.ToString()), annonce.Text);

            // Une frappe juste n'annonce rien.
            if (retourArrière)
                BancCapture.Call(fenêtre, "OnKeyDown", VK_BACK);
            événements.Clear();
            BancCapture.Call(fenêtre, "OnChar", attendu);
            Assert.DoesNotContain(événements, e => e.Kind == "notification");
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    [Theory]
    [InlineData("fr", "Faute : ", " au lieu de espace insécable. Retour arrière pour corriger.")]
    [InlineData("en", "Mistake: ", " instead of no-break space. Press Backspace to fix.")]
    public void Faute_LesEspacesSeDisentParLeurNom_LaSuiteDependDuMode(string langue, string début, string fin)
    {
        using var isolation = new BancCapture.Isolation();
        isolation.SetLanguage(langue);
        Assert.Equal(début + L.LessonsPaper_CharacterFallback("a") + fin,
            LessonsWindow.TypingErrorAnnouncement('a', ' ', backspaceFixes: true));
        Assert.EndsWith(L.LessonsAutomation_TypeAgain, LessonsWindow.TypingErrorAnnouncement('a', ' ', backspaceFixes: false));
        Assert.Contains(L.LessonsAutomation_SpaceName, LessonsWindow.TypingErrorAnnouncement('a', ' ', backspaceFixes: false));
        Assert.Contains(L.LessonsAutomation_NarrowNbspName, LessonsWindow.TypingErrorAnnouncement(' ', null, backspaceFixes: true));
    }
}
