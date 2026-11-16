using System.Reflection;
using System.Runtime.CompilerServices;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Couture d'état de <c>TrayApplication</c> (décision d'Antoine du 16 novembre 2026,
/// docs/audit-frappe-hasard.md § 7). Le vrai <c>WndProcCallback</c> et les vraies méthodes
/// tournent sur un tray non initialisé ; <see cref="FauxTrayShell"/> remplace Windows et
/// garde ce que l'application lui demande. Un test par invariant débloqué :
/// M3.1 I2 (minuterie de pause), M3.6 I1 / C3.6 (icône après TaskbarCreated),
/// M3.8 I3 (veille et session).
/// </summary>
public sealed class CoutureTrayTests
{
    private const uint TimerPause = 9030;          // TrayApplication.TIMER_PAUSE
    private const uint TimerAjoutIcone = 9070;     // TrayApplication.TIMER_TRAY_ADD_RETRY
    private const uint NimAdd = 0;
    private const int WtsSessionLock = 0x7;
    private const int PbtApmSuspend = 0x4;

    private static (TrayApplication Tray, FauxTrayShell Faux, Win32.WNDPROC WndProc) Tray()
    {
        var tray = (TrayApplication)RuntimeHelpers.GetUninitializedObject(typeof(TrayApplication));
        var faux = new FauxTrayShell();
        tray.Shell = faux;
        BancCapture.SetField(tray, "_hWnd", (IntPtr)0x1234);
        BancCapture.SetField(tray, "_tooltip", new TrayApplication.TooltipPublisher());
        BancCapture.SetField(tray, "_wmTaskbarCreated", Win32.RegisterWindowMessageW("TaskbarCreated"));
        var wndProc = (Win32.WNDPROC)Delegate.CreateDelegate(typeof(Win32.WNDPROC), tray,
            typeof(TrayApplication).GetMethod("WndProcCallback", BindingFlags.Instance | BindingFlags.NonPublic)!);
        return (tray, faux, wndProc);
    }

