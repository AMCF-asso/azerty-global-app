using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Lot « couture du tray » des règles de docs/machines-a-etats.md (app 1.3.0) : M3.1, M3.2, M3.3,
/// M3.4, M3.6, M3.8, M7.2, M7.4, M7.5, M7.6, M8.1, M8.3. Le vrai <c>WndProcCallback</c> et les
/// vraies méthodes de <c>TrayApplication</c> tournent sur un tray non initialisé ;
/// <see cref="FauxTrayShell"/> remplace Windows et garde ce que l'application lui demande.
/// Le nom de chaque méthode commence par l'id de la règle. Verdict : vert si le produit tient la
/// règle ; rouge si elle n'est pas tenue (le test échoue alors sur sa dernière assertion, les
/// témoins avant). Le balayage de source passe par <see cref="SourcePur"/>.
/// </summary>
internal static class OutilsTray130
{
    internal const uint WmTrayIcon = 0x8001;          // TrayApplication.WM_TRAYICON (WM_APP + 1)
    internal const uint WmAppToggle = 0x8005;         // TrayApplication.WM_APP_TOGGLE (WM_APP + 5)
    internal const uint NinBalloonTimeout = 0x0404;
    internal const uint NinBalloonUserClick = 0x0405;
    internal const int IdmToggle = 1001;
    internal const int IdmPause15 = 1044;
    internal const int IdmOnboarding = 1010;
    internal const uint TimerPause = 9030;            // TrayApplication.TIMER_PAUSE
    internal const uint TimerReviewQuiet = 9050;      // TrayApplication.TIMER_REVIEW_QUIET
    internal const uint NimModify = 1, NimDelete = 2;
    internal static readonly IntPtr Fenetre = (IntPtr)0x1234;

    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Statique = BindingFlags.Static | BindingFlags.NonPublic;

    /// <summary>Tray non initialisé branché sur un faux shell (éventuellement enveloppé).</summary>
    internal static (TrayApplication Tray, FauxTrayShell Faux, Win32.WNDPROC WndProc) Tray(
        Func<FauxTrayShell, ITrayShell>? envelopper = null)
    {
        var tray = (TrayApplication)RuntimeHelpers.GetUninitializedObject(typeof(TrayApplication));
        var faux = new FauxTrayShell();
        tray.Shell = envelopper != null ? envelopper(faux) : faux;
        BancCapture.SetField(tray, "_hWnd", Fenetre);
        BancCapture.SetField(tray, "_tooltip", new TrayApplication.TooltipPublisher());
        BancCapture.SetField(tray, "_wmTaskbarCreated", Win32.RegisterWindowMessageW("TaskbarCreated"));
        var wndProc = (Win32.WNDPROC)Delegate.CreateDelegate(typeof(Win32.WNDPROC), tray,
            typeof(TrayApplication).GetMethod("WndProcCallback", Instance)!);
        return (tray, faux, wndProc);
    }

    internal static object? Appeler(TrayApplication tray, string methode, params object?[] args) =>
        BancCapture.Call(tray, methode, args);

    internal static bool EnPause(TrayApplication tray) =>
        BancCapture.Field<DateTimeOffset?>(tray, "_pauseUntilUtc").HasValue;

    internal static bool Actif(TrayApplication tray) => BancCapture.Field<bool>(tray, "_enabled");

    /// <summary>NIM_MODIFY reçus : chaque bulle ou mise à jour d'icône en envoie un.</summary>
    internal static int Modifs(FauxTrayShell faux) => faux.Icone.Count(m => m == NimModify);

    internal static string Pendante(TrayApplication tray) =>
        BancCapture.Field<object>(tray, "_pendingBalloon").ToString()!;

    internal static void PoserPendante(TrayApplication tray, string genre) =>
        BancCapture.SetField(tray, "_pendingBalloon",
            Enum.Parse(typeof(TrayApplication).GetNestedType("PendingBalloonKind", Instance)!, genre));

    /// <summary>Accord d'activation et activation, comme un tray démarré après l'accueil.</summary>
    internal static void ActiverAvecHook(TrayApplication tray, KeyboardHook hook)
    {
        BancCapture.SetField(tray, "_hook", hook);
        BancCapture.SetField(tray, "_activationConsent", true);
        BancCapture.SetField(tray, "_enabled", true);
    }

    internal static void Pomper(Stopwatch chrono, long jusquaMs)
    {
        while (chrono.ElapsedMilliseconds < jusquaMs)
        {
            while (BancCapture.Native.PeekMessageW(out var msg, IntPtr.Zero, 0, 0, BancCapture.Native.PM_REMOVE))
            {
                Win32.TranslateMessage(ref msg);
                Win32.DispatchMessageW(ref msg);
            }
            Thread.Sleep(10);
        }
    }

    internal static void Statiques(Action<Type, BindingFlags> action) => action(typeof(ConfigManager), Statique);

    /// <summary>Remet à zéro l'horodatage de la dernière erreur de journal : il garde la demande d'avis.</summary>
    internal static void OublierLesErreurs() =>
        typeof(ConfigManager).GetField("_lastErrorUtc", Statique)!.SetValue(null, null);

    internal static void DerniereFrappeIlYa(long ms) =>
        typeof(UsageStats).GetField("_lastRemapTickCount", Statique)!.SetValue(null, ms < 0 ? 0L : Environment.TickCount64 - ms);

