using Xunit;

namespace TypingEngine.Windows.Tests;

/// <summary>Témoins des défauts relevés avant la publication 1.2.0, sans API native.</summary>
public class Audit120RegressionTests
{
    [Theory]
    [InlineData(0xA4)]
    [InlineData(0x5B)]
    [InlineData(0x5C)]
    public void RelacheRemappageCtrl_MemeSiAltOuWindowsEstEnfonce(uint modifier)
    {
        var api = new MockWin32Api();
        api.AsyncKeyStateScript[0xA2] = unchecked((short)0x8000);
        var layout = new Layout();
        layout.Keys[0x10] = new KeyDefinition { Position = "D01", Scancode = 0x10, Base = "a", Shift = "A" };
        var mapper = new KeyMapper(layout, api);
        mapper.TrackModifiers(0xA2, 0x1D, 0, true);
        Assert.True(mapper.ProcessKey(0x51, 0x10, 0, true));
        api.AsyncKeyStateScript[(int)modifier] = unchecked((short)0x8000);
        mapper.TrackModifiers(modifier, 0, 0, true);
        Assert.True(mapper.ProcessKey(0x51, 0x10, 0, false));
        Assert.Equal(new ushort[] { 0x41, 0x41 }, api.AllInputs.Select(i => i.u.ki.wVk));
        Assert.Equal(2u, api.AllInputs[1].u.ki.dwFlags & 2);
        mapper.ClearPassedThroughKeys();
        Assert.Equal(2, api.AllInputs.Length);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void Combo_RestaureChaqueMajPhysique(bool left, bool right, bool needsShift)
    {
        var mapper = new KeyMapper(new Layout(), new MockWin32Api());
        mapper.TrackModifiers(0xA0, 0x2A, 0, left);
        mapper.TrackModifiers(0xA1, 0x36, 0, right);
        var inputs = new List<Win32.INPUT>();
        mapper.BuildVkComboInputs(0x41, 0x1E, needsShift, false, false, false, IntPtr.Zero, inputs);
        AssertShiftState(inputs, left, right, needsShift, 0x1E);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void AltCode_RestaureChaqueMajPhysique(bool left, bool right, bool numLock)
    {
        var api = new MockWin32Api();
        api.KeyStateScript[0x90] = (short)(numLock ? 1 : 0);
        var mapper = new KeyMapper(new Layout(), api);
        mapper.TrackModifiers(0xA0, 0x2A, 0, left);
        mapper.TrackModifiers(0xA1, 0x36, 0, right);
        var inputs = new List<Win32.INPUT>();
        mapper.BuildAltCodeInputs(201, inputs);
        AssertShiftState(inputs, left, right, false, 0, numpad: true);
    }

    private static void AssertShiftState(List<Win32.INPUT> inputs, bool left, bool right,
        bool expectedDuring, ushort scanCode, bool numpad = false)
    {
        bool currentLeft = left, currentRight = right;
        int characters = 0;
        foreach (var input in inputs)
        {
            var key = input.u.ki;
            if (key.wVk == 0xA0) currentLeft = (key.dwFlags & 2) == 0;
            if (key.wVk == 0xA1) currentRight = (key.dwFlags & 2) == 0;
            if ((key.dwFlags & 2) == 0 &&
                (numpad ? key.wVk >= 0x60 && key.wVk <= 0x69 : key.wVk == 0 && key.wScan == scanCode))
            {
                characters++;
                Assert.Equal(expectedDuring, currentLeft || currentRight);
            }
        }
        Assert.True(characters > 0);
        Assert.Equal(left, currentLeft);
        Assert.Equal(right, currentRight);
    }

    [Fact]
    public void SondesLayout_PreserventEtatToucheMorte_EtCapsLock()
    {
        var api = new MockWin32Api();
        api.KeyStateScript[0x14] = 1;
        api.VkKeyScanScript[('A', api.CurrentHkl)] = 0x0141;
        api.MapVirtualKeyScript[(0x41, 0, api.CurrentHkl)] = 0x1E;
        var mapper = new KeyMapper(new Layout(), api);
        api.DeadKeyState = -1;
        var inputs = new List<Win32.INPUT>();
        Assert.True(mapper.BuildNativeComboInputs('A', api.CurrentHkl, inputs));
        Assert.Equal(3, api.ToUnicodeExFlags.Count);
        Assert.All(api.ToUnicodeExFlags, flag => Assert.Equal(4u, flag));
        Assert.Equal(-1, api.DeadKeyState);
        Assert.Equal(2, inputs.Count); // Caps actif annule Maj demandé.
    }

    [Fact]
    public void RetirerCouche_PurgeVerrousEtHistorique_SansEffacerAutresCouches()
    {
        long clock = 100;
        var manager = new MaintainableLayerManager(() => clock);
        var a = new ForegroundProcessIdentity(1, 10);
        var b = new ForegroundProcessIdentity(2, 20);
        manager.ApplySettings(true, new[] { "dk_greek", "dk_cyrillic" }, 500);
        manager.SetForeground(a, false);
        Tap(manager, "dk_greek");
        clock += 100;
        Tap(manager, "dk_greek");
        Assert.Equal(MaintainableLayerMode.Locked, manager.CurrentState.Mode);
        manager.SetForeground(b, false);
        Tap(manager, "dk_cyrillic");
        clock += 100;
        Tap(manager, "dk_cyrillic");
        manager.ApplySettings(true, new[] { "dk_cyrillic" }, 500);
        Assert.Equal("dk_cyrillic", manager.CurrentState.LayerId);
        manager.SetForeground(a, false);
        Assert.False(manager.CurrentState.IsActive);
        manager.ApplySettings(true, new[] { "dk_greek", "dk_cyrillic" }, 500);
        Assert.False(manager.CurrentState.IsActive);
        Tap(manager, "dk_greek");
        Assert.Equal(MaintainableLayerMode.OneShot, manager.CurrentState.Mode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetirerCouche_PurgeDeclencheurEtOneShot(bool released)
    {
        var manager = new MaintainableLayerManager(() => 100);
        manager.ApplySettings(true, new[] { "dk_greek" }, 500);
        manager.SetForeground(new(1, 10), false);
        manager.BeginTrigger("dk_greek", 10);
        if (released) manager.EndTrigger(10);
        manager.ApplySettings(true, Array.Empty<string>(), 500);
        Assert.False(manager.CurrentState.IsActive);
        Assert.Null(manager.PendingTriggerScanCode);
        manager.ApplySettings(true, new[] { "dk_greek" }, 500);
        Tap(manager, "dk_greek");
        Assert.Equal(MaintainableLayerMode.OneShot, manager.CurrentState.Mode);
    }

    private static void Tap(MaintainableLayerManager manager, string layer)
    {
        Assert.True(manager.BeginTrigger(layer, 10));
        Assert.True(manager.EndTrigger(10));
    }
}
