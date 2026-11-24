using System.Reflection;
using System.Runtime.InteropServices;
using AZERTYGlobal;
using Xunit;
using Xunit.Abstractions;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit UX du 09/11, W24 : « troncature EN non vérifiée ». À la taille par défaut des fenêtres,
/// en français et en anglais, aucun libellé d'interface ne doit sortir coupé (« … »). Chaque
/// coupe passe par <see cref="TruncationProbe"/> (méthodes Ellipsize, DrawText avec
/// DT_END_ELLIPSIS) ; le test ouvre les fenêtres sans les montrer, visite leurs écrans, les
/// repeint (enfants compris) et relève chaque passage.
///
/// Audit de régression 1.3.0, RA16 : s'y ajoutent les lignes écrites sans coupe
/// (<see cref="PaperText.DrawLine"/>, qui débordent de leur cadre), le texte des contrôles
/// natifs mesuré contre leur zone cliente, trois DPI simulés (120, 144, 192) et des états
/// jamais visités ; voir <c>TroncatureLibellesTests.Controles.cs</c>.
///
/// Trois familles de textes coupés :
/// <list type="bullet">
/// <item>une chaîne de <see cref="L"/> (propriété sans paramètre) : échec ;</item>
/// <item>un contenu du catalogue (titre de module ou de leçon, consigne, texte à taper) ou un nom
/// de caractère de la recherche : il cède par conception, relevé sans échec ;</item>
/// <item>tout autre texte (chaîne de L à paramètres, texte composé : statut, touche, titre de
/// carte) : échec aussi, c'est encore de l'interface.</item>
/// </list>
/// Aucune fenêtre n'est montrée ni activée : les fenêtres sont créées cachées, rendues invisibles
/// par <see cref="BancCapture.Isolation"/>, et reçoivent leurs WM_PAINT directement.
/// </summary>
public sealed partial class TroncatureLibellesTests
{
    private readonly ITestOutputHelper _sortie;

    public TroncatureLibellesTests(ITestOutputHelper sortie) => _sortie = sortie;

    // ── Relevé ───────────────────────────────────────────────────────

    private sealed class Relevé : IDisposable
    {
        public readonly List<(string Écran, TruncationProbe.Passage Passage)> Passages = new();
        public readonly List<string> ÉcransMuets = new();
        public readonly HashSet<string> Catalogue = new(StringComparer.Ordinal);
        /// <summary>Titres du module Défi : composés par L (« Défi du 10 novembre 2026 »), pas
        /// écrits dans le catalogue.</summary>
        public readonly HashSet<string> TitresDuDéfi = new(StringComparer.Ordinal);
        /// <summary>Titre daté le plus large de l'année, posé sur le Défi (<see cref="PoserLaDateLaPlusLarge"/>).</summary>
        public string? TitreDatéLePlusLarge;
        /// <summary>DPI de la fenêtre Leçons : les marges de 2 px en dépendent.</summary>
        public int Dpi;
        /// <summary>Classes de contrôle natif rencontrées et non mesurées (RA16).</summary>
        public readonly HashSet<string> ClassesNonMesurées = new(StringComparer.OrdinalIgnoreCase);
        private string _écran = "";

        public Relevé() => TruncationProbe.Observer = passage => Passages.Add((_écran, passage));

        public void Dispose() => TruncationProbe.Observer = null;

        /// <summary>Repeint la fenêtre sous le nom <paramref name="écran"/> ; un écran qui ne passe
        /// par aucune coupe est noté : soit il n'en a pas, soit sa peinture n'a pas eu lieu.</summary>
        public void Visiter(string écran, IntPtr hwnd)
        {
            Assert.NotEqual(IntPtr.Zero, hwnd);
            _écran = écran;
            int avant = Passages.Count;
            Repeindre(hwnd);
            if (Passages.Count == avant)
                ÉcransMuets.Add(écran);
            // RA16 : le texte des contrôles natifs, que Windows coupe au bord sans « … ».
            foreach (var passage in MesurerContrôles(hwnd, ClassesNonMesurées))
                Passages.Add((écran, passage));
        }

        /// <summary>Un passage mesuré par le test lui-même, sous le nom <paramref name="écran"/>.</summary>
        public void Ajouter(string écran, TruncationProbe.Passage passage) => Passages.Add((écran, passage));