    /// <summary>
    /// Statistiques d'un utilisateur à qui l'essai 1 de la demande d'avis est dû (modèle EnvAvis de
    /// MachinesEtatsLot7Tests) : dans le dossier de l'isolation, jamais dans le vrai profil.
    /// </summary>
    internal static void SemerStatistiquesDAvis()
    {
        string Jour(DateOnly d) => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var aujourdhui = DateOnly.FromDateTime(DateTime.Now);
        string stats = Path.Combine(ConfigManager.LogDirectory, "stats-avis.json");
        File.WriteAllText(stats, JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["activeDaysCount"] = 10,
            ["totalActiveMinutes"] = 10L,
            ["accentedUppercaseCount"] = 20L,
            ["firstRemapDate"] = Jour(aujourdhui.AddDays(-30)),
            ["lastActiveDate"] = Jour(aujourdhui),
        }));
        UsageStats.OverrideStatsPathForTests(stats);
        LearningSessionTracker.ResetForTests();
        OublierLesErreurs();
    }
}

/// <summary>Enveloppe de <see cref="FauxTrayShell"/> qui laisse un test agir pendant le menu contextuel.</summary>
internal sealed class TrayShellPendantMenu : ITrayShell
{
    private readonly FauxTrayShell _faux;
    public Action? PendantMenu { get; set; }

    public TrayShellPendantMenu(FauxTrayShell faux) => _faux = faux;

    public UIntPtr SetTimer(IntPtr hWnd, UIntPtr id, uint elapseMs, IntPtr timerProc) => _faux.SetTimer(hWnd, id, elapseMs, timerProc);
    public bool KillTimer(IntPtr hWnd, UIntPtr id) => _faux.KillTimer(hWnd, id);
    public bool Shell_NotifyIconW(uint message, ref Win32.NOTIFYICONDATAW data) => _faux.Shell_NotifyIconW(message, ref data);
    public bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam) => _faux.PostMessageW(hWnd, msg, wParam, lParam);
    public int MessageBoxW(IntPtr hWnd, string text, string caption, uint type) => _faux.MessageBoxW(hWnd, text, caption, type);
    public IntPtr ShellExecuteW(IntPtr hWnd, string operation, string file, string? parameters, string? directory, int showCommand)
        => _faux.ShellExecuteW(hWnd, operation, file, parameters, directory, showCommand);
    public bool TrackPopupMenuEx(IntPtr hMenu, uint flags, int x, int y, IntPtr hWnd, IntPtr parameters)
    {
        bool ok = _faux.TrackPopupMenuEx(hMenu, flags, x, y, hWnd, parameters);
        PendantMenu?.Invoke();   // le menu est ouvert : l'état peut changer avant le choix de l'utilisateur
        return ok;
    }
    public bool InSendMessage() => _faux.InSendMessage();
    public int GetMessageTime() => _faux.GetMessageTime();
    public uint GetTickCount() => _faux.GetTickCount();
}

/// <summary>Activation, pause, suspension, annonces et arrêt du tray (M3.1, M3.2, M3.8, M8.1, M8.3).</summary>
public sealed class Regles130TrayEtatTests
{
    // M3.1 I1 : IsPaused ⇒ _enabled. Par les commandes du menu (WM_COMMAND) et le raccourci : une
    // pause ne démarre pas sur un programme désactivé, et la bascule efface la pause avant d'inverser.
    [Fact]
    public void M3_1_I1_PauseImpliqueActif_SurToutesLesSequencesDeCommandes()
    {
        using var iso = new BancCapture.Isolation();
        using var banc = new BancFrappe(asyncLogique: false);
        ConfigManager.SetNotifications(false);   // ni bulle ni fenêtre de bascule : seul l'état compte
        var (tray, faux, wndProc) = OutilsTray130.Tray();
        OutilsTray130.ActiverAvecHook(tray, banc.Hook);

        void Commande(int id) => wndProc(OutilsTray130.Fenetre, Win32.WM_COMMAND, (IntPtr)id, IntPtr.Zero);
        void Invariant(string etape) =>
            Assert.True(!OutilsTray130.EnPause(tray) || OutilsTray130.Actif(tray), $"M3.1-I1 : en pause alors que désactivé ({etape})");

        Commande(OutilsTray130.IdmPause15);                       // pause de 15 min
        Assert.True(OutilsTray130.EnPause(tray));                  // témoin : la commande agit
        Assert.Contains(OutilsTray130.TimerPause, faux.Minuteries);
        Invariant("pause démarrée");

        Commande(OutilsTray130.IdmToggle);                         // bascule en pause : la pause tombe
        Assert.False(OutilsTray130.Actif(tray));
        Invariant("bascule pendant la pause");

        Commande(OutilsTray130.IdmToggle);                         // réactivé
        Commande(OutilsTray130.IdmPause15);
        Assert.True(OutilsTray130.EnPause(tray));                  // témoin : la pause repart quand actif
        BancCapture.Call(tray, "OnToggleShortcut");                // le raccourci reprend, sans désactiver
        Assert.True(OutilsTray130.Actif(tray));
        Invariant("raccourci pendant la pause");

        Commande(OutilsTray130.IdmToggle);                         // désactivé : plus aucune pause possible
        Assert.False(OutilsTray130.Actif(tray));
        Commande(OutilsTray130.IdmPause15);
        Invariant("pause demandée sur un programme désactivé");
        Assert.False(OutilsTray130.EnPause(tray), "M3.1-I1 : une pause a démarré sur un programme désactivé");
    }

