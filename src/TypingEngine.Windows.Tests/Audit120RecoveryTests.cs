using Xunit;

namespace TypingEngine.Windows.Tests;

/// <summary>La compensation rétablit l'état des touches ; elle ne rejoue jamais le texte.</summary>
public class Audit120RecoveryTests
{
    private static (MockWin32Api Api, KeyMapper Mapper, ForegroundMonitor Monitor) NativeMapper()
    {
        var api = new MockWin32Api { ScriptedProcessName = "editor.exe" };
        api.VkKeyScanScript[('A', api.CurrentHkl)] = 0x0141;
        api.MapVirtualKeyScript[(0x41, 0, api.CurrentHkl)] = 0x1E;
        var host = new TestWindowsTypingHost();
        host.SetCompatibilityOverride("editor.exe", "forceOn");
        var monitor = new ForegroundMonitor(api, IntPtr.Zero, host);
        var mapper = new KeyMapper(new Layout(), api, host);
        mapper.SetForegroundMonitor(monitor);
        return (api, mapper, monitor);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ComboPartielle_RelacheMajEtCaractereSansRejouer(uint sent)
    {
        var (api, mapper, monitor) = NativeMapper();
        using (monitor)
        {
            api.SendInputResults.Enqueue(sent);
            var result = mapper.TryEmitText("A");
            Assert.False(result.IsComplete);
            Assert.Equal(sent, result.SentEvents);
            Assert.Equal(2, api.SendInputCalls.Count);
            Assert.All(api.SendInputCalls[1], input => Assert.Equal(2u, input.u.ki.dwFlags & 2));
            Assert.Contains(api.SendInputCalls[1], input => input.u.ki.wVk == 0xA0);
            Assert.Contains(api.SendInputCalls[1], input => input.u.ki.wScan == 0x1E);
        }
    }

    [Fact]
    public void CompensationRefusee_BloqueNouvelleEmission_PuisSeReprend()
    {
        var (api, mapper, monitor) = NativeMapper();
        using (monitor)
        {
            api.SendInputResults.Enqueue(1);
            api.SendInputResults.Enqueue(0);
            Assert.False(mapper.TryEmitText("A").IsComplete);
            api.SendInputResults.Enqueue(0);
            Assert.True(mapper.TryEmitText("A").Blocked);
            Assert.Equal(3, api.SendInputCalls.Count);
            Assert.All(api.SendInputCalls[2], input => Assert.Equal(2u, input.u.ki.dwFlags & 2));
            Assert.True(mapper.TryEmitText("A").IsComplete);
            Assert.Equal(5, api.SendInputCalls.Count); // Compensation complète, puis seulement nouveau texte.
        }
    }

    [Fact]
    public void CompensationDifferee_NeRestaurePasUnMajRelacheEntreTemps()
    {
        var api = new MockWin32Api();
        var mapper = new KeyMapper(new Layout(), api);
        mapper.TrackModifiers(0xA1, 0x36, 0, true);
        var inputs = new List<Win32.INPUT>();
        mapper.BuildVkComboInputs(0x41, 0x1E, false, false, false, false, IntPtr.Zero, inputs);
        var recovery = new InputRecovery(inputs.ToArray(), true);
        Assert.Contains(recovery.Build(api, _ => true), i => i.u.ki.wVk == 0xA1 && (i.u.ki.dwFlags & 2) == 0);
        recovery.MarkDeferred();
        Assert.DoesNotContain(recovery.Build(api, _ => true), i => i.u.ki.wVk == 0xA1 && (i.u.ki.dwFlags & 2) == 0);
    }

    [Fact]
    public void NumLock_RepareDepuisEtatCourant_EtRespecteNouveauChoix()
    {
        var api = new MockWin32Api();
        var mapper = new KeyMapper(new Layout(), api);
        var inputs = new List<Win32.INPUT>();
        mapper.BuildAltCodeInputs(201, inputs);
        var recovery = new InputRecovery(inputs.ToArray(), false);
        api.KeyStateScript[0x90] = 1; // Le premier toggle a réussi, le second manque.
        var repair = recovery.Build(api, _ => false);
        Assert.Single(repair.Where(i => i.u.ki.wVk == 0x90 && (i.u.ki.dwFlags & 2) == 0));
        recovery.CancelNumLockRestoration();
        Assert.DoesNotContain(recovery.Build(api, _ => false), i => i.u.ki.wVk == 0x90);
    }

    [Fact]
    public void RelachementsPartiels_ConserventTouteLaTraceJusquaAcceptationComplete()
    {
        var api = new MockWin32Api();
        api.AsyncKeyStateScript[0xA2] = unchecked((short)0x8000);
        var layout = new Layout();
        layout.Keys[0x10] = new KeyDefinition { Position = "D01", Scancode = 0x10, Base = "a", Shift = "A" };
        layout.Keys[0x11] = new KeyDefinition { Position = "D02", Scancode = 0x11, Base = "z", Shift = "Z" };
        var mapper = new KeyMapper(layout, api);
        mapper.TrackModifiers(0xA2, 0x1D, 0, true);
        mapper.ProcessKey(0x51, 0x10, 0, true);
        mapper.ProcessKey(0x57, 0x11, 0, true);
        api.SendInputCalls.Clear();
        api.SendInputResults.Enqueue(1);
        mapper.ClearPassedThroughKeys();
        mapper.ClearPassedThroughKeys();
        mapper.ClearPassedThroughKeys();
        Assert.Equal(2, api.SendInputCalls.Count);
        Assert.Equal(2, api.SendInputCalls[0].Length);
        Assert.Equal(2, api.SendInputCalls[1].Length);
        Assert.All(api.AllInputs, i => Assert.Equal(2u, i.u.ki.dwFlags & 2));
    }

    [Fact]
    public void PauseVolontaire_BloqueAussiEmissionDirecteEtCapsLock()
    {
        var api = new MockWin32Api();
        api.KeyStateScript[0x14] = 1;
        var mapper = new KeyMapper(new Layout(), api) { EmissionPaused = true };
        Assert.True(mapper.TryEmitText("x").Blocked);
        mapper.RequestCapsLockOff();
        Assert.Empty(api.SendInputCalls);
    }

    [Fact]
    public void Menu_GardeDerniereApplicationExterne_QuandNotreFenetrePrendFocus()
    {
        var api = new MockWin32Api { ScriptedProcessName = "editor.exe", ScriptedFullPath = @"C:\editor.exe" };
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero);
        api.ScriptedPid = (uint)Environment.ProcessId;
        api.SimulateForegroundChange("AZERTY Global.exe", @"C:\AZERTY Global.exe", api.CurrentHkl);
        Assert.Equal("AZERTY Global.exe", monitor.CurrentProcessName);
        Assert.Equal("editor.exe", monitor.LastApplicationProcessName);
        Assert.Equal(@"C:\editor.exe", monitor.LastApplicationFullPath);
    }

    [Fact]
    public void FenetreChangeAvantNotification_InterditSnapshotPerime()
    {
        var api = new MockWin32Api { ScriptedProcessName = "editor.exe" };
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero);
        api.ForegroundWindow = (IntPtr)999;
        Assert.Equal(CompatibilityMode.DisabledAntiCheat, monitor.GetEmitContext().Mode);
    }
}
