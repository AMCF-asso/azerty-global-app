using System.Reflection;
using System.Runtime.CompilerServices;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Lot « Accueil et Réglages » des règles de docs/machines-a-etats.md (app 1.3.0) : M7.1, M7.2,
/// M7.3 (accueil), M8.1 (Paramètres), M8.2 (recherche de caractère), M8.3 (clavier virtuel),
/// M8.4 (couches maintenables) et M8.5 (désinscription des fenêtres natives). Les vraies fenêtres
/// tournent, sur le bureau caché du script <c>run_tests.ps1</c> ; rien n'est affiché à l'utilisateur.
/// Le nom de chaque méthode commence par l'id de la règle. Verdict : vert si le produit tient la
/// règle ; rouge si elle n'est pas tenue (le test échoue alors sur sa dernière assertion, les
/// témoins avant).
/// </summary>
internal static class OutilsAccueilReglages130
{
    /// <summary>
    /// Le démarrage automatique, la boîte d'erreur et les horloges de l'accueil sont des statiques
    /// qui touchent Windows (registre, MessageBox) : remplacés pendant le test, restaurés à la fin.
    /// </summary>
    internal sealed class EnvAccueil : IDisposable
    {
        private readonly Func<bool, bool> _setter = OnboardingWindow.AutoStartSetter;
        private readonly Func<bool> _registered = OnboardingWindow.AutoStartIsRegistered;
        private readonly Func<IntPtr, string, string, uint, int> _box = OnboardingWindow.ShowMessageBox;
        private readonly Func<long> _clock = OnboardingWindow.Clock;
        private readonly Func<int> _delay = OnboardingWindow.DoubleClickDelay;

        /// <summary>Écritures du démarrage automatique demandées à Windows.</summary>
        internal int EcrituresDemarrage { get; private set; }

        internal EnvAccueil()
        {
            OnboardingWindow.AutoStartIsRegistered = () => false;
            OnboardingWindow.AutoStartSetter = _ => { EcrituresDemarrage++; return true; };
            OnboardingWindow.ShowMessageBox = (_, _, _, _) => 1;
        }

        public void Dispose()
        {
            OnboardingWindow.AutoStartSetter = _setter;
            OnboardingWindow.AutoStartIsRegistered = _registered;
            OnboardingWindow.ShowMessageBox = _box;
            OnboardingWindow.Clock = _clock;
            OnboardingWindow.DoubleClickDelay = _delay;
        }
    }

    internal static int Etape(OnboardingWindow w) => BancCapture.Field<int>(w, "_currentStep");
    internal static bool Bool(object w, string champ) => BancCapture.Field<bool>(w, champ);

    /// <summary>Ouvre l'accueil comme le tray : créé, puis <c>Show</c> (étape 1).</summary>
    internal static OnboardingWindow OuvrirAccueil()
    {
        var w = new OnboardingWindow();
        w.Show();
        return w;
    }

    internal static void AllerALaDerniereEtape(OnboardingWindow w)
    {
        while (Etape(w) < OnboardingStep.Last)
            BancCapture.Call(w, "GoToNextStep");
    }

    internal static LessonsWindow OuvrirLecons()
    {
        var layout = LayoutLoader.LoadFromResource();
        var mapper = new KeyMapper(layout, new MockWin32Api());
        var hook = new KeyboardHook(mapper); // jamais installé
        ConfigManager.ClearWindowBounds(ConfigManager.LessonsWindowBoundsKey);
        return new LessonsWindow(layout, mapper, hook);
    }
}