    // M3.1 I3 : ApplyHookState est le seul écrivain de PassThroughAll, ShortcutsWhilePassThrough,
    // Enabled (du hook) et EmissionPaused. Balayage du texte source : règle structurelle.
    [Fact]
    public void M3_1_I3_ApplyHookState_SeulEcrivainDeLEtatDuHook()
    {
        var ecriture = new Regex(@"\.(PassThroughAll|ShortcutsWhilePassThrough|EmissionPaused)\s*=(?!=)|(?<![A-Za-z])_?hook[?!]?\.Enabled\s*=(?!=)");
        string code = SourcePur.Code("TrayApplication.cs");
        int debut = code.IndexOf("private void ApplyHookState(", StringComparison.Ordinal);
        int fin = debut < 0 ? -1 : code.IndexOf("private void ApplyWindowInputState(", debut, StringComparison.Ordinal);
        Assert.True(debut >= 0 && fin > debut, "ApplyHookState introuvable : le balayage doit être revu");
        Assert.Equal(4, ecriture.Matches(code[debut..fin]).Count);   // témoin : les quatre écritures y sont

        var ailleurs = Directory.GetFiles(SourcePur.DossierSrc(), "*.cs")
            .Where(f => Path.GetFileName(f) != "TrayApplication.cs")
            .Where(f => ecriture.IsMatch(SourcePur.Code(Path.GetFileName(f))))
            .Select(Path.GetFileName).ToList();
        Assert.Empty(ailleurs);                                      // aucun autre fichier de l'app n'écrit l'état du hook

        Assert.Equal(0, ecriture.Matches(code[..debut] + code[fin..]).Count);
    }

    // M3.1 I4 : le menu et la commande exécutée correspondent au même état. Tenu depuis 1.3.0 (C3.2) :
    // le menu retient l'état qu'il affiche ; « Désactiver » choisi alors que le raccourci global a
    // désactivé pendant le menu ne réactive pas. Ajusté : témoin couplé à l'ancienne implémentation
    // (depuis M3.1 I5, le raccourci poste WM_APP_TOGGLE ; la boucle de messages, simulée ici, l'exécute).
    [Fact]
    public void M3_1_I4_CommandeDuMenu_AppliqueeALEtatAffiche()
    {
        using var iso = new BancCapture.Isolation();
        using var banc = new BancFrappe(asyncLogique: false);
        ConfigManager.SetNotifications(false);
        TrayShellPendantMenu? menu = null;
        var (tray, faux, wndProc) = OutilsTray130.Tray(f => menu = new TrayShellPendantMenu(f));
        OutilsTray130.ActiverAvecHook(tray, banc.Hook);
        menu!.PendantMenu = () =>                                  // l'état change pendant le menu
        {
            BancCapture.Call(tray, "OnToggleShortcut");            // le raccourci, dans le rappel du hook
            wndProc(OutilsTray130.Fenetre, OutilsTray130.WmAppToggle, IntPtr.Zero, IntPtr.Zero);   // la boucle le traite
        };

        BancCapture.Call(tray, "ShowContextMenu");                // le menu s'ouvre sur « Désactiver » (état actif)
        Assert.Equal(1, faux.Menus);                               // témoin : le menu a bien été montré
        Assert.False(OutilsTray130.Actif(tray));                   // témoin : l'état a changé pendant le menu

        wndProc(OutilsTray130.Fenetre, Win32.WM_COMMAND, (IntPtr)OutilsTray130.IdmToggle, IntPtr.Zero);   // clic sur « Désactiver »
        Assert.False(OutilsTray130.Actif(tray), "C3.2 : « Désactiver » choisi dans un menu affiché actif a réactivé le programme");
    }

    // M3.1 I5 : le raccourci global ne fait pas de travail lourd dans le rappel du hook. Tenu depuis
    // 1.3.0 (C3.4) : OnToggleShortcut, câblé sur KeyMapper.ToggleRequested, ne fait que poster
    // WM_APP_TOGGLE ; la bascule (Shell_NotifyIcon, GDI+) tourne dans la boucle de messages. Le test
    // exige zéro appel shell dans le rappel, puis une bascule, une seule, quand le message est traité.
    [Fact]
    public void M3_1_I5_RaccourciGlobal_NeFaitPasDeTravailLourdDansLeRappelDuHook()
    {
        using var iso = new BancCapture.Isolation();
        using var banc = new BancFrappe(asyncLogique: false);
        ConfigManager.SetNotifications(false);   // pas de fenêtre de bascule : on observe le shell seul
        var (tray, faux, wndProc) = OutilsTray130.Tray();
        OutilsTray130.ActiverAvecHook(tray, banc.Hook);
        // Le câblage du constructeur (TrayApplication.cs : _mapper.ToggleRequested += OnToggleShortcut).
        banc.Mapper.ToggleRequested += (Action)Delegate.CreateDelegate(typeof(Action), tray,
            typeof(TrayApplication).GetMethod("OnToggleShortcut", BindingFlags.Instance | BindingFlags.NonPublic)!);
        int rappels = 0;
        banc.Mapper.ToggleRequested += () => rappels++;

        banc.Frapper("CtrlG", bas: true);
        banc.Frapper("MajG", bas: true);
        banc.Frapper("VerrMaj", bas: true);                        // Ctrl+Maj+Verr. Maj : le rappel du hook
        banc.Frapper("VerrMaj", bas: false);
        banc.Frapper("MajG", bas: false);
        banc.Frapper("CtrlG", bas: false);
        Assert.Equal(1, rappels);                                  // témoin : le raccourci est arrivé jusqu'au tray
        Assert.True(faux.Icone.Count == 0,
            $"C3.4 : le rappel du hook a appelé Shell_NotifyIcon {faux.Icone.Count} fois (travail lourd dans le hook)");

        // Preuve que le travail n'est pas perdu : un seul message posté, et son traitement bascule une fois.
        Assert.Equal(1, faux.Postes.Count(m => m == OutilsTray130.WmAppToggle));
        Assert.True(OutilsTray130.Actif(tray));                    // rien n'a basculé avant la boucle
        wndProc(OutilsTray130.Fenetre, OutilsTray130.WmAppToggle, IntPtr.Zero, IntPtr.Zero);
        Assert.False(OutilsTray130.Actif(tray));                   // désactivé, une fois
        Assert.True(faux.Icone.Count > 0);                         // l'icône est mise à jour, hors du rappel
    }

