using Xunit;

namespace TypingEngine.Windows.Tests;

public class Audit120ResumeTests
{
    private static Layout LayoutWithLetters()
    {
        var layout = new Layout();
        layout.Keys[0x10] = new KeyDefinition { Position = "D01", Scancode = 0x10, Base = "a", Shift = "A" };
        layout.Keys[0x11] = new KeyDefinition { Position = "D02", Scancode = 0x11, Base = "z", Shift = "Z" };
        return layout;
    }

    [Fact]
    public void KeyUpPossede_PasseAvantUneAutreCompensationRefusee()
    {
        var api = new MockWin32Api();
        api.AsyncKeyStateScript[0xA2] = unchecked((short)0x8000);
        var mapper = new KeyMapper(LayoutWithLetters(), api);
        mapper.TrackModifiers(0xA2, 0x1D, 0, true);
        Assert.True(mapper.ProcessKey(0x51, 0x10, 0, true));
        api.SendInputResults.Enqueue(1);
        api.SendInputResults.Enqueue(0);
        Assert.False(mapper.TryEmitText("xy").IsComplete);
        Assert.True(mapper.ProcessKey(0x51, 0x10, 0, false));
        var release = Assert.Single(api.SendInputCalls.Last());
        Assert.Equal((ushort)0x41, release.u.ki.wVk);
        Assert.Equal(2u, release.u.ki.dwFlags & 2);
    }

    [Fact]
    public void EchecCompensation_AssocieChaqueKeyUpAuDownQuiAPasseOuEteAbsorbe()
    {
        var api = new MockWin32Api();
        var mapper = new KeyMapper(LayoutWithLetters(), api);
        api.SendInputResults.Enqueue(1);
        api.SendInputResults.Enqueue(0);
        Assert.True(mapper.ProcessKey(0x51, 0x10, 0, true)); // Down absorbé ; émission partielle.
        api.SendInputResults.Enqueue(0);
        Assert.True(mapper.ProcessKey(0x51, 0x10, 0, true)); // Répétition absorbée aussi.
        api.SendInputResults.Enqueue(0);
        Assert.True(mapper.ProcessKey(0x51, 0x10, 0, false));
        api.SendInputResults.Enqueue(0);
        Assert.False(mapper.ProcessKey(0x57, 0x11, 0, true)); // Nouveau down physique passe.
        Assert.False(mapper.ProcessKey(0x57, 0x11, 0, true)); // Sa répétition reste physique.
        Assert.False(mapper.ProcessKey(0x57, 0x11, 0, false)); // Compensation rétablie : son up passe encore.
    }

    [Fact]
    public void NouvelAppuiPendantSuspension_ResteAssocieASonKeyUpApresReprise()
    {
        var api = new MockWin32Api();
        var mapper = new KeyMapper(LayoutWithLetters(), api);
        Assert.True(mapper.ProcessKey(0x51, 0x10, 0, true));
        KeyboardHook.ObserveSuspendedModifiers(mapper, 0x51, 0x10, 0, false);
        KeyboardHook.ObserveSuspendedModifiers(mapper, 0x51, 0x10, 0, true);
        mapper.SyncState();
        Assert.False(mapper.ProcessKey(0x51, 0x10, 0, true)); // Répétition du nouvel appui.
        Assert.False(mapper.ProcessKey(0x51, 0x10, 0, false));
    }

    [Fact]
    public void RelachementDansCibleSuspendue_ConserveLaQuarantaineDeLaCiblePrecedente()
    {
        var api = new MockWin32Api { ScriptedProcessName = "editor.exe" };
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero);
        var mapper = new KeyMapper(LayoutWithLetters(), api);
        mapper.SetForegroundMonitor(monitor);
        Assert.False(mapper.ProcessKey(0x41, 0x10, 0, true)); // A-down transmis à l'éditeur.
        api.SimulateForegroundChange("VALORANT.exe", @"C:\Riot\VALORANT.exe", api.CurrentHkl);
        KeyboardHook.ObserveSuspendedModifiers(mapper, 0x41, 0x10, 0, false);
        mapper.ClearPassedThroughKeys();
        Assert.Empty(api.SendInputCalls);
        api.SimulateForegroundChange("editor.exe", @"C:\editor.exe", api.CurrentHkl);
        mapper.SyncState();
        var release = Assert.Single(Assert.Single(api.SendInputCalls));
        Assert.Equal((ushort)0x10, release.u.ki.wScan);
        Assert.Equal(2u, release.u.ki.dwFlags & 2);
        mapper.ClearPassedThroughKeys();
        Assert.Single(api.SendInputCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MajPendantSuspension_EstSuiviSansNotificationPuisRespecte(bool held)
    {
        var api = new MockWin32Api { ScriptedProcessName = "editor.exe" };
        var host = new TestWindowsTypingHost();
        host.SetCompatibilityOverride("editor.exe", "forceOn");
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero, host);
        api.VkKeyScanScript[('A', api.CurrentHkl)] = 0x0141;
        api.MapVirtualKeyScript[(0x41, 0, api.CurrentHkl)] = 0x1E;
        var mapper = new KeyMapper(LayoutWithLetters(), api);
        mapper.SetForegroundMonitor(monitor);
        api.SendInputResults.Enqueue(1);
        api.SendInputResults.Enqueue(0);
        mapper.TryEmitText("A");
        mapper.EmissionPaused = true;
        int notifications = 0;
        mapper.StateChanged += () => notifications++;
        KeyboardHook.ObserveSuspendedModifiers(mapper, 0xA0, 0x2A, 0, held);
        api.AsyncKeyStateScript[0xA0] = held ? unchecked((short)0x8000) : (short)0;
        Assert.Equal(0, notifications);
        Assert.Equal(2, api.SendInputCalls.Count);
        mapper.EmissionPaused = false;
        mapper.SyncState();
        var shift = Assert.Single(api.SendInputCalls.Last().Where(i => i.u.ki.wVk == 0xA0));
        Assert.Equal(held ? 0u : 2u, shift.u.ki.dwFlags & 2);
    }

    [Fact]
    public void NumLockPendantSuspension_AnnuleTousLesEvenementsDeRestauration()
    {
        var api = new MockWin32Api { ScriptedProcessName = "editor.exe" };
        var host = new TestWindowsTypingHost();
        host.SetCompatibilityOverride("editor.exe", "forceOn");
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero, host);
        var mapper = new KeyMapper(LayoutWithLetters(), api);
        mapper.SetForegroundMonitor(monitor);
        api.SendInputResults.Enqueue(1);
        api.SendInputResults.Enqueue(0);
        mapper.TryEmitText("É"); // Alt+code avec NumLock initialement éteint.
        KeyboardHook.ObserveSuspendedModifiers(mapper, 0x90, 0x45, 0, true);
        mapper.ClearPassedThroughKeys();
        Assert.DoesNotContain(api.SendInputCalls.Last(), i => i.u.ki.wVk == 0x90);
    }
}