/// <summary>Accueil : navigation, fermeture, essai de frappe (M7.1, M7.2, M7.3).</summary>
public sealed class Regles130AccueilReglagesAccueilTests
{
    // M7.1 I1 : 0 <= étape courante <= dernière étape. Avancer plus de fois qu'il n'y a d'étapes puis
    // reculer plus de fois : GoToNextStep et GoToPreviousStep bornent chacun leur sens.
    [Fact]
    public void M7_1_I1_EtapeCourante_ResteEntre0EtLaDerniereEtape()
    {
        using var env = new OutilsAccueilReglages130.EnvAccueil();
        using var iso = new BancCapture.Isolation();
        var w = OutilsAccueilReglages130.OuvrirAccueil();
        try
        {
            Assert.Equal(0, OutilsAccueilReglages130.Etape(w));                 // témoin : on part de l'étape 1
            var vues = new List<int>();
            for (int i = 0; i < OnboardingStep.Last + 2; i++)
            {
                BancCapture.Call(w, "GoToNextStep");
                vues.Add(OutilsAccueilReglages130.Etape(w));
            }
            Assert.Contains(OnboardingStep.Last, vues);                          // témoin : la dernière étape est atteinte
            for (int i = 0; i < OnboardingStep.Last + 2; i++)
            {
                BancCapture.Call(w, "GoToPreviousStep");
                vues.Add(OutilsAccueilReglages130.Etape(w));
            }
            Assert.Contains(1, vues.Skip(OnboardingStep.Last + 2));              // témoin : le retour passe par l'étape 2
            Assert.All(vues, etape => Assert.InRange(etape, 0, OnboardingStep.Last));
            Assert.Equal(0, OutilsAccueilReglages130.Etape(w));
        }
        finally { BancCapture.Teardown(w); }
    }

    // M7.1 I2 : _step3Reached vrai implique que l'étape 3 a été affichée depuis le dernier Show. Close
    // remet aussi le drapeau à faux : le Show sans Close est donc la seule séquence qui prouve la remise
    // à zéro de Show ; la séquence Close vient ensuite, pour la remise à zéro de Close.
    [Fact]
    public void M7_1_I2_Etape3Atteinte_VraiSeulementSiVueDepuisLeDernierShow()
    {
        using var env = new OutilsAccueilReglages130.EnvAccueil();
        using var iso = new BancCapture.Isolation();
        var w = OutilsAccueilReglages130.OuvrirAccueil();
        try
        {
            Assert.False(OutilsAccueilReglages130.Bool(w, "_step3Reached"));     // témoin : faux à l'ouverture
            BancCapture.Call(w, "GoToNextStep");                                  // étape 2 : pas l'étape 3
            Assert.False(OutilsAccueilReglages130.Bool(w, "_step3Reached"));
            BancCapture.Call(w, "GoToNextStep");                                  // étape 3
            Assert.True(OutilsAccueilReglages130.Bool(w, "_step3Reached"));       // témoin : le drapeau se pose
            BancCapture.Call(w, "GoToPreviousStep");                              // revenu à l'étape 2
            Assert.True(OutilsAccueilReglages130.Bool(w, "_step3Reached"));       // vue depuis le Show : reste vrai

            w.Show();                                                             // nouveau Show sans Close
            Assert.Equal(0, OutilsAccueilReglages130.Etape(w));
            Assert.False(OutilsAccueilReglages130.Bool(w, "_step3Reached"), "Show n'a pas remis _step3Reached à faux");

            OutilsAccueilReglages130.AllerALaDerniereEtape(w);
            Assert.True(OutilsAccueilReglages130.Bool(w, "_step3Reached"));
            w.Close(validated: false);
            Assert.False(OutilsAccueilReglages130.Bool(w, "_step3Reached"), "Close n'a pas remis _step3Reached à faux");
            w.Show();
            Assert.False(OutilsAccueilReglages130.Bool(w, "_step3Reached"));
        }
        finally { BancCapture.Teardown(w); }
    }