    private static object EtatAnnonce(TrayApplication tray) => BancCapture.Field<object>(tray, "_announcementState");

    private static void ArmerLaReprise(TrayApplication tray)
    {
        object etat = EtatAnnonce(tray);                           // structure boxée : on la modifie puis on la repose
        etat.GetType().GetField("ResumeOwed")!.SetValue(etat, true);
        BancCapture.SetField(tray, "_announcementState", etat);
    }

    private static void Suspendre(TrayApplication tray)
    {
        BancCapture.SetField(tray, "_activationConsent", true);
        BancCapture.SetField(tray, "_suspendedForCompatibility", true);
        BancCapture.SetField(tray, "_appliedSuspendReason", CompatibilitySuspendReason.AntiCheat);
        BancCapture.SetField(tray, "_wasEnabledBeforeAutoDisable", true);   // le programme était actif : la suspension arrête quelque chose, donc une bulle
        BancCapture.Call(tray, "AnnounceCompatibilityStateIfChanged", "jeu.exe", "jeu");
    }

    private static void SortirDeLaSuspension(TrayApplication tray)
    {
        BancCapture.SetField(tray, "_suspendedForCompatibility", false);
        BancCapture.SetField(tray, "_appliedSuspendReason", CompatibilitySuspendReason.None);
        BancCapture.Call(tray, "AnnounceCompatibilityStateIfChanged", "", "");
    }

    // M3.2 I1 : une annonce de reprise n'est due que si une suspension l'a rendue due (ResumeOwed, armé
    // par la branche Leave). Par l'annonce réelle du tray : une suspension annoncée, puis la sortie sans
    // reprise due, ne dit rien de plus ; avec la reprise due, la bulle « à nouveau actif » part.
    [Fact]
    public void M3_2_I1_RepriseAnnonceeSeulementSiUneSuspensionLaRenduDue()
    {
        using var iso = new BancCapture.Isolation();

        // Témoin : reprise due (la branche Leave l'arme) ⇒ deux bulles, suspension puis reprise.
        var (trayDu, fauxDu, _) = OutilsTray130.Tray();
        Suspendre(trayDu);
        Assert.Equal(1, OutilsTray130.Modifs(fauxDu));
        ArmerLaReprise(trayDu);
        SortirDeLaSuspension(trayDu);
        Assert.Equal(2, OutilsTray130.Modifs(fauxDu));

        // La règle : sortie de suspension sans reprise due ⇒ aucune bulle de plus.
        var (tray, faux, _) = OutilsTray130.Tray();
        Suspendre(tray);
        Assert.Equal(1, OutilsTray130.Modifs(faux));
        SortirDeLaSuspension(tray);
        Assert.Equal(1, OutilsTray130.Modifs(faux));
    }

    // M3.8 I1 : Cleanup est idempotent. Le second appel ne retire pas l'icône une seconde fois, ne tue
    // aucune minuterie, n'écrit rien (la config modifiée entre-temps reste en attente).
    [Fact]
    public void M3_8_I1_Cleanup_EstIdempotent()
    {
        using var iso = new BancCapture.Isolation();
        using var banc = new BancFrappe(asyncLogique: false);
        var (tray, faux, _) = OutilsTray130.Tray();
        OutilsTray130.ActiverAvecHook(tray, banc.Hook);

        BancCapture.Call(tray, "Cleanup");
        Assert.Null(BancCapture.Field<KeyboardHook?>(tray, "_hook"));            // témoin : le hook est décroché
        Assert.Equal(1, faux.Icone.Count(m => m == OutilsTray130.NimDelete));    // témoin : l'icône est retirée

        faux.Minuteries.Add(OutilsTray130.TimerPause);   // une minuterie posée après le nettoyage
        ConfigManager.SetChallengeAnnounceDone();         // un réglage en attente d'écriture
        int ecritures = ConfigManager.DiskWriteCount;

        BancCapture.Call(tray, "Cleanup");
        Assert.Contains(OutilsTray130.TimerPause, faux.Minuteries);
        Assert.Equal(ecritures, ConfigManager.DiskWriteCount);
        Assert.Equal(1, faux.Icone.Count(m => m == OutilsTray130.NimDelete));
    }

