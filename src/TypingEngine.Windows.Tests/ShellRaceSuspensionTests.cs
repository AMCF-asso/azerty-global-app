using Xunit;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Écart mesuré en VM le 2026-09-19 : ouvrir la zone de notification suspendait le
/// remapping. Cause : la fenêtre de premier plan passe d'explorer.exe au volet
/// ShellExperienceHost.exe entre les deux lectures de GetForegroundWindow d'un même
/// Recompute, et la branche de course fabriquait une identité inconnue.
///
/// ⛔ Ces témoins ont été réécrits le 2026-09-22. La version du 19/09 empilait deux fenêtres
/// dans <c>ForegroundWindowScript</c> sans rien asserter sur leur consommation : depuis le
/// correctif, <c>Recompute</c> ne lit la fenêtre qu'une fois, la seconde valeur restait dans
/// la file, et le test passait à l'identique avec ou sans le correctif qu'il prétendait
/// prouver (§ 3.2 de la revue du 2026-09-21). Ce qui suit vérifie d'abord que le scénario est
/// joué, puis ce que le correctif garantit réellement.
/// </summary>
public class ShellRaceSuspensionTests
{
    private static MockWin32Api ShellApi(string processName) => new()
    {
        ScriptedProcessName = processName,
        ScriptedFullPath = @"C:\Windows\" + processName,
    };

    [Fact]
    public void La_fenetre_qui_change_entre_deux_recomputes_ne_suspend_pas()
    {
        var api = ShellApi("explorer.exe");
        api.ForegroundWindowScript.Enqueue((IntPtr)0x1111);
        api.ForegroundWindowScript.Enqueue((IntPtr)0x2222);

        using var monitor = new ForegroundMonitor(api, IntPtr.Zero);
        int apresConstruction = api.ForegroundWindowReads;

        api.ScriptedProcessName = "ShellExperienceHost.exe";
        api.ScriptedFullPath = @"C:\Windows\ShellExperienceHost.exe";
        monitor.Recompute();

        // ⛔ Le garde-fou du témoin lui-même : si le code cessait de lire la fenêtre, ou si la
        // file n'était pas consommée, ces deux assertions rougiraient avant celles qui portent
        // sur le comportement. C'est précisément ce qui manquait à la version du 19/09.
        Assert.True(api.ForegroundWindowReads > apresConstruction,
            "Recompute n'a pas relu la fenêtre de premier plan : le scénario n'est pas joué.");
        Assert.Empty(api.ForegroundWindowScript);

        Assert.Equal(CompatibilityMode.Default, monitor.CurrentMode);
        Assert.Equal(CompatibilitySuspendReason.None, monitor.CurrentSuspendReason);
    }

    [Theory]
    [InlineData("explorer.exe")]
    [InlineData("ShellExperienceHost.exe")]
    [InlineData("SearchHost.exe")]
    [InlineData("StartMenuExperienceHost.exe")]
    [InlineData("TextInputHost.exe")]
    public void Aucune_surface_du_shell_ne_suspend(string processName)
    {
        var api = ShellApi(processName);

        using var monitor = new ForegroundMonitor(api, IntPtr.Zero);

        Assert.Equal(CompatibilityMode.Default, monitor.CurrentMode);
        Assert.Equal(CompatibilitySuspendReason.None, monitor.CurrentSuspendReason);
    }

    [Fact]
    public void Seul_un_suivi_indisponible_suspend_encore()
    {
        // ⛔ La réciproque, sans laquelle les témoins ci-dessus ne prouvent rien : ils
        // pourraient passer parce que **plus rien** ne suspend. Une cause de suspension doit
        // rester vivante.
        var api = ShellApi("explorer.exe");
        api.ShouldFailSetWinEventHook = true;

        using var monitor = new ForegroundMonitor(api, IntPtr.Zero);

        Assert.False(monitor.IsTrackingAvailable);
        Assert.NotEqual(CompatibilitySuspendReason.None, monitor.CurrentSuspendReason);
    }

    /// <summary>
    /// Réciproque, et justification du retrait : la garde qui compte vraiment est celle de
    /// l'émission. Fenêtre déplacée après le calcul, GetEmitContext refuse toujours d'émettre.
    /// </summary>
    [Fact]
    public void FenetreDeplaceeApresCalcul_LEmissionResteRefusee()
    {
        var api = new MockWin32Api { ScriptedProcessName = "editor.exe", ScriptedFullPath = @"C:\editor.exe" };
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero);
        Assert.Equal(CompatibilityMode.Default, monitor.CurrentMode);

        api.ForegroundWindow = (IntPtr)0x7777;
        var ctx = monitor.GetEmitContext();

        Assert.Equal(CompatibilityMode.DisabledAntiCheat, ctx.Mode);
        Assert.Equal(IntPtr.Zero, ctx.Hkl);
    }
}