    // M7.2 I5 : Close est idempotent. Garanti en partie (doc) : le drapeau _step3Reached est consommé,
    // donc rien n'est réécrit ; mais OnClosed est rappelé à chaque Close. Le test vérifie la moitié
    // garantie (une seule écriture de démarrage automatique, une seule de « Ne plus afficher », même
    // si la configuration a changé entre les deux Close) et CARACTÉRISE l'autre : OnClosed est rappelé
    // (vert aujourd'hui, c'est un fait documenté, pas une garantie).
    [Fact]
    public void M7_2_I5_CloseDeuxFois_UneSeuleEcriture_OnClosedRappele()
    {
        using var env = new OutilsAccueilReglages130.EnvAccueil();
        using var iso = new BancCapture.Isolation();
        var w = OutilsAccueilReglages130.OuvrirAccueil();
        try
        {
            int fermetures = 0;
            w.OnClosed = () => fermetures++;
            BancCapture.SetField(w, "_activationAccepted", true);
            OutilsAccueilReglages130.AllerALaDerniereEtape(w);

            w.Close(validated: true);
            Assert.Equal(1, env.EcrituresDemarrage);                              // témoin : la validation écrit
            Assert.True(ConfigManager.ShowOnboardingAtStartup);                   // témoin : « Ne plus afficher » décochée écrite
            Assert.False(OutilsAccueilReglages130.Bool(w, "_visible"));
            Assert.Equal(1, fermetures);

            ConfigManager.SetShowOnboardingAtStartup(false);                      // l'utilisateur change le réglage ailleurs
            w.Close(validated: true);                                             // second Close (WM_CLOSE d'un updater, etc.)

            Assert.Equal(2, fermetures);                                          // caractérisation : OnClosed rappelé
            Assert.False(OutilsAccueilReglages130.Bool(w, "_visible"));
            Assert.False(ConfigManager.ShowOnboardingAtStartup, "le second Close a réécrit « Ne plus afficher »");
            Assert.Equal(1, env.EcrituresDemarrage);
        }
        finally { BancCapture.Teardown(w); }
    }

    // M7.3 I2 : module appris implique module tenté à l'ouverture. Show recalcule les deux drapeaux
    // d'après la progression : seul le cas « 3 exercices réussis, parcours jamais ouvert » distingue
    // « tenté = au moins un exercice » d'un autre seuil. Une isolation neuve par cas.
    [Fact]
    public void M7_3_I2_ModuleAppris_ImpliqueModuleTenteALOuverture()
    {
        var cas = new (int Exercices, bool ParcoursOuvert)[]
        {
            (0, false), (1, false), (2, false), (TutorialSteps.DoneThreshold, false),
            (TutorialSteps.DoneThreshold + 2, false), (0, true), (TutorialSteps.DoneThreshold, true),
        };
        var vus = new List<(int, bool, bool, bool)>();
        foreach (var (exercices, ouvert) in cas)
        {
            using var env = new OutilsAccueilReglages130.EnvAccueil();
            using var iso = new BancCapture.Isolation();
            if (exercices > 0) ConfigManager.SetLearningMaxStepCompleted(exercices);
            if (ouvert) ConfigManager.MarkLearningTutorialOpened();
            var w = OutilsAccueilReglages130.OuvrirAccueil();
            try
            {
                vus.Add((exercices, ouvert,
                    OutilsAccueilReglages130.Bool(w, "_learningModuleAttempted"),
                    OutilsAccueilReglages130.Bool(w, "_learningModuleDone")));
            }
            finally { BancCapture.Teardown(w); }
        }

        Assert.Contains(vus, v => v is (0, false, false, false));                          // témoin : rien fait, rien tenté
        Assert.Contains(vus, v => v.Item1 == TutorialSteps.DoneThreshold && v.Item4);       // témoin : seuil atteint, appris
        Assert.Contains(vus, v => v is (0, true, true, false));                             // témoin : parcours ouvert, tenté
        var violations = vus.Where(v => v.Item4 && !v.Item3).ToList();
        Assert.True(violations.Count == 0, "appris sans être tenté : " + string.Join(" ; ",
            violations.Select(v => $"exercices={v.Item1} parcoursOuvert={v.Item2}")));
    }