    // M8.1 I4 : Dispose unique. Cleanup deux fois avec des fenêtres réelles : aucune exception, champs
    // nuls, fenêtres détruites, icône retirée une seule fois. Le garde _cleaned et la remise à nul des
    // champs protègent chacun du double Dispose : le double Dispose d'une fenêtre ne s'observe pas
    // (il est sans effet), seul le second NIM_DELETE trahit un second nettoyage.
    [Fact]
    public void M8_1_I4_Cleanup_DeuxFoisAvecDesFenetresReelles_DisposeUnique()
    {
        using var iso = new BancCapture.Isolation();
        using var banc = new BancFrappe(asyncLogique: false);
        var (tray, faux, _) = OutilsTray130.Tray();
        OutilsTray130.ActiverAvecHook(tray, banc.Hook);
        var apropos = new AboutWindow();
        var parametres = new SettingsWindow();
        var bascule = new ToggleNotification();
        BancCapture.SetField(tray, "_about", apropos);
        BancCapture.SetField(tray, "_settings", parametres);
        BancCapture.SetField(tray, "_toggleNotification", bascule);
        IntPtr[] poignees = { BancCapture.Handle(apropos), BancCapture.Handle(parametres), BancCapture.Handle(bascule) };
        Assert.All(poignees, h => Assert.True(Win32.IsWindow(h)));               // témoin : trois fenêtres vivantes

        BancCapture.Call(tray, "Cleanup");
        Assert.All(new[] { "_about", "_settings", "_toggleNotification", "_hook" },
            champ => Assert.Null(BancCapture.Field<object?>(tray, champ)));
        BancCapture.Pump(5);
        Assert.All(poignees, h => Assert.False(Win32.IsWindow(h)));              // témoin : détruites au premier appel

        var exception = Record.Exception(() => BancCapture.Call(tray, "Cleanup"));
        Assert.Null(exception);
        Assert.Equal(1, faux.Icone.Count(m => m == OutilsTray130.NimDelete));
    }

    // M8.3 I2 : après Dispose, plus aucun appel ne vise la fenêtre : Cleanup décroche le hook avant de
    // détruire une fenêtre. Balayage de l'ordre des lignes de Cleanup (règle d'ordre, structurelle).
    [Fact]
    public void M8_3_I2_Cleanup_DecrocheLeHookAvantLesFenetres()
    {
        string code = SourcePur.Code("TrayApplication.cs");
        int debut = code.IndexOf("private void Cleanup()", StringComparison.Ordinal);
        Assert.True(debut >= 0, "Cleanup introuvable : le balayage doit être revu");
        int fin = code.IndexOf("private static void PrepareTrayIconForAdd", debut, StringComparison.Ordinal);
        Assert.True(fin > debut);
        string corps = code[debut..fin];

        int hook = corps.IndexOf("_hook?.Dispose()", StringComparison.Ordinal);
        Assert.True(hook >= 0, "le hook n'est plus disposé par Cleanup");
        string[] fenetres = { "_virtualKeyboard", "_characterSearch", "_onboarding", "_settings", "_maintainableLayersWindow",
            "_layerIndicator", "_about", "_usageStats", "_toggleNotification", "_lessons", "_layoutConflictWindow" };
        foreach (string fenetre in fenetres)
            Assert.True(corps.Contains(fenetre + "?.Dispose()"), $"{fenetre} n'est plus disposée par Cleanup");   // témoins
        var avant = fenetres.Where(f => corps.IndexOf(f + "?.Dispose()", StringComparison.Ordinal) < hook).ToList();
        Assert.Empty(avant);
    }
}

