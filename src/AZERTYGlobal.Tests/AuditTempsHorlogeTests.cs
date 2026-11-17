using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit temps et horloge de l'app 1.3.0 (17/11/2026, docs/audit-temps-horloge.md). Chaque test
/// affirme le comportement CORRECT quand l'heure de Windows bouge (horloge avancée puis remise,
/// resynchronisation qui recule l'heure, passage de minuit fenêtre fermée) et échoue tant que le
/// défaut nommé (constat TH-xx) n'est pas corrigé. Aucun test ne touche à l'heure ni au fuseau de
/// la machine : l'heure s'injecte par les horloges que le produit expose déjà, ou s'écrit dans
/// les dates stockées, relatives au vrai « aujourd'hui » capturé une fois par test.
/// </summary>
public sealed class AuditTempsHorlogeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "AZGTEMPS_" + Guid.NewGuid().ToString("N"));
    private string ProgressPath => Path.Combine(_dir, "lessons-progress.json");

    public AuditTempsHorlogeTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        for (int essai = 0; essai < 20; essai++)
        {
            try { Directory.Delete(_dir, true); return; }
            catch (IOException) { Thread.Sleep(25); }
            catch (UnauthorizedAccessException) { Thread.Sleep(25); }
        }
    }

    private static readonly DateTimeOffset Debut = new(2026, 11, 17, 18, 0, 0, TimeSpan.Zero);

    /// <summary>60 frappes justes, une par seconde de temps réel ; l'horloge murale recule de
    /// <paramref name="reculSecondes"/> juste avant la fin (resynchronisation de l'heure).
    /// Les deux horloges du produit sont injectées : l'heure murale (date de l'essai) et
    /// l'horloge des durées (<see cref="MonotonicClock"/> en production, TT-2), qui avance d'une
    /// seconde par frappe et ignore le recul.</summary>
    private static LessonAttemptStats ExerciceDeSoixanteSecondes(int reculSecondes)
    {
        var mur = Debut;
        var duree = TimeSpan.FromHours(3); // origine arbitraire, comme le compte de Windows
        var origine = duree;
        var stats = new LessonAttemptStats(() => mur, () => duree);
        for (int i = 0; i < 60; i++)
        {
            mur = Debut.AddSeconds(i);
            duree = origine + TimeSpan.FromSeconds(i);
            stats.RecordChar('a', 'a');
        }
        mur = Debut.AddSeconds(60 - reculSecondes);
        duree = origine + TimeSpan.FromSeconds(60);
        stats.Complete();
        return stats;
    }

    private static LessonExercise Exercice() =>
        new("m", "l", 0, "practice", "Tape", new string('a', 60), LessonTypingMode.Flexible);

    // ═══════════════════════════════════════════════════════════════
    // TH-01 : la durée d'un exercice se mesurait sur l'horloge murale (LessonAttemptStats,
    // DateTimeOffset.UtcNow par défaut). Une heure qui recule pendant l'exercice raccourcissait
    // la durée, gonflait la vitesse, et le record (BestWpm : Max, BestSeconds : Min,
    // TrainingBestWpm, texte partagé du Défi) la gardait à vie. Correctif TT-2 : durées sur
    // MonotonicClock (hors veille, décision d'Antoine), injectable à côté de l'heure murale ;
    // idem pour le mode Libre (LessonsWindow.ElapsedClock).
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Audit", "temps-horloge")]
    public void TH01_Une_heure_qui_recule_pendant_l_exercice_ne_raccourcit_pas_sa_duree()
    {
        var stats = ExerciceDeSoixanteSecondes(reculSecondes: 50);

        // 60 frappes à une seconde d'intervalle : au moins 59 s se sont écoulées.
        Assert.True(stats.ElapsedSeconds >= 59,
            $"TH-01 : durée {stats.ElapsedSeconds:0} s pour 59 s réelles ; vitesse affichée {stats.Wpm} mots/min au lieu de 12");
    }

    [Fact]
    [Trait("Audit", "temps-horloge")]
    public void TH01_Un_record_gonfle_par_l_horloge_ne_reste_pas_a_vie()
    {
        var store = new LessonProgressStore(ProgressPath);
        var exercice = Exercice();
        store.RecordSuccess(exercice, ExerciceDeSoixanteSecondes(reculSecondes: 50), _ => false);
        store.RecordSuccess(exercice, ExerciceDeSoixanteSecondes(reculSecondes: 0), _ => false); // essai honnête

        var progres = new LessonProgressStore(ProgressPath).GetValidProgress(exercice);
        Assert.NotNull(progres);
        Assert.True(progres!.BestWpm <= 12 && progres.BestSeconds >= 59,
            $"TH-01 : record gardé {progres.BestWpm} mots/min en {progres.BestSeconds:0} s ; vitesse réelle 12 mots/min en 59 s");
    }

    [Fact]
    [Trait("Audit", "temps-horloge")]
    public void TH01_Sans_horloge_injectee_la_duree_se_lit_sur_l_horloge_hors_veille()
    {
        // Production : aucune horloge injectée. La durée doit venir de MonotonicClock (hors veille,
        // insensible à l'heure de Windows) : deux lectures ne reculent jamais, et une durée
        // mesurée ne dépasse pas le temps réel écoulé autour d'elle.
        var avant = MonotonicClock.Now();
        var stats = new LessonAttemptStats();
        stats.RecordChar('a', 'a');
        Thread.Sleep(30);
        stats.Complete();
        var apres = MonotonicClock.Now();

        Assert.True(apres >= avant, $"MonotonicClock a reculé : {avant} puis {apres}");
        Assert.InRange(stats.ElapsedSeconds, 0.0, (apres - avant).TotalSeconds);
        Assert.True(stats.ElapsedSeconds > 0, "durée nulle : l'horloge des durées n'a pas été lue");
    }

    [Fact]
    [Trait("Audit", "temps-horloge")]
    public void TH01_Mode_Libre_mesure_sa_vitesse_sur_l_horloge_des_durees()
    {
        // Le mode Libre affiche vitesse et durée (BuildFreeStatsText) : elles se lisent sur
        // LessonsWindow.ElapsedClock, plus sur l'heure murale. 60 caractères en 60 s : 12 mots/min.
        using var isolation = new BancCapture.Isolation();
        var layout = LayoutLoader.LoadFromResource();
        var mapper = new KeyMapper(layout, new MockWin32Api());
        var hook = new KeyboardHook(mapper); // jamais installé
        ConfigManager.ClearWindowBounds(ConfigManager.LessonsWindowBoundsKey);
        var fenetre = new LessonsWindow(layout, mapper, hook);
        try
        {
            var duree = TimeSpan.FromHours(3);
            fenetre.ElapsedClock = () => duree;
            var modeType = typeof(LessonsWindow).GetNestedType("WindowMode", BindingFlags.NonPublic)!;
            BancCapture.Call(fenetre, "SwitchMode", Enum.Parse(modeType, "Free"));
            for (int i = 0; i < 60; i++)
                BancCapture.Call(fenetre, "OnChar", 'a');
            duree += TimeSpan.FromSeconds(60);

            string texte = (string)BancCapture.Call(fenetre, "BuildFreeStatsText")!;
            Assert.Equal(L.LessonsWin_FreeStatsFilled("12", 60, 60, 0), texte);
        }
        finally
        {
            BancCapture.Teardown(fenetre);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // TH-02 : dates futures que la réparation ME-10 ne couvre pas. Horloge avancée au premier
    // lancement d'une version, puis remise : currentVersionFirstRunDate reste dans le futur, et
    // l'essai 1 de l'avis (FirstAttemptAllowed : today > date) attend que la vraie date la dépasse.
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Audit", "temps-horloge")]
    public void TH02_Premier_lancement_de_version_date_dans_le_futur_est_ramene_a_aujourd_hui()
    {
        using var isolation = new BancCapture.Isolation(); // config temporaire, rendue à la fin
        var aujourdhui = DateOnly.FromDateTime(DateTime.Now);
        string config = (string)typeof(ConfigManager).GetField("_configPath", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        string futur = aujourdhui.AddDays(60).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        File.WriteAllText(config,
            $$"""{ "appLanguage": "fr", "currentVersionFirstRunVersion": "1.3.0", "currentVersionFirstRunDate": "{{futur}}", "currentVersionUpgradedWithUsage": true }""");
        ConfigManager.OverrideConfigPathForTests(config);

        var lancement = ConfigManager.EnsureCurrentVersionFirstRun("1.3.0", aujourdhui, hadUsageBefore: true);

        Assert.True(lancement.Date <= aujourdhui,
            $"TH-02 : premier lancement daté du {lancement.Date:yyyy-MM-dd}, après aujourd'hui ({aujourdhui:yyyy-MM-dd}) ; " +
            $"essai 1 de l'avis permis demain : {ReviewPromptGate.FirstAttemptAllowed(ReviewPromptTrigger.QuietTyping, aujourdhui.AddDays(1), true, lancement.Date)}");
    }

    // TH-02b : essai 1 de l'avis affiché horloge en avance ; l'heure remise, l'essai 2 attendait
    // cette date future plus SecondMinGapDays. Réparé à chaque décision (MaybeShowReviewPrompt).
    [Fact]
    [Trait("Audit", "temps-horloge")]
    public void TH02b_Dernier_essai_d_avis_date_dans_le_futur_ne_repousse_pas_l_essai_2()
    {
        using var isolation = new BancCapture.Isolation();
        var aujourdhui = DateOnly.FromDateTime(DateTime.Now);
        string config = (string)typeof(ConfigManager).GetField("_configPath", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        string futur = aujourdhui.AddDays(60).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        File.WriteAllText(config,
            $$"""{ "appLanguage": "fr", "reviewPromptCount": 1, "reviewPromptLastShown": "{{futur}}" }""");
        ConfigManager.OverrideConfigPathForTests(config);

        ConfigManager.RepairFutureReviewDates(aujourdhui);

        var etat = ReviewState.Load();
        Assert.True(etat.LastShown <= aujourdhui, $"TH-02b : dernier essai daté du {etat.LastShown:yyyy-MM-dd}");
        int ecart = ReviewPromptGate.SecondMinGapDays;
        Assert.True(etat.LastShown.HasValue && aujourdhui.AddDays(ecart).DayNumber - etat.LastShown.Value.DayNumber >= ecart,
            "TH-02b : l'essai 2 reste repoussé au-delà de 7 jours après l'heure remise");
    }

    // ═══════════════════════════════════════════════════════════════
    // TH-03 : un caractère révisé quand l'horloge était en avance reçoit une échéance
    // (DueUtc) dans le futur de cette horloge ; l'heure remise, il sort de la Révision du jour
    // et compte pour « maîtrisé » jusqu'à ce que la vraie date rattrape l'échéance, ou que le
    // caractère soit retapé ailleurs. Invariant : DueUtc <= maintenant + intervalle de sa boîte.
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Audit", "temps-horloge")]
    public void TH03_Echeance_d_un_geste_revise_horloge_en_avance_ne_depasse_pas_l_intervalle_de_sa_boite()
    {
        var maintenant = DateTimeOffset.UtcNow;
        var stats = new LessonAttemptStats(() => maintenant.AddDays(60));
        stats.RecordChar('é', 'é');
        new LessonProgressStore(ProgressPath, () => maintenant.AddDays(60)).RecordGestures(stats, _ => true);

        var store = new LessonProgressStore(ProgressPath, () => maintenant); // heure remise
        var geste = store.Gestures["é"];
        var borne = maintenant + GestureMastery.IntervalFor(geste.Box);
        Assert.True(geste.DueUtc <= borne,
            $"TH-03 : échéance {geste.DueUtc:yyyy-MM-dd}, au-delà de {borne:yyyy-MM-dd} (boîte {geste.Box}) ; " +
            $"à revoir aujourd'hui : {store.DueGestures(10).Count} caractère(s)");
    }

    // ═══════════════════════════════════════════════════════════════
    // TH-04 : l'échéance d'une pause est une heure murale (_pauseUntilUtc). Pause de 15 min
    // posée horloge avancée de 60 jours, puis heure remise : la frappe restait en pause 60 jours.
    // Rouge sur c16866b par l'échéance posée directement ; le correctif ajoute l'horloge
    // injectable TrayApplication.UtcClock (QCM du 17/11) et le test passe par elle.
    // ═══════════════════════════════════════════════════════════════

    private const uint TimerPause = 9030; // TrayApplication.TIMER_PAUSE

    private static (TrayApplication Tray, Action Tic) TrayEnPause(Func<DateTimeOffset> horloge)
    {
        var tray = (TrayApplication)RuntimeHelpers.GetUninitializedObject(typeof(TrayApplication));
        tray.Shell = new FauxTrayShell();
        tray.UtcClock = horloge;
        BancCapture.SetField(tray, "_hWnd", (IntPtr)0x1234);
        BancCapture.SetField(tray, "_tooltip", new TrayApplication.TooltipPublisher());
        BancCapture.SetField(tray, "_activationConsent", true);
        BancCapture.SetField(tray, "_enabled", true);
        var wndProc = (Win32.WNDPROC)Delegate.CreateDelegate(typeof(Win32.WNDPROC), tray,
            typeof(TrayApplication).GetMethod("WndProcCallback", BindingFlags.Instance | BindingFlags.NonPublic)!);
        BancCapture.Call(tray, "StartPause", TimeSpan.FromMinutes(15));
        return (tray, () => wndProc((IntPtr)0x1234, Win32.WM_TIMER, (IntPtr)TimerPause, IntPtr.Zero));
    }

    private static DateTimeOffset? Echeance(TrayApplication tray) => BancCapture.Field<DateTimeOffset?>(tray, "_pauseUntilUtc");

    [Fact]
    [Trait("Audit", "temps-horloge")]
    public void TH04_Pause_posee_horloge_en_avance_ne_dure_pas_plus_que_la_plus_longue_pause_possible()
    {
        using var iso = new BancCapture.Isolation();
        var mur = Debut.AddDays(60); // horloge en avance au moment de la pause
        var (tray, tic) = TrayEnPause(() => mur);

        mur = Debut.AddSeconds(1); // heure remise, tic suivant
        tic();

        Assert.True(Echeance(tray) is { } fin && fin - mur <= TimeSpan.FromMinutes(15),
            $"TH-04 : frappe en pause jusqu'au {Echeance(tray):yyyy-MM-dd HH:mm} UTC, pour une pause de 15 min");
    }

    [Fact]
    [Trait("Audit", "temps-horloge")]
    public void TH04_Heure_qui_recule_pendant_la_pause_ne_la_prolonge_pas()
    {
        using var iso = new BancCapture.Isolation();
        var mur = Debut;
        var (tray, tic) = TrayEnPause(() => mur);

        mur = Debut.AddMinutes(5); tic();              // 5 min réelles
        mur = Debut.AddMinutes(5).AddHours(-1); tic(); // resynchronisation : -1 h
        mur = mur.AddMinutes(10).AddSeconds(1); tic(); // 10 min réelles de plus : 15 min écoulées

        Assert.Null(Echeance(tray));
    }

    [Fact]
    [Trait("Audit", "temps-horloge")]
    public void TH04_Veille_plus_longue_que_la_pause_la_termine_au_reveil()
    {
        using var iso = new BancCapture.Isolation();
        var mur = Debut;
        var (tray, tic) = TrayEnPause(() => mur);

        mur = Debut.AddHours(2); // veille de 2 h, réveil
        tic();

        Assert.Null(Echeance(tray));
    }

    // ═══════════════════════════════════════════════════════════════
    // TH-06 : Leçons fermées sur le Défi avant minuit, rouvertes après. Show() reconstruit le
    // module (RefreshChallengeModuleIfStale : date du module = aujourd'hui) sans relancer la
    // séance : « Reprendre » remet l'extrait d'hier, et le finir compte le Défi d'aujourd'hui.
    // Distinct de L17 (Audit0911Lot8Tests) : fenêtre restée ouverte, module jamais reconstruit.
    // ═══════════════════════════════════════════════════════════════

    private static LessonsWindow OuvrirLecons()
    {
        var layout = LayoutLoader.LoadFromResource();
        var mapper = new KeyMapper(layout, new MockWin32Api());
        var hook = new KeyboardHook(mapper); // jamais installé
        ConfigManager.ClearWindowBounds(ConfigManager.LessonsWindowBoundsKey);
        return new LessonsWindow(layout, mapper, hook);
    }

    [Fact]
    [Trait("Audit", "temps-horloge")]
    public void TH06_Defi_d_hier_repris_apres_minuit_compte_pour_le_jour_de_son_extrait()
    {
        using var isolation = new BancCapture.Isolation();
        ConfigManager.SetTrainingEnabled(true);
        uint etape = DailyChallenge.SequenceLength; // défi commun
        ConfigManager.SetTrainingSequenceIndex(etape);
        var aujourdhui = DateOnly.FromDateTime(DateTime.Now);
        var hier = aujourdhui.AddDays(-1);
        var fenetre = OuvrirLecons();
        try
        {
            Assert.True(fenetre.OpenChallenge());

            // État laissé par une ouverture la veille au soir : catalogue, séance de l'extrait
            // et clé du Défi construits hier.
            var moduleHier = DailyChallenge.BuildModule(hier, etape)!;
            BancCapture.SetField(fenetre, "_catalog",
                new LessonCatalog(LessonCatalogLoader.LoadFromResource().Modules.Append(moduleHier).ToArray()));
            BancCapture.SetField(fenetre, "_exerciseIndex", DailyChallenge.ExtractExerciseIndex);
            BancCapture.Call(fenetre, "StartCurrentSession", false);
            var defi = BancCapture.Field<ChallengeSessionState>(fenetre, "_challenge");
            BancCapture.SetField(defi, "_moduleDate", hier);
            BancCapture.SetField(defi, "_lessonKey", $"{hier:yyyyMMdd}/{etape}/{L.Language}");
            Assert.Equal($"defi-{hier:yyyyMMdd}", BancCapture.Field<LessonTypingSession>(fenetre, "_session").Exercise.LessonId);

            // Réouverture après minuit (Show, fenêtre cachée : RefreshChallengeModuleIfStale, puis
            // l'accueil), onglet « Toutes les leçons » (HomeShowExercise : la séance en cours
            // reprend telle quelle), puis frappe.
            BancCapture.Call(fenetre, "RefreshChallengeModuleIfStale");
            BancCapture.Call(fenetre, "HomeShowExercise");
            var seance = BancCapture.Field<LessonTypingSession>(fenetre, "_session");
            string extraitTape = seance.Exercise.LessonId;
            for (int garde = 0; garde < 5000 && !BancCapture.Field<bool>(fenetre, "_showSummary"); garde++)
            {
                var s = BancCapture.Field<LessonTypingSession>(fenetre, "_session");
                Assert.True(s.GetNextExpectedCharacter() is char, "séance bloquée avant le bilan");
                BancCapture.Call(fenetre, "OnChar", s.GetNextExpectedCharacter()!.Value);
            }
            Assert.True(BancCapture.Field<bool>(fenetre, "_showSummary"));

            string compte = "defi-" + (StoredDate.Parse(ConfigManager.TrainingLastSessionDate)?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? "aucun");
            Assert.True(compte == extraitTape,
                $"TH-06 : extrait tapé {extraitTape}, séance comptée pour {compte}");
        }
        finally { BancCapture.Teardown(fenetre); }
    }
}