    // M7.3 I3 : _tutorialOpen vrai si et seulement si un parcours est ouvert dans les Leçons. Trois
    // volets : sans rappel branché rien ne s'ouvre (cohérent) ; avec une vraie fenêtre Leçons, le
    // drapeau suit IsTutorialActive à l'ouverture puis à la fermeture ; et, câblé comme le tray, un
    // parcours qui ne peut pas s'ouvrir (pas d'accord d'activation) referme l'état aussitôt, ce que
    // le doc nomme comme voie « non garantie » (ShowTutorialFromOnboarding).
    [Fact]
    public void M7_3_I3_TutorialOpen_SuitLeParcoursOuvertDansLesLecons()
    {
        using var env = new OutilsAccueilReglages130.EnvAccueil();
        using var iso = new BancCapture.Isolation();
        var w = OutilsAccueilReglages130.OuvrirAccueil();
        var lecons = OutilsAccueilReglages130.OuvrirLecons();
        try
        {
            // 1. Aucun rappel branché : LaunchTutorial quitte sans rien poser.
            BancCapture.Call(w, "LaunchTutorial");
            Assert.False(OutilsAccueilReglages130.Bool(w, "_tutorialOpen"));
            Assert.False(OutilsAccueilReglages130.Bool(w, "_learningModuleAttempted"));

            // 2. Parcours réel : le rappel ouvre le parcours des Leçons et relaie sa fin à l'accueil.
            w.TutorialRequested = () =>
            {
                lecons.TutorialClosed = completed => w.OnTutorialClosed(completed);
                lecons.BeginTutorial();
            };
            BancCapture.Call(w, "LaunchTutorial");
            Assert.True(lecons.IsTutorialActive);                                       // témoin : le parcours est ouvert
            Assert.True(OutilsAccueilReglages130.Bool(w, "_tutorialOpen"));
            BancCapture.Call(lecons, "StartTutorialRoute");
            BancCapture.Call(lecons, "LeaveTutorialForNow");                            // « Passer pour l'instant »
            Assert.False(lecons.IsTutorialActive);                                      // témoin : le parcours est fermé
            Assert.False(OutilsAccueilReglages130.Bool(w, "_tutorialOpen"));

            // 3. Câblé comme le tray, sans accord d'activation : le parcours ne s'ouvre pas.
            var tray = (TrayApplication)RuntimeHelpers.GetUninitializedObject(typeof(TrayApplication));
            BancCapture.SetField(tray, "_onboarding", w);
            BancCapture.SetField(tray, "_activationConsent", false);
            w.TutorialRequested = (Action)Delegate.CreateDelegate(typeof(Action), tray,
                typeof(TrayApplication).GetMethod("ShowTutorialFromOnboarding", BindingFlags.Instance | BindingFlags.NonPublic)!);
            BancCapture.Call(w, "LaunchTutorial");
            Assert.True(ConfigManager.LearningTutorialOpened);                          // témoin : le lancement a bien eu lieu
            Assert.True(OutilsAccueilReglages130.Bool(w, "_learningModuleAttempted"));
            Assert.False(OutilsAccueilReglages130.Bool(w, "_tutorialOpen"),
                "_tutorialOpen reste vrai alors qu'aucun parcours n'est ouvert");
        }
        finally
        {
            BancCapture.Teardown(lecons);
            BancCapture.Teardown(w);
        }
    }
}

/// <summary>Paramètres : capture de raccourci et handle GDI (M8.1).</summary>
public sealed class Regles130AccueilReglagesParametresTests
{
    private const uint VkF9 = 0x78; // touche valide pour un raccourci