/// <summary>Bulles et clics de bulle (M3.3, M7.6).</summary>
public sealed class Regles130TrayBullesTests
{
    private static string NouvelleConfig(string nom, bool notifications)
    {
        string chemin = Path.Combine(ConfigManager.LogDirectory, nom);
        File.WriteAllText(chemin, JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["appLanguage"] = "fr",
            ["notificationsEnabled"] = notifications,
        }));
        ConfigManager.OverrideConfigPathForTests(chemin);
        return chemin;
    }

    // M3.3 I1 : bulles et nudges respectent NotificationsEnabled. Témoin avec notifications actives (la
    // bulle part, l'annonce du Défi est marquée faite), puis notifications coupées : aucune bulle, aucune
    // annonce marquée, aucun rappel marqué. Le rappel du Défi n'est discriminant qu'après 17 h (garde
    // horaire de TrainingReminders, horloge non injectable : couture manquante).
    [Fact]
    public void M3_3_I1_BullesEtNudges_RespectentNotificationsEnabled()
    {
        using var iso = new BancCapture.Isolation();

        NouvelleConfig("avec-notifications.json", notifications: true);
        var (actif, fauxActif, _) = OutilsTray130.Tray();
        BancCapture.Call(actif, "ShowBalloon", "titre", "texte");
        Assert.Equal(1, OutilsTray130.Modifs(fauxActif));                                             // témoin : la bulle part
        Assert.True((bool)BancCapture.Call(actif, "MaybeShowChallengeAnnouncement")!);              // témoin : l'annonce part
        Assert.True(ConfigManager.ChallengeAnnounceDone);

        NouvelleConfig("sans-notifications.json", notifications: false);
        ConfigManager.SetTrainingEnabled(true);                                                       // le rappel du Défi serait dû
        var (tray, faux, _) = OutilsTray130.Tray();
        BancCapture.Call(tray, "ShowBalloon", "titre", "texte");
        Assert.Equal(0, OutilsTray130.Modifs(faux));
        Assert.False((bool)BancCapture.Call(tray, "MaybeShowChallengeAnnouncement")!);
        Assert.False(ConfigManager.ChallengeAnnounceDone);
        BancCapture.Call(tray, "MaybeShowTrainingReminder");
        Assert.True(string.IsNullOrEmpty(ConfigManager.TrainingLastReminderDate));
        Assert.Equal(0, OutilsTray130.Modifs(faux));
    }

    // M3.3 I2 : une sollicitation à usage unique n'est marquée « faite » qu'une fois réellement montrée.
    // Non garanti (C3.5) : l'annonce du Défi est marquée avant l'affichage, et le retour de
    // Shell_NotifyIconW est ignoré. ROUGE attendu aujourd'hui. Formulation ambiguë : les commentaires du
    // produit revendiquent « jamais deux fois, même si la balloon échoue ensuite » ; à trancher.
    [Fact]
    public void M3_3_I2_SollicitationAUsageUnique_MarqueeSeulementSiMontree()
    {
        using var iso = new BancCapture.Isolation();
        var (tray, faux, _) = OutilsTray130.Tray();
        faux.ResultatIcone = m => m != OutilsTray130.NimModify;   // le shell refuse la bulle
        Assert.False(ConfigManager.ChallengeAnnounceDone);

        bool partie = (bool)BancCapture.Call(tray, "MaybeShowChallengeAnnouncement")!;
        Assert.True(partie);                                      // témoin : l'annonce a été tentée
        Assert.Equal(1, faux.Refus);                               // témoin : le shell a refusé la bulle
        Assert.False(ConfigManager.ChallengeAnnounceDone,
            "C3.5 : l'annonce unique est marquée faite alors que la bulle a été refusée par le shell");
    }

    // M7.6 I1 : toute nouvelle balloon remplace la précédente et invalide la cible, par ShowBalloon et
    // par ShowSecurityBalloon.
    [Fact]
    public void M7_6_I1_NouvelleBalloon_RemplaceLaPrecedenteEtInvalideLaCible()
    {
        using var iso = new BancCapture.Isolation();
        var (tray, faux, _) = OutilsTray130.Tray();

        OutilsTray130.PoserPendante(tray, "Review");
        Assert.Equal("Review", OutilsTray130.Pendante(tray));          // témoin : la balloon d'avis est en attente
        BancCapture.Call(tray, "ShowBalloon", "titre", "texte");
        Assert.Equal(1, OutilsTray130.Modifs(faux));                    // témoin : la nouvelle bulle est partie
        Assert.Equal("None", OutilsTray130.Pendante(tray));

        OutilsTray130.PoserPendante(tray, "Review");
        BancCapture.Call(tray, "ShowSecurityBalloon", "titre", "texte");
        Assert.Equal("None", OutilsTray130.Pendante(tray));
    }

    private static void ClicDeBulle(Win32.WNDPROC wndProc, uint notification) =>
        wndProc(OutilsTray130.Fenetre, OutilsTray130.WmTrayIcon, IntPtr.Zero, (IntPtr)notification);

    // M7.6 I2 : un clic de balloon ne s'applique qu'une fois : le second clic ne rouvre rien. Par le
    // vrai WndProc ; l'ouverture passe par la couture ShellExecuteW (OpenFeedback), comptée par le faux.
    [Fact]
    public void M7_6_I2_ClicDeBalloon_NeSAppliqueQuUneFois()
    {
        using var iso = new BancCapture.Isolation();
        var (tray, faux, wndProc) = OutilsTray130.Tray();
        BancCapture.SetField(tray, "_reviewTargetIsStore", false);     // page feedback : ShellExecuteW, sans boîte du Store
        OutilsTray130.PoserPendante(tray, "Review");

        ClicDeBulle(wndProc, OutilsTray130.NinBalloonUserClick);
        Assert.Single(faux.Liens);                                      // témoin : le premier clic ouvre la cible
        Assert.Equal("None", OutilsTray130.Pendante(tray));

        ClicDeBulle(wndProc, OutilsTray130.NinBalloonUserClick);
        Assert.Single(faux.Liens);
    }

    // M7.6 I3 : une balloon expirée n'est plus cliquable : NIN_BALLOONTIMEOUT remet le type à None,
    // sans action shell, et un clic tardif ne rouvre rien.
    [Fact]
    public void M7_6_I3_BalloonExpiree_NEstPlusCliquable()
    {
        using var iso = new BancCapture.Isolation();
        var (tray, faux, wndProc) = OutilsTray130.Tray();
        BancCapture.SetField(tray, "_reviewTargetIsStore", false);
        OutilsTray130.PoserPendante(tray, "Review");
        Assert.Equal("Review", OutilsTray130.Pendante(tray));           // témoin

        ClicDeBulle(wndProc, OutilsTray130.NinBalloonTimeout);
        Assert.Equal("None", OutilsTray130.Pendante(tray));
        Assert.Empty(faux.Liens);                                       // l'expiration n'ouvre rien

        ClicDeBulle(wndProc, OutilsTray130.NinBalloonUserClick);        // clic tardif sur la bulle disparue
        Assert.Empty(faux.Liens);
    }
}