        /// <summary>Une peinture hors fenêtre, relevée sous le nom <paramref name="écran"/>.</summary>
        public void Sous(string écran, Action peindre)
        {
            _écran = écran;
            int avant = Passages.Count;
            peindre();
            if (Passages.Count == avant)
                ÉcransMuets.Add(écran);
        }

        /// <summary>Titres, descriptions, consignes et textes à taper du catalogue de la fenêtre
        /// Leçons, Défi du jour compris.</summary>
        public void Cataloguer(LessonsWindow fenêtre)
        {
            void Ajouter(string? texte)
            {
                if (string.IsNullOrEmpty(texte)) return;
                Catalogue.Add(texte);
                foreach (string ligne in texte.Split('\n'))
                    Catalogue.Add(ligne);
            }

            foreach (var module in BancCapture.Field<LessonCatalog>(fenêtre, "_catalog").Modules)
            {
                if (module.Id == DailyChallenge.ModuleId)
                {
                    TitresDuDéfi.Add(module.Title);
                    foreach (var leçon in module.Lessons)
                        TitresDuDéfi.Add(leçon.Title);
                }
                Ajouter(module.Title);
                Ajouter(module.Description);
                foreach (var leçon in module.Lessons)
                {
                    Ajouter(leçon.Title);
                    Ajouter(leçon.Description);
                    foreach (var exercice in leçon.Exercises)
                    {
                        Ajouter(exercice.Instruction);
                        Ajouter(exercice.Content);
                    }
                }
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLongW(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hWnd);

    private delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr lParam);

    private const int WS_VISIBLE = 0x10000000;

    /// <summary>Le contrôle et chacun de ses parents jusqu'à la fenêtre portent WS_VISIBLE : il
    /// serait à l'écran si la fenêtre l'était.</summary>
    private static bool Affiché(IntPtr contrôle, IntPtr fenêtre)
    {
        for (IntPtr h = contrôle; h != IntPtr.Zero && h != fenêtre; h = GetParent(h))
            if ((GetWindowLongW(h, -16) & WS_VISIBLE) == 0)
                return false;
        return true;
    }

    /// <summary>WM_PAINT direct à la fenêtre, puis à chacun de ses contrôles affichés : les
    /// boutons se peignent eux-mêmes. Rien n'est montré ni activé.</summary>
    private static void Repeindre(IntPtr hwnd)
    {
        Win32.InvalidateRect(hwnd, IntPtr.Zero, true);
        Win32.SendMessageW(hwnd, Win32.WM_PAINT, IntPtr.Zero, IntPtr.Zero);
        var enfants = new List<IntPtr>();
        EnumChildWindows(hwnd, (h, _) => { enfants.Add(h); return true; }, IntPtr.Zero);
        foreach (IntPtr enfant in enfants.Where(e => Affiché(e, hwnd)))
        {
            Win32.InvalidateRect(enfant, IntPtr.Zero, true);
            Win32.SendMessageW(enfant, Win32.WM_PAINT, IntPtr.Zero, IntPtr.Zero);
        }
    }

    // ── Fenêtre Leçons ───────────────────────────────────────────────

    private static LessonsWindow Leçons()
    {
        var layout = LayoutLoader.LoadFromResource();
        var mapper = new KeyMapper(layout, new MockWin32Api());
        var hook = new KeyboardHook(mapper); // jamais installé
        ConfigManager.ClearWindowBounds(ConfigManager.LessonsWindowBoundsKey);
        var fenêtre = new LessonsWindow(layout, mapper, hook);
        MettreÀLÉchelle(fenêtre, fenêtre.Handle); // RA16 : DPI simulé
        return fenêtre;
    }

    /// <summary>Tape tout l'exercice sans faute, à 150 mots/min : une frappe toutes les 80 ms
    /// sur l'horloge de la séance. Sans ce rythme, la frappe instantanée du test affichait
    /// « 1351021 mots/min » au bilan, une largeur qu'aucun utilisateur ne produit.</summary>
    private static void Taper(LessonsWindow fenêtre)
    {
        var stats = BancCapture.Field<LessonTypingSession>(fenêtre, "_session").Stats;
        // Durée réaliste (0,08 s par frappe) sur l'horloge des durées (audit temps et horloge,
        // TT-2) : le bilan affiche alors une vitesse plausible, de largeur réaliste.
        (typeof(LessonAttemptStats).GetField("_elapsedClock", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(LessonAttemptStats), "_elapsedClock"))
            .SetValue(stats, (Func<TimeSpan>)(() => TimeSpan.FromSeconds(stats.ProductiveKeystrokes * 0.08)));
        for (int garde = 0; garde < 5000 && !BancCapture.Field<bool>(fenêtre, "_showSummary"); garde++)
        {
            var séance = BancCapture.Field<LessonTypingSession>(fenêtre, "_session");
            Assert.True(séance.GetNextExpectedCharacter() is char, "séance bloquée avant le bilan");
            BancCapture.Call(fenêtre, "OnChar", séance.GetNextExpectedCharacter()!.Value);
        }
        Assert.True(BancCapture.Field<bool>(fenêtre, "_showSummary"));
    }

    private static object Énum(string type, string valeur) =>
        Enum.Parse(typeof(LessonsWindow).GetNestedType(type, BindingFlags.NonPublic)
            ?? throw new MissingMemberException(nameof(LessonsWindow), type), valeur);

    private static void Choisir(LessonsWindow fenêtre, string module, string leçon, int exercice) =>
        Assert.True((bool)BancCapture.Call(fenêtre, "SelectExercise", module, leçon, exercice, false)!,
            $"exercice {module}/{leçon}/{exercice} introuvable");

    /// <summary>Accueil (Aujourd'hui, carte, texte), l'exercice de chaque module, sélecteur,
    /// Paramètres, Écriture libre, bilan d'un exercice.</summary>
    private static void ParcourirLeçons(Relevé relevé)
    {
        var fenêtre = Leçons();
        try
        {
            IntPtr hwnd = fenêtre.Handle;
            relevé.Dpi = Win32.GetDpiForWindow(hwnd);
            foreach (string page in new[] { "Today", "Map", "Text" })
            {
                BancCapture.Call(fenêtre, "HomeShowPage", Énum("HomePage", page));
                relevé.Visiter($"Leçons · accueil {page}", hwnd);
            }

            var modules = BancCapture.Field<LessonCatalog>(fenêtre, "_catalog").Modules.Where(m => m.Id != DailyChallenge.ModuleId).ToArray();
            foreach (var module in modules)
            {
                Choisir(fenêtre, module.Id, module.Lessons[0].Id, 0);
                relevé.Visiter($"Leçons · exercice {module.Id}", hwnd);
            }

            Choisir(fenêtre, modules[1].Id, modules[1].Lessons[0].Id, 0);
            BancCapture.SetField(fenêtre, "_pickerOpen", true);
            relevé.Visiter("Leçons · sélecteur", hwnd);
            BancCapture.SetField(fenêtre, "_pickerOpen", false);

            BancCapture.Call(fenêtre, "ToggleSettings");
            relevé.Visiter("Leçons · Paramètres", hwnd);
            BancCapture.Call(fenêtre, "ToggleSettings");

            BancCapture.Call(fenêtre, "SwitchMode", Énum("WindowMode", "Free"));
            BancCapture.SetField(fenêtre, "_freeStartedElapsed", null); // statistiques sans horloge
            relevé.Visiter("Leçons · Écriture libre", hwnd);
            BancCapture.Call(fenêtre, "SwitchMode", Énum("WindowMode", "Lessons"));

            Taper(fenêtre);
            relevé.Visiter("Leçons · bilan", hwnd);
            relevé.Cataloguer(fenêtre);
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    /// <summary>Le parcours de l'accueil : usages, chaque étape, le bilan d'une étape et d'un
    /// exercice bonus, l'écran final.</summary>
    private static void ParcourirParcours(Relevé relevé)
    {
        var fenêtre = Leçons();
        try
        {
            IntPtr hwnd = fenêtre.Handle;
            fenêtre.BeginTutorial();
            relevé.Visiter("Parcours · usages", hwnd);
            BancCapture.Call(fenêtre, "ToggleTutorialUsage", 2); // Code
            BancCapture.Call(fenêtre, "ToggleTutorialUsage", 4); // Afrique
            relevé.Visiter("Parcours · usages cochés", hwnd);
            BancCapture.Call(fenêtre, "StartTutorialRoute");

            var parcours = BancCapture.Field<TutorialSteps.RouteStep[]>(fenêtre, "_tutorialRoute");
            int bonus = Array.FindIndex(parcours, étape => étape.Skippable);
            for (int i = 0; i < parcours.Length; i++)
            {
                Assert.Equal(i, BancCapture.Field<int>(fenêtre, "_tutorialRoutePos"));
                relevé.Visiter($"Parcours · étape {i + 1}", hwnd);
                if (i == 0 || i == bonus)
                {
                    Taper(fenêtre);
                    relevé.Visiter($"Parcours · bilan de l'étape {i + 1}", hwnd);
                }
                BancCapture.Call(fenêtre, "AdvanceTutorialRoute");
            }
            Assert.Equal("Final", BancCapture.Field<object>(fenêtre, "_tutorialPage").ToString());
            relevé.Visiter("Parcours · écran final", hwnd);
            relevé.Cataloguer(fenêtre);
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    /// <summary>Le Défi : échauffement, puis bilan de l'extrait final pendant la prise en main
    /// (5 premières séances) et après (avec « Partager »).</summary>
    private static void ParcourirDéfi(Relevé relevé, bool aprèsPriseEnMain)
    {
        if (aprèsPriseEnMain)
            ConfigManager.SetTrainingSequenceIndex(DailyChallenge.SequenceLength);
        var fenêtre = Leçons();
        try
        {
            IntPtr hwnd = fenêtre.Handle;
            string suffixe = aprèsPriseEnMain ? "" : " (prise en main)";
            BancCapture.Field<ChallengeSessionState>(fenêtre, "_challenge").Request();
            BancCapture.Call(fenêtre, "RefreshChallengeModuleIfStale");
            var défi = BancCapture.Field<LessonCatalog>(fenêtre, "_catalog").Modules.Single(m => m.Id == DailyChallenge.ModuleId);
            if (aprèsPriseEnMain)
                relevé.TitreDatéLePlusLarge = PoserLaDateLaPlusLarge(fenêtre, défi.Lessons[0]);
            Choisir(fenêtre, défi.Id, défi.Lessons[0].Id, 0);
            relevé.Visiter("Défi · échauffement" + suffixe, hwnd);
            if (relevé.TitreDatéLePlusLarge is { } titre)
            {
                // L'en-tête a bien été dessiné avec ce titre : le sélecteur le porte dans son nom.
                var noms = BancCapture.Field<List<LessonControlView>>(fenêtre, "_controlViews").Select(c => c.Name);
                Assert.Contains(noms, nom => nom.Contains(titre, StringComparison.Ordinal));
            }
            Choisir(fenêtre, défi.Id, défi.Lessons[0].Id, défi.Lessons[0].Exercises.Count - 1);
            Taper(fenêtre);
            relevé.Visiter("Défi · bilan" + suffixe, hwnd);
            // Audit de régression du 10/11, RA9 : l'extrait rejoué, sa ligne d'état précédée de
            // « Séance du jour déjà comptée ».
            BancCapture.Call(fenêtre, "ReplayExercise");
            Assert.True(fenêtre.ChallengeAlreadyCounted);
            Assert.StartsWith(L.Challenge_AlreadyDone, (string)BancCapture.Call(fenêtre, "BuildTypingStatus")!);
            relevé.Visiter("Défi · rejoué, séance déjà comptée" + suffixe, hwnd);
            relevé.Cataloguer(fenêtre);
        }
        finally { BancCapture.Teardown(fenêtre); }
    }

    /// <summary>Après la prise en main, le titre du Défi porte la date du jour (« Défi du
    /// 10 novembre 2026 ») : le verdict changerait d'un jour à l'autre. Le titre prend donc la
    /// date la plus large de l'année, mesurée dans la police de l'en-tête.</summary>
    private static string PoserLaDateLaPlusLarge(LessonsWindow fenêtre, LessonLesson leçon)
    {
        var police = (FontSpec)BancCapture.Call(fenêtre, "F", AppFonts.Text, 15, TypeRamp.Semibold)!;
        var premier = new DateOnly(DateTime.Now.Year, 1, 1);
        IntPtr hdc = Win32.CreateCompatibleDC(IntPtr.Zero);
        string titre;
        try
        {
            titre = Enumerable.Range(0, 366).Select(premier.AddDays)
                .Select(L.Challenge_DailyLessonTitle)
                .MaxBy(t => PaperText.Width(hdc, t, police))!;
        }
        finally { Win32.DeleteDC(hdc); }
        Assert.Equal(L.Challenge_DailyLessonTitle(DateOnly.FromDateTime(DateTime.Now)), leçon.Title); // le titre daté, pas « Découverte n/5 »
        (typeof(LessonLesson).GetField("<Title>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(nameof(LessonLesson), "Title"))
            .SetValue(leçon, titre);
        return titre;
    }

    // ── Autres fenêtres ──────────────────────────────────────────────

    private static void ParcourirFenêtres(Relevé relevé)
    {
        var accueil = new OnboardingWindow();
        try
        {
            IntPtr hwnd = BancCapture.Handle(accueil);
            MettreÀLÉchelle(accueil, hwnd);
            VérifierTailleDemandée(accueil, hwnd);
            for (int étape = 0; étape < OnboardingStep.Count; étape++)
            {
                BancCapture.SetField(accueil, "_currentStep", étape);
                BancCapture.Call(accueil, "UpdateStepVisibility");
                relevé.Visiter($"Accueil · étape {étape + 1}", hwnd);
            }
            // Étape 1 dans l'autre état de l'accord : « Activer et essayer » ou « Essayer maintenant ».
            BancCapture.SetField(accueil, "_activationAccepted", !BancCapture.Field<bool>(accueil, "_activationAccepted"));
            BancCapture.SetField(accueil, "_currentStep", 0);
            Win32.SetWindowTextW(BancCapture.Field<IntPtr>(accueil, "_hWndBtnTry"), (string)BancCapture.Call(accueil, "get_TryButtonText")!);
            BancCapture.Call(accueil, "UpdateStepVisibility");
            relevé.Visiter("Accueil · étape 1, autre accord", hwnd);
        }
        finally { BancCapture.Teardown(accueil); }

        var paramètres = new SettingsWindow();
        try
        {
            IntPtr hwnd = BancCapture.Handle(paramètres);
            MettreÀLÉchelle(paramètres, hwnd);
            BancCapture.Call(paramètres, "RepositionControls");
            IntPtr onglets = BancCapture.Field<IntPtr>(paramètres, "_hWndTabStrip");
            var type = typeof(SettingsWindow).GetNestedType("SettingsTab", BindingFlags.NonPublic)!;
            foreach (var onglet in Enum.GetValues(type))
            {
                Win32.SendMessageW(onglets, Win32.TCM_SETCURSEL, (IntPtr)(int)onglet, IntPtr.Zero);
                BancCapture.Call(paramètres, "SetActiveTab", onglet);
                relevé.Visiter($"Paramètres · {onglet}", hwnd);
            }
            VisiterMessagesDesParamètres(relevé, paramètres, hwnd);
        }
        finally { BancCapture.Teardown(paramètres); }

        var statistiques = new UsageStatsWindow();
        try
        {
            MettreÀLÉchelle(statistiques, BancCapture.Handle(statistiques));
            BancCapture.Call(statistiques, "FitWindow");
            relevé.Visiter("Statistiques", BancCapture.Handle(statistiques));
        }
        finally { BancCapture.Teardown(statistiques); }
        VisiterStatistiquesÉteintes(relevé);

        var àPropos = new AboutWindow();
        try
        {
            MettreÀLÉchelle(àPropos, BancCapture.Handle(àPropos));
            BancCapture.Call(àPropos, "RepositionControls");
            relevé.Visiter("À propos", BancCapture.Handle(àPropos));
        }
        finally { BancCapture.Teardown(àPropos); }

        var couches = new MaintainableLayersWindow();
        try
        {
            MettreÀLÉchelle(couches, BancCapture.Handle(couches));
            BancCapture.Call(couches, "LoadFromConfig");
            relevé.Visiter("Couches", BancCapture.Handle(couches));
        }
        finally { BancCapture.Teardown(couches); }

        var clavier = new VirtualKeyboard(LayoutLoader.LoadFromResource());
        try
        {
            IntPtr hwnd = BancCapture.Handle(clavier);
            MettreÀLÉchelle(clavier, hwnd);
            relevé.Visiter("Clavier virtuel", hwnd);
            clavier.UpdateState(false, true, false, false, false, null);
            relevé.Visiter("Clavier virtuel · AltGr", hwnd);
            clavier.UpdateState(false, false, false, false, false, "dk_acute");
            relevé.Visiter("Clavier virtuel · touche morte", hwnd);
        }
        finally { BancCapture.Teardown(clavier); }

        var pause = new PauseDurationDialog();
        var horloge = PauseDurationDialog.Clock;
        PauseDurationDialog.Clock = () => new DateTime(2026, 9, 28, 14, 30, 0);
        try
        {
            BancCapture.Call(pause, "CreateWindow", IntPtr.Zero);
            MettreÀLÉchelle(pause, BancCapture.Handle(pause));
            relevé.Visiter("Durée de pause", BancCapture.Handle(pause));
        }
        finally
        {
            BancCapture.Teardown(pause);
            PauseDurationDialog.Clock = horloge;
        }

        foreach (bool auDémarrage in new[] { true, false })
        {
            var conflit = new LayoutConflictWindow(auDémarrage, () => { }, () => { });
            try
            {
                MettreÀLÉchelle(conflit, BancCapture.Handle(conflit));
                BancCapture.Call(conflit, "FitWindow");
                relevé.Visiter(auDémarrage ? "Conflit · démarrage" : "Conflit · bascule", BancCapture.Handle(conflit));
            }
            finally { BancCapture.Teardown(conflit); }
        }

        var recherche = new CharacterSearch(new TextInsertionService(_ => default, _ => false, () => false));
        try
        {
            IntPtr hwnd = BancCapture.Handle(recherche);
            MettreÀLÉchelle(recherche, hwnd);
            BancCapture.Call(recherche, "ResizeToFitResults");
            Win32.SetWindowTextW(BancCapture.Field<IntPtr>(recherche, "_hEdit"), L.IsEnglish ? "acute" : "aigu");
            relevé.Visiter("Recherche · résultats", hwnd);
            VisiterRechercheSansRésultat(relevé, recherche, hwnd);
        }
        finally { BancCapture.Teardown(recherche); }

        VisiterBascule(relevé);
        VisiterIndicateurDeCouche(relevé, _dpiSimulé > 0 ? _dpiSimulé : relevé.Dpi);
    }

    // ── Le test ──────────────────────────────────────────────────────

    /// <summary>Toutes les chaînes sans paramètre de <see cref="L"/>, dans la langue courante.</summary>
    private static Dictionary<string, string> Chaînes()
    {
        var chaînes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in typeof(L).GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                     .Where(p => p.PropertyType == typeof(string) && p.GetIndexParameters().Length == 0 && p.Name != nameof(L.Language)))
        {
            if (p.GetValue(null) is string texte && texte.Length > 0)
                chaînes.TryAdd(texte, p.Name);
        }
        return chaînes;
    }

    private static string Décrire(string écran, TruncationProbe.Passage p, string origine) =>
        $"« {p.Text} » [{origine}] — {écran} — {Path.GetFileName(p.File)}:{p.Line} — demande {p.Needed} px, dispose de {p.Available} px";

    /// <summary><paramref name="dpi"/> 0 : le DPI du poste, sur son écran. 120, 144 et 192 :
    /// DPI simulé (RA16), sur un écran courant à cette échelle.</summary>
    [Theory]
    [InlineData("fr", 0)]
    [InlineData("en", 0)]
    [InlineData("fr", 120)]
    [InlineData("en", 120)]
    [InlineData("fr", 144)]
    [InlineData("en", 144)]
    [InlineData("fr", 192)]
    [InlineData("en", 192)]
    public void Aucun_libelle_d_interface_n_est_coupe_a_la_taille_par_defaut(string langue, int dpi)
    {
        using var isolation = new BancCapture.Isolation();
        isolation.SetLanguage(langue);
        using var écranSimulé = dpi > 0 ? NativeWindow.OverrideWorkAreaForTests(ÉcranPour(dpi)) : null;
        using var relevé = new Relevé();
        _échelles.Clear();
        _dpiSimulé = dpi;
        try
        {
            ParcourirLeçons(relevé);
            ParcourirParcours(relevé);
            ParcourirDéfi(relevé, aprèsPriseEnMain: false);
            ParcourirDéfi(relevé, aprèsPriseEnMain: true);
            ParcourirFenêtres(relevé);
        }
        finally { _dpiSimulé = 0; }

        var chaînes = Chaînes();
        var interface_ = new List<string>();
        var contenus = new List<string>();
        foreach (var (écran, p) in relevé.Passages.Where(x => x.Passage.Cut)
                     .DistinctBy(x => (x.Écran, x.Passage.Text, x.Passage.File, x.Passage.Line)))
        {
            if (chaînes.TryGetValue(p.Text, out string? nom))
                interface_.Add(Décrire(écran, p, "L." + nom));
            else if (relevé.TitresDuDéfi.Contains(p.Text))
                interface_.Add(Décrire(écran, p, "titre du Défi composé par L"));
            else if (relevé.Catalogue.Contains(p.Text) || relevé.Catalogue.Any(c => c.Contains(p.Text, StringComparison.Ordinal)))
                contenus.Add(Décrire(écran, p, "catalogue"));
            else if (Path.GetFileName(p.File) == "CharacterSearch.cs")
                contenus.Add(Décrire(écran, p, "nom de caractère"));
            else
                interface_.Add(Décrire(écran, p, "texte composé"));
        }

        _sortie.WriteLine($"{langue}, poste à {relevé.Dpi} DPI, {(dpi > 0 ? $"fenêtres portées à {dpi} DPI" : "sans DPI simulé")} : {relevé.Passages.Count} passages par une coupe, sur {relevé.Passages.Select(x => x.Écran).Distinct().Count()} écrans.");
        _sortie.WriteLine($"Dont {relevé.Passages.Count(x => x.Passage.Overflow)} lignes sans coupe (DrawLine) et {relevé.Passages.Count(x => x.Passage.File.StartsWith("natif ", StringComparison.Ordinal))} textes de contrôle natif.");
        _sortie.WriteLine("Échelle retenue par fenêtre : " + string.Join(", ", _échelles.OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => $"{e.Key} {e.Value:0.##}")));
        _sortie.WriteLine("Classes de contrôle natif non mesurées : " + (relevé.ClassesNonMesurées.Count == 0 ? "aucune" : string.Join(", ", relevé.ClassesNonMesurées.OrderBy(c => c, StringComparer.Ordinal))));
        _sortie.WriteLine("Appels vus : " + string.Join(", ", relevé.Passages
            .Select(x => $"{Path.GetFileName(x.Passage.File)}:{x.Passage.Line}").Distinct().OrderBy(s => s, StringComparer.Ordinal)));
        // L'en-tête n'appelle Ellipsize que si module et leçon ne tiennent pas ensemble : sans
        // passage, le titre le plus large a tenu entier.
        var titreDuDéfi = relevé.Passages.Where(x => x.Passage.Text == relevé.TitreDatéLePlusLarge).Select(x => x.Passage).ToList();
        _sortie.WriteLine($"Titre du Défi le plus large : « {relevé.TitreDatéLePlusLarge} », " + (titreDuDéfi.Count == 0
            ? "tient entier (aucune coupe demandée)"
            : $"demande {titreDuDéfi[0].Needed} px, dispose de {titreDuDéfi[0].Available} px"));
        _sortie.WriteLine($"Écrans sans aucune coupe : {(relevé.ÉcransMuets.Count == 0 ? "aucun" : string.Join(" | ", relevé.ÉcransMuets))}");
        _sortie.WriteLine($"Contenus coupés (par conception, sans échec) : {contenus.Count}");
        foreach (string ligne in contenus) _sortie.WriteLine("  " + ligne);
        _sortie.WriteLine($"Libellés d'interface coupés : {interface_.Count}");
        foreach (string ligne in interface_) _sortie.WriteLine("  " + ligne);
        // Les textes qui tiennent de justesse : les premiers à couper si un libellé s'allonge.
        _sortie.WriteLine("Marges les plus faibles (textes qui tiennent) :");
        foreach (var (écran, p) in relevé.Passages.Where(x => !x.Passage.Cut && x.Passage.Text.Length > 1)
                     .DistinctBy(x => (x.Passage.Text, x.Passage.File, x.Passage.Line))
                     .OrderBy(x => x.Passage.Available - x.Passage.Needed).Take(12))
            _sortie.WriteLine("  " + Décrire(écran, p, chaînes.TryGetValue(p.Text, out string? n) ? "L." + n : "autre"));
        if (Environment.GetEnvironmentVariable("AZERTY_TRONCATURE_DETAIL") == "1")
        {
            _sortie.WriteLine("Contrôles natifs mesurés :");
            foreach (var (écran, p) in relevé.Passages.Where(x => x.Passage.File.StartsWith("natif ", StringComparison.Ordinal))
                         .DistinctBy(x => (x.Passage.Text, x.Passage.File, x.Passage.Line, x.Passage.Available)))
                _sortie.WriteLine("  " + Décrire(écran, p, "natif"));
        }

        // Seules ces fenêtres n'ont aucune coupe à points de suspension (ni Ellipsize ni
        // DT_END_ELLIPSIS) ; tout autre écran muet n'a pas été peint, et ne prouverait rien.
        // Depuis que DrawLine passe par la couture (RA16), il ne reste que le Conflit, dont le
        // texte est en paragraphes et en contrôles natifs.
        string[] sansCoupe = { "Conflit · " };
        var muets = relevé.ÉcransMuets.Where(é => !sansCoupe.Any(é.StartsWith)).ToList();
        Assert.True(muets.Count == 0, $"{langue} : écrans peints sans aucun passage par une coupe : {string.Join(" | ", muets)}");

        // Le DPI simulé a été pris : sans cela, les passages à 120, 144 et 192 DPI rejoueraient
        // celui du poste. L'Accueil réduit lui-même son échelle pour tenir dans l'écran.
        if (dpi > 0)
        {
            Assert.True(_échelles.Count >= 9, $"échelle lue dans {_échelles.Count} fenêtres seulement");
            foreach (var (fenêtre, échelle) in _échelles.Where(e => e.Key != nameof(OnboardingWindow)))
                Assert.True(Math.Abs(échelle - dpi / 96f) < 0.02f, $"{fenêtre} : échelle {échelle} pour {dpi} DPI");
            Assert.True(_échelles[nameof(OnboardingWindow)] > 1f, $"Accueil : échelle {_échelles[nameof(OnboardingWindow)]} pour {dpi} DPI");
        }

        Assert.True(interface_.Count == 0,
            $"{langue} : {interface_.Count} libellé(s) d'interface coupé(s) :\n" + string.Join("\n", interface_));
    }

    /// <summary>Témoin : la couture voit une coupe d'Ellipsize et une coupe de DrawText. Sans lui,
    /// un test vert pourrait venir d'un observateur jamais appelé.</summary>
    [Fact]
    public void La_couture_voit_les_deux_sortes_de_coupe()
    {
        var vus = new List<TruncationProbe.Passage>();
        TruncationProbe.Observer = vus.Add;
        IntPtr hdc = Win32.CreateCompatibleDC(IntPtr.Zero);
        try
        {
            var police = FontSpec.Of(AppFonts.Text, 15, TypeRamp.Regular, 1f);
            var ellipsize = typeof(LessonsWindow).GetMethod("Ellipsize", BindingFlags.Static | BindingFlags.NonPublic)!;
            string rendu = (string)ellipsize.Invoke(null, new object[] { hdc, "Continuer les leçons", police, 30, "", 0 })!;
            Assert.EndsWith("…", rendu);

            var bouton = new Win32.RECT { left = 0, top = 0, right = 10, bottom = 30 };
            GdiHelpers.DrawLightButton(hdc, bouton, "Continuer les leçons", police.Handle, primary: true,
                hot: false, pressed: false, enabled: true, radius: 4, backdrop: 0xFFFFFF);

            Assert.Equal(2, vus.Count);
            Assert.All(vus, p => Assert.True(p.Cut, $"« {p.Text} » : {p.Needed} px pour {p.Available} px"));
            Assert.Equal(10, vus[1].Available);
            Assert.True(vus[1].Needed > 10);
        }
        finally
        {
            TruncationProbe.Observer = null;
            Win32.DeleteDC(hdc);
        }
    }
}