    // M8.1 I1 : pas de capture de raccourci hors de la fenêtre visible. Une capture confirmée et le
    // focus du champ ne survivent ni à Close ni à Show ; et SetInputPaused(true) annule la capture.
    [Fact]
    public void M8_1_I1_PasDeCaptureDeRaccourciHorsDeLaFenetreVisible()
    {
        using var iso = new BancCapture.Isolation();
        var w = new SettingsWindow();
        try
        {
            w.Show();
            IntPtr champ = BancCapture.Field<IntPtr>(w, "_hWndEditKeyboard");
            Win32.SetFocus(champ);
            BancCapture.Pump(2);
            Assert.Equal(champ, Win32.GetFocus());                                       // témoin : le champ a le vrai focus
            Assert.Equal(champ, BancCapture.Field<IntPtr>(w, "_focusedShortcut"));       // témoin : la capture est ouverte
            BancCapture.Call(w, "ApplyCapturedShortcut", champ, VkF9);
            Assert.True(OutilsAccueilReglages130.Bool(w, "_captureConfirmed"));          // témoin : capture confirmée

            // Pas d'assertion entre Close et Show : sur le bureau caché aucune fenêtre n'est au premier
            // plan, la fenêtre masquée garde donc son focus et _focusedShortcut (Windows n'envoie
            // WM_KILLFOCUS qu'à la perte d'activation, ce que Close ne provoque pas ici). Dans le
            // produit Close ne part que de la fenêtre active (bouton, Échap, croix).
            w.Close();
            BancCapture.Pump(2);
            Assert.False(Win32.IsWindowVisible(BancCapture.Handle(w)));                  // témoin : masquée
            w.Show();
            Assert.Equal(IntPtr.Zero, BancCapture.Field<IntPtr>(w, "_focusedShortcut"));
            Assert.False(OutilsAccueilReglages130.Bool(w, "_captureConfirmed"));
            Assert.False(OutilsAccueilReglages130.Bool(w, "_showCaptureHint"));

            Win32.SetFocus(BancCapture.Handle(w));                                       // le focus quitte le champ
            BancCapture.Pump(2);
            Win32.SetFocus(champ);                                                       // nouvelle capture
            BancCapture.Pump(2);
            Assert.Equal(champ, BancCapture.Field<IntPtr>(w, "_focusedShortcut"));       // témoin
            Assert.True(OutilsAccueilReglages130.Bool(w, "_showCaptureHint"));           // témoin : l'invite de capture est affichée
            Win32.SetFocus(BancCapture.Handle(w));                                       // perte de focus : WM_KILLFOCUS clôt la capture
            BancCapture.Pump(2);
            Assert.Equal(IntPtr.Zero, BancCapture.Field<IntPtr>(w, "_focusedShortcut"));
            Win32.SetFocus(champ);
            BancCapture.Pump(2);
            Assert.Equal(champ, BancCapture.Field<IntPtr>(w, "_focusedShortcut"));       // témoin : capture rouverte
            w.SetInputPaused(true);
            Assert.Equal(IntPtr.Zero, BancCapture.Field<IntPtr>(w, "_focusedShortcut"));
            Assert.False(OutilsAccueilReglages130.Bool(w, "_showCaptureHint"));
        }
        finally { BancCapture.Teardown(w); }
    }

    // M8.1 I5 : un handle GDI n'est supprimé qu'une fois. Dispose supprime _hSurfaceBrush sans remettre
    // le champ à zéro : un second Dispose le supprimerait de nouveau (doc : non garanti, jugé non
    // atteignable via I4). GetObjectType ne voit pas la destruction d'un pinceau plein (cache gdi32,
    // NativeWindowTests.cs:137) : la preuve est l'état du champ après le premier Dispose.
    [Fact]
    public void M8_1_I5_DoubleDispose_LePinceauNEstSupprimeQuUneFois()
    {
        using var iso = new BancCapture.Isolation();
        var w = new SettingsWindow();
        IntPtr hwnd = BancCapture.Handle(w);
        Assert.True(Win32.IsWindow(hwnd));                                               // témoin : fenêtre vivante
        Assert.NotEqual(IntPtr.Zero, BancCapture.Field<IntPtr>(w, "_hSurfaceBrush"));    // témoin : pinceau posé

        w.Dispose();
        BancCapture.Pump(5);
        Assert.False(Win32.IsWindow(hwnd));                                              // témoin : fenêtre détruite
        Assert.Null(Record.Exception(() => w.Dispose()));                                // témoin : le second Dispose ne lève pas
        IntPtr reste = BancCapture.Field<IntPtr>(w, "_hSurfaceBrush");
        Assert.True(reste == IntPtr.Zero,
            $"Dispose a supprimé _hSurfaceBrush sans remettre le champ à zéro (reste {reste}) : un second Dispose le supprimerait de nouveau");
    }
}

/// <summary>Recherche de caractère : surbrillance et copie de secours (M8.2).</summary>
public sealed class Regles130AccueilReglagesRechercheTests
{
    private static CharacterSearch Creer(Func<string, TypingEngine.Windows.TextEmissionResult>? emission = null) =>
        new(new TextInsertionService(emission ?? (_ => default), activateTarget: _ => true,
            canInsert: () => true, targetAboveUs: _ => false));

