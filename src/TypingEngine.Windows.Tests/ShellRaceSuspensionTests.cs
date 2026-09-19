using Xunit;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Écart mesuré en VM le 2026-09-19 : ouvrir la zone de notification suspendait le
/// remapping. Cause : la fenêtre de premier plan passe d'explorer.exe au volet
/// ShellExperienceHost.exe entre les deux lectures de GetForegroundWindow d'un même
/// Recompute, et la branche de course fabriquait une identité inconnue.
/// </summary>
public class ShellRaceSuspensionTests
{
    [Fact]
    public void FenetreChangeEntreDeuxLectures_NeSuspendPlus()
    {
        var api = new MockWin32Api { ScriptedProcessName = "explorer.exe", ScriptedFullPath = @"C:\Windows\explorer.exe" };
        api.ForegroundWindowScript.Enqueue((IntPtr)0x1111);
        api.ForegroundWindowScript.Enqueue((IntPtr)0x2222);

        using var monitor = new ForegroundMonitor(api, IntPtr.Zero);

        Assert.Equal(CompatibilityMode.Default, monitor.CurrentMode);
        Assert.Equal(CompatibilitySuspendReason.None, monitor.CurrentSuspendReason);
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
