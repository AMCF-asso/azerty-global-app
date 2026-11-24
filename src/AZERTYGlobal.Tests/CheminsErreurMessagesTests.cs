using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit des chemins d'erreur, rapport-F : appels Windows dont le retour n'est pas lu.
/// Panne provoquée par une poignée de fenêtre détruite : PostMessageW et SetTimer y échouent.
/// La poignée est celle d'une fenêtre créée puis détruite par le test lui-même ; aucun objet de
/// l'application réelle n'est visé.
/// </summary>
public sealed class CheminsErreurMessagesTests : IDisposable
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Statique = BindingFlags.Static | BindingFlags.NonPublic;

    private readonly List<string> _dossiers = new();
    private readonly Func<IntPtr, bool, bool> _minuterieOrigine = TrayApplication.ReviewTimerSwitch;

    public void Dispose()
    {
        TrayApplication.ReviewTimerSwitch = _minuterieOrigine;
        foreach (var dossier in _dossiers)
            try { Directory.Delete(dossier, true); } catch { }
    }

    /// <summary>Fenêtre créée puis détruite sur un fil STA : la poignée rendue est invalide.</summary>
    private static IntPtr PoigneeDetruite()
    {
        IntPtr hwnd = IntPtr.Zero;
        var fil = new Thread(() =>
        {
            hwnd = Win32.CreateWindowExW(0, "STATIC", "x", Win32.WS_POPUP,
                0, 0, 10, 10, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            Win32.DestroyWindow(hwnd);
        });
        fil.SetApartmentState(ApartmentState.STA);
        fil.Start();
        fil.Join();
        Assert.NotEqual(IntPtr.Zero, hwnd);
        // Prémisse : les deux appels testés échouent bien sur cette poignée.
        Assert.False(Win32.PostMessageW(hwnd, 0x8000 + 1, IntPtr.Zero, IntPtr.Zero));
        Assert.Equal(UIntPtr.Zero, Win32.SetTimer(hwnd, (UIntPtr)1, 1000, IntPtr.Zero));
        return hwnd;
    }

    private static void PoserChamp<T>(object cible, string nom, T valeur) =>
        cible.GetType().GetField(nom, Instance)!.SetValue(cible, valeur);

    private static T LireChamp<T>(object cible, string nom) =>
        (T)cible.GetType().GetField(nom, Instance)!.GetValue(cible)!;

    // ── S1 — W1074 ───────────────────────────────────────────────────────

    // rapport-F S1 (W1074)
    [Fact]
    [Trait("Audit", "chemins-erreur")]
    public void EtatTray_PostMessageRate_NeLaisseAucunDrapeauEnFileColle()
    {
        IntPtr morte = PoigneeDetruite();
        var tray = (TrayApplication)RuntimeHelpers.GetUninitializedObject(typeof(TrayApplication));
        PoserChamp(tray, "_hWnd", morte);

        typeof(TrayApplication).GetMethod("OnStateChanged", Instance)!.Invoke(tray, null);

        // Le message n'a pas pu partir : aucun message n'est donc « en file ».
        Assert.Equal(0, LireChamp<int>(tray, "_stateRefreshQueued"));
    }

    // ── S2 — W338 ────────────────────────────────────────────────────────

    private sealed class HoteMuet : ILessonsAutomationHost
    {
        public IntPtr Handle { get; set; }
        public LessonSurfaceView? SurfaceView => null;
        public IReadOnlyList<LessonControlView> Controls { get; } = Array.Empty<LessonControlView>();
        public bool WindowHasFocus => false;
        public bool SurfaceHasFocus => false;
        public int FocusedControl => -1;
        public void FocusSurface() { }
        public void FocusControl(int index) { }
        public void InvokeControl(int index, string name) { }
    }

    // rapport-F S2 (W338)
    [Fact]
    [Trait("Audit", "chemins-erreur")]
    public void Lecons_PostMessageRate_NeLaisseAucunEvenementUiaEnAttenteFictive()
    {
        IntPtr morte = PoigneeDetruite();
        var fenetre = (LessonsWindow)RuntimeHelpers.GetUninitializedObject(typeof(LessonsWindow));
        PoserChamp(fenetre, "_hWnd", morte);
        PoserChamp(fenetre, "_automation", new LessonsAutomation(new HoteMuet { Handle = morte }));

        typeof(LessonsWindow).GetMethod("RequestAutomationEvents", Instance)!.Invoke(fenetre, null);

        Assert.False(LireChamp<bool>(fenetre, "_automationEventPending"),
            "le message n'est pas parti : rien n'est en attente, la demande suivante doit pouvoir repartir");
    }

    // ── Environnement isolé (copie réduite de EnvAvis, MachinesEtatsLot7Tests.cs) ──

    private IDisposable Env(bool notifications)
    {
        string dossier = Path.Combine(Path.GetTempPath(), "AZGCE_F_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dossier);
        _dossiers.Add(dossier);
        var politique = PolicyManager.OverrideForTests((_, _) => null);
        var canal = AppChannel.OverrideForTests(DistributionChannel.Store);
        LearningSessionTracker.ResetForTests();
        typeof(ConfigManager).GetField("_lastErrorUtc", Statique)!.SetValue(null, null);

        string config = Path.Combine(dossier, "config.json");
        File.WriteAllText(config, JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["notificationsEnabled"] = notifications,
        }));
        ConfigManager.OverrideConfigPathForTests(config);

        string Jour(DateOnly d) => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var aujourdhui = DateOnly.FromDateTime(DateTime.Now);
        string stats = Path.Combine(dossier, "usage-stats.json");
        File.WriteAllText(stats, JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["activeDaysCount"] = 10,
            ["totalActiveMinutes"] = 10L,
            ["accentedUppercaseCount"] = 20L,
            ["firstRemapDate"] = Jour(aujourdhui.AddDays(-30)),
            ["lastActiveDate"] = Jour(aujourdhui),
        }));
        UsageStats.OverrideStatsPathForTests(stats);

        return new Fin(() =>
        {
            UsageStats.ClearEnrichedThresholdSignal();
            LearningSessionTracker.ResetForTests();
            canal.Dispose();
            politique.Dispose();
            ConfigManager.OverrideConfigPathForTests(Path.Combine(dossier, "fin.json"));
            UsageStats.OverrideStatsPathForTests(Path.Combine(dossier, "fin-stats.json"));
        });
    }

    private sealed class Fin : IDisposable
    {
        private readonly Action _fin;
        public Fin(Action fin) => _fin = fin;
        public void Dispose() => _fin();
    }

    // ── S3 — W1072 ───────────────────────────────────────────────────────

    // rapport-F S3 (W1072)
    [Fact]
    [Trait("Audit", "chemins-erreur")]
    public void Pause_SetTimerRate_NeLaisseAucunePauseSansEcheance()
    {
        using var env = Env(notifications: false); // ShowBalloon sort tout de suite
        IntPtr morte = PoigneeDetruite();
        var tray = (TrayApplication)RuntimeHelpers.GetUninitializedObject(typeof(TrayApplication));
        PoserChamp(tray, "_hWnd", morte);
        PoserChamp(tray, "_tooltip", new TrayApplication.TooltipPublisher()); // initialiseur de champ non exécuté
        PoserChamp(tray, "_enabled", true);

        try
        {
            typeof(TrayApplication).GetMethod("StartPauseUntil", Instance)!
                .Invoke(tray, new object[] { DateTimeOffset.UtcNow.AddMinutes(15), "Pause" });

            bool enPause = (bool)typeof(TrayApplication).GetProperty("IsPaused", Instance)!.GetValue(tray)!;
            // Seul le tick de TIMER_PAUSE peut lever la pause ; sans minuterie, elle ne finira jamais.
            Assert.False(enPause, "la pause ne doit pas rester active quand sa minuterie n'a pas pu être posée");
        }
        finally
        {
            IntPtr icone = LireChamp<IntPtr>(tray, "_hIcon");
            if (icone != IntPtr.Zero) Win32.DestroyIcon(icone);
        }
    }

    // ── S4 — W1088 ───────────────────────────────────────────────────────

    // rapport-F S4 (W1088)
    [Fact]
    [Trait("Audit", "chemins-erreur")]
    public void MinuterieAvis_SetTimerRate_NeSeDitPasPosee()
    {
        using var env = Env(notifications: true); // une sollicitation reste possible : la minuterie doit tourner
        IntPtr morte = PoigneeDetruite();
        var tray = (TrayApplication)RuntimeHelpers.GetUninitializedObject(typeof(TrayApplication));
        PoserChamp(tray, "_hWnd", morte);

        tray.EnsureReviewTimer(); // couture ReviewTimerSwitch par défaut : vrai SetTimer, qui échoue

        Assert.False(tray.ReviewTimerArmed,
            "SetTimer a échoué : la minuterie n'existe pas, elle ne doit pas être déclarée posée");
    }
}