    // M8.2 I1 : la fenêtre masquée ne laisse pas de surbrillance au clavier virtuel. Hide notifie
    // SelectionChanged(null). Une sélection non nulle est d'abord notifiée (témoin), sinon retirer
    // l'appel ne changerait rien.
    [Fact]
    public void M8_2_I1_Hide_EffaceLaSurbrillanceDuClavierVirtuel()
    {
        using var iso = new BancCapture.Isolation();
        var recherche = Creer();
        try
        {
            var recues = new List<MethodData?>();
            recherche.SelectionChanged += recues.Add;
            recherche.Show();
            Win32.SetWindowTextW(BancCapture.Field<IntPtr>(recherche, "_hEdit"), "e");
            Assert.True(BancCapture.Field<System.Collections.IList>(recherche, "_filteredResults").Count > 0);
            BancCapture.SetField(recherche, "_selectedIndex", 0);
            BancCapture.Call(recherche, "NotifySelectionChanged");
            Assert.NotEmpty(recues);
            Assert.NotNull(recues[^1]);                                                   // témoin : une surbrillance est demandée

            recherche.Hide();
            Assert.False(recherche.IsVisible);
            Assert.Null(recues[^1]);
        }
        finally { BancCapture.Teardown(recherche); }
    }

    // M8.2 I3 : la copie de secours ne part que si aucun événement n'a été émis. Émission partielle ou
    // complète : le presse-papiers n'est pas touché ; aucune émission : il l'est (témoin, en premier).
    // Émission, activation de la cible et presse-papiers sont tous injectés : aucune frappe réelle.
    [Fact]
    public void M8_2_I3_CopieDeSecours_SeulementSiRienNAEteEmis()
    {
        using var iso = new BancCapture.Isolation();
        var emis = new TypingEngine.Windows.TextEmissionResult(2, 0);
        var recherche = Creer(_ => emis);
        try
        {
            var copies = new List<string>();
            recherche.ClipboardWriter = (_, texte) => { copies.Add(texte); return true; };
            recherche.Show();
            Win32.SetWindowTextW(BancCapture.Field<IntPtr>(recherche, "_hEdit"), "e");
            Assert.True(BancCapture.Field<System.Collections.IList>(recherche, "_filteredResults").Count > 0);
            BancCapture.SetField(recherche, "_selectedIndex", 0);
            BancCapture.SetField(recherche, "_targetWindow", (IntPtr)0x1234);            // cible factice, jamais activée pour de vrai

            emis = new TypingEngine.Windows.TextEmissionResult(2, 0);                    // rien émis
            BancCapture.Call(recherche, "InsertSelectedCharacter");
            Assert.Single(copies);                                                       // témoin : la copie de secours part

            copies.Clear();
            emis = new TypingEngine.Windows.TextEmissionResult(2, 1);                    // un événement sur deux : partiel
            BancCapture.Call(recherche, "InsertSelectedCharacter");
            emis = new TypingEngine.Windows.TextEmissionResult(2, 2);                    // complet
            BancCapture.Call(recherche, "InsertSelectedCharacter");
            Assert.Empty(copies);
        }
        finally { BancCapture.Teardown(recherche); }
    }
}

