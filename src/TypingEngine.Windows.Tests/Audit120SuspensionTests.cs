using Xunit;

namespace TypingEngine.Windows.Tests;

public class Audit120SuspensionTests
{
    [Theory]
    [InlineData("explorer.exe")]
    [InlineData("SearchHost.exe")]
    [InlineData("StartMenuExperienceHost.exe")]
    public void Shell_ActualiseIdentiteLayoutEtChampSecurise(string shell)
    {
        var api = new MockWin32Api { ScriptedProcessName = "editor.exe" };
        var host = new TestWindowsTypingHost();
        host.SetCompatibilityOverride("editor.exe", "forceOn");
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero, host);
        api.ScriptedPid = 5678;
        api.ScriptedProcessStartTime = 222;
        api.ScriptedSecureInput = true;
        api.SimulateForegroundChange(shell, @"C:\Windows\shell.exe", (IntPtr)0x04090409);
        Assert.Equal(shell, monitor.CurrentProcessName);
        Assert.Equal(new ForegroundProcessIdentity(5678, 222), monitor.CurrentIdentity);
        Assert.Equal((IntPtr)0x04090409, monitor.CurrentHkl);
        Assert.True(monitor.IsSecureInput);
        Assert.Equal(CompatibilityMode.Default, monitor.CurrentMode);
    }

    [Fact]
    public void EchecSuiviForeground_SuspendEmission()
    {
        var api = new MockWin32Api { ScriptedProcessName = "editor.exe", ShouldFailSetWinEventHook = true };
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero);
        Assert.Equal(CompatibilityMode.DisabledAntiCheat, monitor.CurrentMode);
        Assert.Equal(CompatibilitySuspendReason.UnknownForeground, monitor.CurrentSuspendReason);
    }

    [Fact]
    public void Suspension_NettoieSansEmettreDansNouvelleCible()
    {
        var api = new MockWin32Api { ScriptedProcessName = "editor.exe" };
        api.AsyncKeyStateScript[0xA2] = unchecked((short)0x8000);
        var layout = new Layout();
        layout.Keys[0x10] = new KeyDefinition { Position = "D01", Scancode = 0x10, Base = "a", Shift = "A" };
        var host = new TestWindowsTypingHost();
        var mapper = new KeyMapper(layout, api, host);
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero);
        mapper.SetForegroundMonitor(monitor);
        mapper.TrackModifiers(0xA2, 0x1D, 0, true);
        mapper.ProcessKey(0x51, 0x10, 0, true);
        api.SendInputCalls.Clear();
        api.SimulateForegroundChange("VALORANT.exe", @"C:\Riot\VALORANT.exe", api.CurrentHkl);
        mapper.ClearPassedThroughKeys();
        mapper.EmitText("α");
        api.KeyStateScript[0x14] = 1;
        mapper.RequestCapsLockOff();
        Assert.Empty(api.SendInputCalls);
        Assert.Empty(host.EmittedTexts);
        api.SimulateForegroundChange("editor.exe", @"C:\editor.exe", api.CurrentHkl);
        mapper.ClearPassedThroughKeys();
        Assert.Single(api.SendInputCalls);
        Assert.Equal((ushort)0x41, api.AllInputs[0].u.ki.wVk);
        Assert.Equal(2u, api.AllInputs[0].u.ki.dwFlags & 2); // Relâchement différé à la reprise sûre.
        mapper.ClearPassedThroughKeys();
        Assert.Single(api.SendInputCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void EmissionIncomplete_NeComptePasLeTexte(uint sent)
    {
        var api = new MockWin32Api { SendInputResult = sent };
        var host = new TestWindowsTypingHost();
        var mapper = new KeyMapper(new Layout(), api, host);
        mapper.EmitText("ab");
        Assert.Equal(sent == 0 ? 1 : 2, api.SendInputCalls.Count);
        Assert.Empty(host.EmittedTexts);
    }
}