    private static void Appeler(TrayApplication tray, string methode, params object[] args) =>
        typeof(TrayApplication).GetMethod(methode, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(tray, args);

    private static bool EnPause(TrayApplication tray) =>
        ((DateTimeOffset?)typeof(TrayApplication).GetField("_pauseUntilUtc", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(tray)).HasValue;

    // M3.1 I2 : IsPaused ⇒ minuterie 9030 active, et elle disparaît avec la pause.
    [Fact]
    public void M3_1_I2_LaPauseVaAvecSaMinuterie()
    {
        using var iso = new BancCapture.Isolation();
        var (tray, faux, wndProc) = Tray();
        BancCapture.SetField(tray, "_activationConsent", true);
        BancCapture.SetField(tray, "_enabled", true);

        Appeler(tray, "StartPauseUntil", DateTimeOffset.UtcNow.AddMinutes(5), "pause");
        Assert.True(EnPause(tray));
        Assert.Contains(TimerPause, faux.Minuteries);

        wndProc((IntPtr)0x1234, Win32.WM_TIMER, (IntPtr)TimerPause, IntPtr.Zero);   // échéance pas atteinte
        Assert.True(EnPause(tray));
        Assert.Contains(TimerPause, faux.Minuteries);

        BancCapture.SetField(tray, "_pauseUntilUtc", (DateTimeOffset?)DateTimeOffset.UtcNow.AddSeconds(-1));
        wndProc((IntPtr)0x1234, Win32.WM_TIMER, (IntPtr)TimerPause, IntPtr.Zero);   // échéance passée
        Assert.False(EnPause(tray));
        Assert.DoesNotContain(TimerPause, faux.Minuteries);

        Appeler(tray, "StartPauseUntil", DateTimeOffset.UtcNow.AddMinutes(5), "pause");
        Appeler(tray, "StopPause", false);                                            // reprise demandée
        Assert.False(EnPause(tray));
        Assert.DoesNotContain(TimerPause, faux.Minuteries);
    }

    // M3.6 I1 : l'icône existe tant que l'application tourne. Témoin : un NIM_ADD accepté après
    // TaskbarCreated remet l'icône.
    [Fact]
    public void M3_6_I1_TaskbarCreated_RemetLIcone()
    {
        using var iso = new BancCapture.Isolation();
        var (tray, faux, wndProc) = Tray();
        wndProc((IntPtr)0x1234, Win32.RegisterWindowMessageW("TaskbarCreated"), IntPtr.Zero, IntPtr.Zero);
        Assert.Equal(1, faux.AjoutsDepuis(0));
        Assert.True(faux.IconePresente);
    }

    // C3.6 confirmé par la couture, puis corrigé (décision d'Antoine du 16 novembre 2026) :
    // Explorer qui redémarre refuse le premier NIM_ADD (il n'a pas fini de créer la zone de
    // notification) ; sans réessai, l'icône restait absente jusqu'au redémarrage suivant.
    [Fact]
    public void C3_6_NimAddRefuse_EstReessaye()
    {
        using var iso = new BancCapture.Isolation();
        var (tray, faux, wndProc) = Tray();
        int ajouts = 0;
        faux.ResultatIcone = m => m != NimAdd || ++ajouts > 1;   // seul le premier NIM_ADD est refusé
        wndProc((IntPtr)0x1234, Win32.RegisterWindowMessageW("TaskbarCreated"), IntPtr.Zero, IntPtr.Zero);
        Assert.Equal(1, faux.Refus);   // témoin : le premier NIM_ADD a bien été refusé

        // Seul le résultat compte : réessai immédiat ou sur minuterie. Un correctif qui passerait
        // par une minuterie déjà périodique (chien de garde) doit l'armer dans ce test : un tray
        // non initialisé n'en pose aucune.
        foreach (var id in faux.Minuteries.ToArray())
            wndProc((IntPtr)0x1234, Win32.WM_TIMER, (IntPtr)id, IntPtr.Zero);
        Assert.True(faux.IconePresente, "C3.6 : NIM_ADD refusé après TaskbarCreated, aucun réessai");
        Assert.Empty(faux.Minuteries);   // l'icône posée, plus de réessai en attente
    }

    // C3.6, borne : un shell qui refuse toujours n'entraîne pas de réessai sans fin.
    // 1 essai sur TaskbarCreated + TRAY_ADD_MAX_RETRIES (15) réessais, puis plus rien.
    [Fact]
    public void C3_6_NimAddToujoursRefuse_ReessaisBornes()
    {
        using var iso = new BancCapture.Isolation();
        var (_, faux, wndProc) = Tray();
        faux.ResultatIcone = m => m != NimAdd;
        wndProc((IntPtr)0x1234, Win32.RegisterWindowMessageW("TaskbarCreated"), IntPtr.Zero, IntPtr.Zero);
        for (int tick = 0; tick < 40 && faux.Minuteries.Contains(TimerAjoutIcone); tick++)
            wndProc((IntPtr)0x1234, Win32.WM_TIMER, (IntPtr)TimerAjoutIcone, IntPtr.Zero);
        Assert.Equal(16, faux.AjoutsDepuis(0));
        Assert.DoesNotContain(TimerAjoutIcone, faux.Minuteries);
        Assert.False(faux.IconePresente);

        // Un nouvel Explorer ouvre une nouvelle série.
        faux.ResultatIcone = _ => true;
        wndProc((IntPtr)0x1234, Win32.RegisterWindowMessageW("TaskbarCreated"), IntPtr.Zero, IntPtr.Zero);
        Assert.True(faux.IconePresente);
    }

    // C3.6, TaskbarCreated répété alors que l'icône existe : NIM_ADD échoue, NIM_MODIFY
    // l'accepte, aucun réessai n'est armé.
    [Fact]
    public void C3_6_IconeDejaPresente_AucunReessai()
    {
        using var iso = new BancCapture.Isolation();
        var (_, faux, wndProc) = Tray();
        faux.IconePresente = true;
        wndProc((IntPtr)0x1234, Win32.RegisterWindowMessageW("TaskbarCreated"), IntPtr.Zero, IntPtr.Zero);
        Assert.True(faux.IconePresente);
        Assert.DoesNotContain(TimerAjoutIcone, faux.Minuteries);
    }

    // C3.6, nettoyage : un réessai en attente ne remet pas l'icône après Cleanup (WM_ENDSESSION
    // nettoie sans détruire la fenêtre).
    [Fact]
    public void C3_6_Cleanup_AnnuleLeReessaiEnAttente()
    {
        using var iso = new BancCapture.Isolation();
        var (tray, faux, wndProc) = Tray();
        faux.ResultatIcone = m => m != NimAdd;
        wndProc((IntPtr)0x1234, Win32.RegisterWindowMessageW("TaskbarCreated"), IntPtr.Zero, IntPtr.Zero);
        Assert.Contains(TimerAjoutIcone, faux.Minuteries);
        Appeler(tray, "Cleanup");
        Assert.DoesNotContain(TimerAjoutIcone, faux.Minuteries);
        faux.ResultatIcone = _ => true;
        int ajouts = faux.AjoutsDepuis(0);
        wndProc((IntPtr)0x1234, Win32.WM_TIMER, (IntPtr)TimerAjoutIcone, IntPtr.Zero);   // tick déjà en file
        Assert.Equal(ajouts, faux.AjoutsDepuis(0));
        Assert.False(faux.IconePresente);
    }

    // M3.8 I3, voie AFH-4 : verrouillage et veille oublient les appuis en cours ; l'heure d'un
    // avis de session envoyé est celle du traitement, pas celle du dernier message posté.
    [Fact]
    public void M3_8_I3_SessionEtVeille_OublientLesAppuisEnCours()
    {
        using var iso = new BancCapture.Isolation();
        using var banc = new BancFrappe(asyncLogique: false);
        var (tray, faux, wndProc) = Tray();
        BancCapture.SetField(tray, "_hook", banc.Hook);
        // Sans accord : ReinstallHook sort tout de suite (pas de vrai SetWindowsHookEx dans un test).
        BancCapture.SetField(tray, "_activationConsent", false);
        int declenchements = 0;
        banc.Hook.VirtualKeyboardRequested += () => declenchements++;

        void Tenir() { banc.Frapper("CtrlG", bas: true); banc.Frapper("MajG", bas: true); banc.Frapper("q", bas: true); }
        void Repeter() => banc.Frapper("q", bas: true);
        void Attendre() => banc.Jouer(new ActionFrappe(TypeAction.Temps, 40));

        int posteAvant = unchecked((int)banc.Heure);
        Attendre();
        Tenir();
        Assert.Equal(1, declenchements);
        Attendre();

        // Témoin : avis posté daté d'avant l'appui, rien n'est oublié, la répétition reste servie.
        faux.Envoye = false; faux.HeureMessage = posteAvant; faux.Maintenant = banc.Heure;
        wndProc((IntPtr)0x1234, Win32.WM_WTSSESSION_CHANGE, (IntPtr)WtsSessionLock, IntPtr.Zero);
        Repeter();
        Assert.Equal(1, declenchements);

        // Avis envoyé : même heure de dernier message posté, mais l'heure retenue est le traitement.
        faux.Envoye = true;
        wndProc((IntPtr)0x1234, Win32.WM_WTSSESSION_CHANGE, (IntPtr)WtsSessionLock, IntPtr.Zero);
        Repeter();
        Assert.Equal(2, declenchements);   // appui neuf

        // Veille : l'avis de départ n'oublie rien, la reprise oublie tout.
        Attendre();
        wndProc((IntPtr)0x1234, Win32.WM_POWERBROADCAST, (IntPtr)PbtApmSuspend, IntPtr.Zero);
        Repeter();
        Assert.Equal(2, declenchements);
        wndProc((IntPtr)0x1234, Win32.WM_POWERBROADCAST, (IntPtr)Win32.PBT_APMRESUMEAUTOMATIC, IntPtr.Zero);
        Repeter();
        Assert.Equal(3, declenchements);
    }
}