/// <summary>Clavier virtuel et couches maintenables : état masqué (M8.3, M8.4).</summary>
[Collection("DialogNavigation")]   // lit DialogNavigation.RegisteredCount, comme DialogNavigationTests
public sealed class Regles130AccueilReglagesClavierCouchesTests
{
    // M8.3 I1 : pas de surbrillance ni de minuterie quand masqué. Visible, NotifyKeyPress pose le
    // scancode et arme TIMER_KEYPRESS (témoin ; KillTimer le désarme) ; masqué, ni l'un ni l'autre,
    // et HighlightMethod est ignoré.
    [Fact]
    public void M8_3_I1_ClavierMasque_PasDeSurbrillanceNiDeMinuterie()
    {
        using var iso = new BancCapture.Isolation();
        var clavier = new VirtualKeyboard(LayoutLoader.LoadFromResource());
        try
        {
            IntPtr hwnd = BancCapture.Handle(clavier);
            var methode = new MethodData { Type = "direct", Key = "KeyQ", Layer = "Base" };
            clavier.Show();
            clavier.NotifyKeyPress(0x1E);
            Assert.Equal(0x1Eu, BancCapture.Field<uint>(clavier, "_pressedScancode"));   // témoin : surbrillance posée
            Assert.True(Win32.KillTimer(hwnd, (UIntPtr)1));                              // témoin : minuterie armée (et désarmée ici)
            clavier.HighlightMethod(methode);
            Assert.NotNull(BancCapture.Field<object?>(clavier, "_gesture"));             // témoin : le geste est montré

            clavier.Hide();
            BancCapture.SetField(clavier, "_pressedScancode", 0u);
            clavier.NotifyKeyPress(0x1F);
            clavier.HighlightMethod(methode);
            Assert.Null(BancCapture.Field<object?>(clavier, "_gesture"));
            Assert.Equal(0u, BancCapture.Field<uint>(clavier, "_pressedScancode"));
            Assert.False(Win32.KillTimer(hwnd, (UIntPtr)1), "une minuterie de surbrillance a été armée fenêtre masquée");
        }
        finally { BancCapture.Teardown(clavier); }
    }

    // M8.3 I3 : bornes enregistrées seulement quand visible. WM_SIZE visible écrit les bornes (témoin,
    // elles sont effacées avant) ; WM_SIZE masqué n'écrit rien. Hide enregistre lui-même les bornes :
    // elles sont donc effacées de nouveau après Hide, avant le message masqué.
    [Fact]
    public void M8_3_I3_WmSize_EnregistreLesBornesSeulementQuandVisible()
    {
        using var iso = new BancCapture.Isolation();
        string cle = ConfigManager.VirtualKeyboardBoundsKey;
        ConfigManager.ClearWindowBounds(cle);
        var clavier = new VirtualKeyboard(LayoutLoader.LoadFromResource());
        try
        {
            IntPtr hwnd = BancCapture.Handle(clavier);
            clavier.Show();
            ConfigManager.ClearWindowBounds(cle);
            Win32.SendMessageW(hwnd, Win32.WM_SIZE, IntPtr.Zero, IntPtr.Zero);
            Assert.True(ConfigManager.TryGetWindowBounds(cle, out _));                   // témoin : visible, les bornes sont écrites

            clavier.Hide();
            ConfigManager.ClearWindowBounds(cle);
            Assert.False(ConfigManager.TryGetWindowBounds(cle, out _));                  // témoin : effacées
            Win32.SendMessageW(hwnd, Win32.WM_SIZE, IntPtr.Zero, IntPtr.Zero);
            Assert.False(ConfigManager.TryGetWindowBounds(cle, out _), "WM_SIZE masqué a enregistré les bornes");
        }
        finally { BancCapture.Teardown(clavier); }
    }

    // M8.4 I3 : inscrite dans DialogNavigation seulement quand visible. Référence = le compte de
    // départ (d'autres fenêtres du processus peuvent être inscrites). Show inscrit (une fois, même
    // répété), Hide et WM_CLOSE désinscrivent, Dispose désinscrit sans doublon.
    [Fact]
    public void M8_4_I3_CouchesMaintenables_InscriteSeulementQuandVisible()
    {
        using var iso = new BancCapture.Isolation();
        int depart = DialogNavigation.RegisteredCount;
        var fenetre = new MaintainableLayersWindow();
        bool detruite = false;
        try
        {
            Assert.Equal(depart, DialogNavigation.RegisteredCount);                      // masquée : pas inscrite
            fenetre.Show();
            Assert.Equal(depart + 1, DialogNavigation.RegisteredCount);                  // témoin : visible, inscrite
            fenetre.Show();
            Assert.Equal(depart + 1, DialogNavigation.RegisteredCount);                  // pas de doublon
            fenetre.Hide();
            Assert.Equal(depart, DialogNavigation.RegisteredCount);
            fenetre.Show();
            Win32.SendMessageW(BancCapture.Handle(fenetre), Win32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            Assert.Equal(depart, DialogNavigation.RegisteredCount);                      // croix : désinscrite

            fenetre.Show();
            Assert.Equal(depart + 1, DialogNavigation.RegisteredCount);                  // témoin : inscrite avant Dispose
            fenetre.Dispose();
            detruite = true;
            BancCapture.Pump(5);
            Assert.Equal(depart, DialogNavigation.RegisteredCount);
        }
        finally
        {
            if (!detruite) BancCapture.Teardown(fenetre);
        }
    }
}

/// <summary>Désinscription avant destruction des fenêtres natives (M8.5).</summary>
[Collection("DialogNavigation")]
public sealed class Regles130AccueilReglagesNavigationTests
{
    private static readonly UIntPtr IdSousClasse = (UIntPtr)0x7E57;