/// <summary>Demande d'avis et accueil rouvert (M7.2, M7.4, M7.5).</summary>
public sealed class Regles130TrayAvisEtAccueilTests
{
    // M7.2 I1 : jamais deux affichages concurrents de l'accueil ; un second appel ramène devant sans
    // remettre à l'étape 1. Les trois voies : ShowOnboardingNow, double clic sur l'icône sans accord,
    // entrée « Revoir l'accueil » du menu.
    [Fact]
    public void M7_2_I1_AccueilDejaOuvert_RamenePremierPlanSansRetourALEtape1()
    {
        using var iso = new BancCapture.Isolation();
        var setterAvant = OnboardingWindow.AutoStartSetter;
        var registreAvant = OnboardingWindow.AutoStartIsRegistered;
        var boiteAvant = OnboardingWindow.ShowMessageBox;
        OnboardingWindow.AutoStartSetter = _ => true;
        OnboardingWindow.AutoStartIsRegistered = () => false;
        OnboardingWindow.ShowMessageBox = (_, _, _, _) => 1;
        OnboardingWindow? accueil = null;
        try
        {
            var (tray, _, wndProc) = OutilsTray130.Tray();
            accueil = new OnboardingWindow();
            accueil.Show();
            BancCapture.Call(accueil, "GoToNextStep");                  // étape 2 (index 1)
            BancCapture.SetField(tray, "_onboarding", accueil);
            BancCapture.SetField(tray, "_activationConsent", false);     // sans accord : le double clic ouvre l'accueil
            Assert.True(accueil.IsVisible);
            Assert.Equal(1, BancCapture.Field<int>(accueil, "_currentStep"));   // témoin

            BancCapture.Call(tray, "ShowOnboardingNow");
            Assert.Equal(1, BancCapture.Field<int>(accueil, "_currentStep"));

            wndProc(OutilsTray130.Fenetre, OutilsTray130.WmTrayIcon, IntPtr.Zero, (IntPtr)Win32.WM_LBUTTONDBLCLK);
            Assert.Equal(1, BancCapture.Field<int>(accueil, "_currentStep"));

            wndProc(OutilsTray130.Fenetre, Win32.WM_COMMAND, (IntPtr)OutilsTray130.IdmOnboarding, IntPtr.Zero);
            Assert.True(accueil.IsVisible);
            Assert.Equal(1, BancCapture.Field<int>(accueil, "_currentStep"));
        }
        finally
        {
            OnboardingWindow.AutoStartSetter = setterAvant;
            OnboardingWindow.AutoStartIsRegistered = registreAvant;
            OnboardingWindow.ShowMessageBox = boiteAvant;
            if (accueil != null) BancCapture.Teardown(accueil);
        }
    }

    // M7.4 I7 : décision et enregistrement atomiques : une première demande est enregistrée (compteur à
    // 1) avant l'affichage ; la seconde, tout de suite après, est refusée et le compteur reste à 1.
    [Fact]
    public void M7_4_I7_DeuxDemandesSuccessives_LaSecondeEstRefusee_CompteurA1()
    {
        using var iso = new BancCapture.Isolation();
        using var canal = AppChannel.OverrideForTests(DistributionChannel.Unpackaged);
        OutilsTray130.SemerStatistiquesDAvis();
        OutilsTray130.DerniereFrappeIlYa(20_000);
        try
        {
            var (tray, faux, _) = OutilsTray130.Tray();
            BancCapture.SetField(tray, "_versionFirstRun",
                new ConfigManager.VersionFirstRun("1.3.0", DateOnly.FromDateTime(DateTime.Now), false));

            Assert.True((bool)BancCapture.Call(tray, "MaybeShowReviewPrompt", ReviewPromptTrigger.QuietTyping)!);
            Assert.Equal(1, ConfigManager.ReviewPromptCount);           // témoin : le premier essai est compté
            Assert.Equal(1, OutilsTray130.Modifs(faux));                // témoin : la bulle est partie

            bool seconde = (bool)BancCapture.Call(tray, "MaybeShowReviewPrompt", ReviewPromptTrigger.QuietTyping)!;
            Assert.Equal(1, OutilsTray130.Modifs(faux));
            Assert.False(seconde, "M7.4-I7 : la seconde demande, sans plancher de 7 jours, a été acceptée");
            Assert.Equal(1, ConfigManager.ReviewPromptCount);
        }
        finally
        {
            UsageStats.ClearEnrichedThresholdSignal();
            OutilsTray130.DerniereFrappeIlYa(-1);
            LearningSessionTracker.ResetForTests();
        }
    }

    // M7.5 I6 : aucune E/S disque dans le tick de quiétude. Le fichier de configuration est supprimé
    // pendant qu'un réglage attend son écriture : le tick (refusé) ne l'écrit pas. Témoin : la même
    // situation, un Flush explicite recrée bien le fichier.
    [Fact]
    public void M7_5_I6_TickDeQuietude_NeFaitAucuneEcritureSurDisque()
    {
        using var iso = new BancCapture.Isolation();
        using var canal = AppChannel.OverrideForTests(DistributionChannel.Unpackaged);
        OutilsTray130.SemerStatistiquesDAvis();
        string chemin = Path.Combine(ConfigManager.LogDirectory, "config.json");
        var (tray, _, wndProc) = OutilsTray130.Tray();
        BancCapture.SetField(tray, "_versionFirstRun",
            new ConfigManager.VersionFirstRun("1.3.0", DateOnly.FromDateTime(DateTime.Now), false));

        ConfigManager.SetChallengeAnnounceDone();                       // un réglage attend son écriture
        File.Delete(chemin);
        ConfigManager.Flush();
        Assert.True(File.Exists(chemin));                                // témoin : un Flush recrée le fichier supprimé

        ConfigManager.SetAutoStartNudgeDone();                          // de nouveau un réglage en attente
        File.Delete(chemin);
        int ecritures = ConfigManager.DiskWriteCount;
        wndProc(OutilsTray130.Fenetre, Win32.WM_TIMER, (IntPtr)OutilsTray130.TimerReviewQuiet, IntPtr.Zero);   // le tick, refusé (aucune frappe)
        Assert.Equal(ecritures, ConfigManager.DiskWriteCount);
        Assert.False(File.Exists(chemin), "M7.5-I6 : le tick de quiétude a écrit config.json");
    }
}