    /// <summary>
    /// Crée une fenêtre, la fait détruire par son Dispose, et relève le compte des fenêtres inscrites
    /// au moment du WM_DESTROY (une sous-classe posée par le test le voit avant la fenêtre) : retrouver
    /// le compte de départ APRÈS le Dispose ne prouverait pas l'ordre « désinscrire puis détruire ».
    /// </summary>
    private static string Verifier(string nom, bool inscriteALaCreation, Func<(IntPtr Hwnd, Action Detruire)> creer)
    {
        int depart = DialogNavigation.RegisteredCount;
        var (hwnd, detruire) = creer();
        int apresCreation = DialogNavigation.RegisteredCount;
        int auDestroy = int.MinValue;
        Win32.SUBCLASSPROC sonde = (h, msg, wParam, lParam, id, data) =>
        {
            if (msg == Win32.WM_DESTROY) auDestroy = DialogNavigation.RegisteredCount;
            return Win32.DefSubclassProc(h, msg, wParam, lParam);
        };
        if (!Win32.SetWindowSubclass(hwnd, sonde, IdSousClasse, IntPtr.Zero))
            return $"{nom} : sonde non posée (montage cassé)";
        detruire();
        BancCapture.Pump(5);
        GC.KeepAlive(sonde);

        if (inscriteALaCreation && apresCreation != depart + 1)
            return $"{nom} : non inscrite à l'ouverture ({depart} puis {apresCreation}), témoin manquant";
        if (auDestroy == int.MinValue)
            return $"{nom} : WM_DESTROY non vu (montage cassé)";
        if (auDestroy != depart)
            return $"{nom} : encore inscrite au WM_DESTROY (compte {auDestroy}, départ {depart})";
        int apres = DialogNavigation.RegisteredCount;
        return apres != depart ? $"{nom} : compte {apres} après Dispose, départ {depart}" : "";
    }

    // M8.5 I2 : une fenêtre est désinscrite avant destruction. Les cinq fenêtres inscrites que couvre la
    // règle ; les constats sont réunis pour que le message dise laquelle manque.
    [Fact]
    public void M8_5_I2_Dispose_DesinscritAvantDeDetruire_ChaqueFenetreInscrite()
    {
        using var env = new OutilsAccueilReglages130.EnvAccueil();
        using var iso = new BancCapture.Isolation();
        var constats = new List<string>
        {
            Verifier("AboutWindow", true, () =>
            {
                var f = new AboutWindow();
                return (BancCapture.Handle(f), f.Dispose);
            }),
            Verifier("LayoutConflictWindow", true, () =>
            {
                var f = new LayoutConflictWindow(false, () => { }, () => { });
                return (BancCapture.Handle(f), f.Dispose);
            }),
            Verifier("SettingsWindow", true, () =>
            {
                var f = new SettingsWindow();
                return (BancCapture.Handle(f), f.Dispose);
            }),
            Verifier("OnboardingWindow", true, () =>
            {
                var f = OutilsAccueilReglages130.OuvrirAccueil();
                return (BancCapture.Handle(f), f.Dispose);
            }),
            Verifier("MaintainableLayersWindow", true, () =>
            {
                var f = new MaintainableLayersWindow();
                f.Show();
                return (BancCapture.Handle(f), f.Dispose);
            }),
        }.Where(c => c.Length > 0).ToList();

        Assert.True(constats.Count == 0, string.Join(" | ", constats));
    }
}