/// <summary>Fenêtres réelles en process : dialogue de pause (M3.4) et fenêtre de bascule (M3.6).</summary>
public sealed class Regles130TrayFenetresTests
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void MinuterieProc(IntPtr hwnd, uint msg, UIntPtr id, uint temps);

    // M3.4 I3 : quitter pendant le dialogue ne bloque pas : la boucle modale sort sur WM_QUIT, le
    // reposte (le processus ne survit pas sans icône) et réactive la fenêtre propriétaire. Une minuterie
    // de fil lève WM_QUIT depuis l'intérieur de la boucle, ce qui prouve aussi qu'elle tournait.
    [Fact]
    public void M3_4_I3_QuitterPendantLeDialogue_NeBloquePas_WmQuitRepose()
    {
        using var iso = new BancCapture.Isolation();
        IntPtr proprietaire = Win32.CreateWindowExW(0, "STATIC", "", Win32.WS_POPUP, 0, 0, 10, 10,
            IntPtr.Zero, IntPtr.Zero, Win32.GetModuleHandleW(null), IntPtr.Zero);
        Assert.NotEqual(IntPtr.Zero, proprietaire);
        bool proprietaireGeleDansLaBoucle = false;
        MinuterieProc rappel = (_, _, _, _) =>
        {
            proprietaireGeleDansLaBoucle = !Win32.IsWindowEnabled(proprietaire);
            Win32.PostQuitMessage(7);                                  // l'utilisateur quitte l'application
        };
        UIntPtr minuterie = Win32.SetTimer(IntPtr.Zero, UIntPtr.Zero, 30, Marshal.GetFunctionPointerForDelegate(rappel));
        try
        {
            TimeSpan? duree = PauseDurationDialog.Show(proprietaire);

            Assert.True(proprietaireGeleDansLaBoucle, "la boucle modale n'a pas tourné (propriétaire jamais désactivé)");   // témoin
            Assert.Null(duree);
            Assert.True(Win32.IsWindowEnabled(proprietaire));           // propriétaire réactivé dans le finally
            bool reposte = BancCapture.Native.PeekMessageW(out var msg, IntPtr.Zero, 0x0012, 0x0012, BancCapture.Native.PM_REMOVE);
            Assert.True(reposte, "M3.4-I3 : WM_QUIT n'a pas été reposté par la boucle du dialogue");
            Assert.Equal((IntPtr)7, msg.wParam);
        }
        finally
        {
            Win32.KillTimer(IntPtr.Zero, minuterie);
            BancCapture.Native.PeekMessageW(out _, IntPtr.Zero, 0x0012, 0x0012, BancCapture.Native.PM_REMOVE);   // ne laisse aucun WM_QUIT
            Win32.DestroyWindow(proprietaire);
            GC.KeepAlive(rappel);
        }
    }

    private static int AbonnesLangue()
    {
        var champ = typeof(ConfigManager).GetField("AppLanguageChanged", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
        return (champ.GetValue(null) as Delegate)?.GetInvocationList().Length ?? 0;
    }

    // M3.4 I4 : l'abonnement AppLanguageChanged du dialogue est retiré à la fermeture (Dispose).
    [Fact]
    public void M3_4_I4_AbonnementAppLanguageChanged_RetireALaFermeture()
    {
        using var iso = new BancCapture.Isolation();
        int avant = AbonnesLangue();
        var dialogue = new PauseDurationDialog();
        try
        {
            typeof(PauseDurationDialog).GetMethod("CreateWindow", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(dialogue, new object[] { IntPtr.Zero });
            Assert.Equal(avant + 1, AbonnesLangue());                   // témoin : la création abonne le dialogue
        }
        finally { BancCapture.Teardown(dialogue); }
        Assert.Equal(avant, AbonnesLangue());
    }

    // M3.6 I2 : la fenêtre de bascule se ferme 2 s après la dernière bascule, la minuterie étant
    // réarmée par chaque Show. Fenêtre réelle, pompe de messages à échéance ; durée d'environ 6,5 s.
    // Couture manquante : ToggleNotification appelle Win32 (SetTimer, KillTimer) directement.
    [Fact]
    public void M3_6_I2_FenetreDeBascule_SeFerme2sApresLaDerniereBascule()
    {
        using var iso = new BancCapture.Isolation();
        var bascule = new ToggleNotification();
        try
        {
            IntPtr fenetre = BancCapture.Handle(bascule);
            Assert.NotEqual(IntPtr.Zero, fenetre);
            var chrono = Stopwatch.StartNew();

            bascule.Show(true);
            Assert.True(Win32.IsWindowVisible(fenetre));                // témoin : la fenêtre s'affiche
            OutilsTray130.Pomper(chrono, 1500);
            Assert.True(Win32.IsWindowVisible(fenetre));                // témoin : pas fermée trop tôt
            OutilsTray130.Pomper(chrono, 2400);
            Assert.False(Win32.IsWindowVisible(fenetre));               // fermée 2 s après la bascule

            chrono.Restart();
            bascule.Show(false);
            OutilsTray130.Pomper(chrono, 1500);
            bascule.Show(true);                                         // seconde bascule : la minuterie repart de zéro
            chrono.Restart();
            OutilsTray130.Pomper(chrono, 1500);
            Assert.True(Win32.IsWindowVisible(fenetre), "M3.6-I2 : fermée 1,5 s après la dernière bascule (minuterie non réarmée)");
            OutilsTray130.Pomper(chrono, 2400);
            Assert.False(Win32.IsWindowVisible(fenetre));
        }
        finally { BancCapture.Teardown(bascule); }
    }
}
