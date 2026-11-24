using System.Reflection;
using System.Runtime.InteropServices;
using TypingEngine.Core;
using TypingEngine.Windows;
using Xunit;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Témoins V1.3.0 : modificateurs de raccourcis configurables dans le moteur Windows
/// (règles de combinaison, exclusion AltGr/Win, déclenchement par le hook).
/// </summary>
public sealed class ModificateursRaccourcisTests : IDisposable
{
    private const uint WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_SYSKEYDOWN = 0x104, WM_SYSKEYUP = 0x105;
    private const uint VK_LSHIFT = 0xA0;
    private const uint VK_LCONTROL = 0xA2;
    private const uint VK_LMENU = 0xA4;
    private const uint VK_RMENU = 0xA5;
    private const uint VK_LWIN = 0x5B;
    private const uint VK_CAPITAL = 0x14;
    private const uint VK_Q = 0x51;
    private const uint VK_W = 0x57;
    private const uint SC_Q = 0x1E;
    private const uint SC_W = 0x2C;
    private const uint SC_LSHIFT = 0x2A;
    private const uint SC_LCONTROL = 0x1D;
    private const uint SC_LMENU = 0x38;
    private const uint SC_CAPS = 0x3A;
    private const uint LLKHF_EXTENDED = 0x01;

    private readonly IntPtr _lParam = Marshal.AllocHGlobal(Marshal.SizeOf<Win32.KBDLLHOOKSTRUCT>());

    public void Dispose() => Marshal.FreeHGlobal(_lParam);

    private static Layout CreateMinimalLayout()
    {
        var layout = new Layout();
        layout.Keys[SC_Q] = new KeyDefinition { Position = "C01", Scancode = SC_Q, Base = "q", Shift = "Q" };
        layout.Keys[SC_W] = new KeyDefinition { Position = "B01", Scancode = SC_W, Base = "w", Shift = "W" };
        return layout;
    }

    private static MockWin32Api CreateApi()
    {
        var api = new MockWin32Api { ScriptedProcessName = "notepad.exe" };
        foreach (var vk in new[] { VK_LSHIFT, VK_LCONTROL, VK_LMENU, VK_RMENU, VK_LWIN })
            api.AsyncKeyStateScript[(int)vk] = unchecked((short)0x8000);
        return api;
    }

    private long Rappel(Win32.LowLevelKeyboardProc proc, uint msg, uint vk, uint scan, uint flags = 0)
    {
        Marshal.StructureToPtr(new Win32.KBDLLHOOKSTRUCT { vkCode = vk, scanCode = scan, flags = flags }, _lParam, false);
        return proc(0, (IntPtr)msg, _lParam).ToInt64();
    }

    private static Win32.LowLevelKeyboardProc GetProc(KeyboardHook hook) =>
        (Win32.LowLevelKeyboardProc)typeof(KeyboardHook)
            .GetField("_proc", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(hook)!;

    [Theory]
    [InlineData(ShortcutModifiers.Ctrl | ShortcutModifiers.Shift, true)]
    [InlineData(ShortcutModifiers.Ctrl | ShortcutModifiers.Alt, true)]
    [InlineData(ShortcutModifiers.Shift | ShortcutModifiers.Alt, true)]
    [InlineData(ShortcutModifiers.Ctrl | ShortcutModifiers.Shift | ShortcutModifiers.Alt, true)]
    [InlineData(ShortcutModifiers.None, false)]
    [InlineData(ShortcutModifiers.Ctrl, false)]
    [InlineData(ShortcutModifiers.Shift, false)]
    [InlineData(ShortcutModifiers.Alt, false)]
    [InlineData((ShortcutModifiers)8, false)]
    public void Regles_ExigeAuMoinsDeuxModificateursValides(ShortcutModifiers mods, bool attendu)
    {
        Assert.Equal(attendu, ShortcutModifierRules.IsValid(mods));
    }

    [Theory]
    [InlineData(ShortcutModifiers.Ctrl | ShortcutModifiers.Shift, "ctrl+shift")]
    [InlineData(ShortcutModifiers.Ctrl | ShortcutModifiers.Alt, "ctrl+alt")]
    [InlineData(ShortcutModifiers.Shift | ShortcutModifiers.Alt, "alt+shift")]
    [InlineData(ShortcutModifiers.Ctrl | ShortcutModifiers.Shift | ShortcutModifiers.Alt, "ctrl+alt+shift")]
    public void Serialisation_AllerRetourFidele(ShortcutModifiers mods, string chaine)
    {
        Assert.Equal(chaine, ShortcutModifierRules.Serialize(mods));
        Assert.Equal(mods, ShortcutModifierRules.ParseOrDefault(chaine));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ctrl")]
    [InlineData("win+shift")]
    [InlineData("inconnu")]
    public void ParseOrDefault_RetombeSurCtrlMajSiInvalide(string? brut)
    {
        Assert.Equal(ShortcutModifierRules.Default, ShortcutModifierRules.ParseOrDefault(brut));
    }

    [Fact]
    public void KeyMapper_CtrlAltGauche_CorrespondMaisPasAltGrNiWin()
    {
        var api = CreateApi();
        var mapper = new KeyMapper(CreateMinimalLayout(), api);
        const ShortcutModifiers ctrlAlt = ShortcutModifiers.Ctrl | ShortcutModifiers.Alt;

        // Ctrl gauche + Alt gauche → vrai Ctrl+Alt
        mapper.TrackModifiers(VK_LCONTROL, SC_LCONTROL, 0, true);
        mapper.TrackModifiers(VK_LMENU, SC_LMENU, 0, true);
        Assert.True(mapper.MatchesShortcutModifiers(ctrlAlt));
        Assert.False(mapper.MatchesShortcutModifiers(ShortcutModifierRules.Default));

        // Ajout de Win → refusé
        mapper.TrackModifiers(VK_LWIN, 0x5B, LLKHF_EXTENDED, true);
        Assert.False(mapper.MatchesShortcutModifiers(ctrlAlt));
        mapper.TrackModifiers(VK_LWIN, 0x5B, LLKHF_EXTENDED, false);
        Assert.True(mapper.MatchesShortcutModifiers(ctrlAlt));

        // Relâche Alt gauche, appui Alt droit avec Ctrl gauche = AltGr → refusé
        mapper.TrackModifiers(VK_LMENU, SC_LMENU, 0, false);
        mapper.TrackModifiers(VK_RMENU, SC_LMENU, LLKHF_EXTENDED, true);
        Assert.False(mapper.MatchesShortcutModifiers(ctrlAlt));
    }

    [Fact]
    public void Hook_DeclencheRechercheSurAltMajW_EtPasSurCtrlMajW()
    {
        var api = CreateApi();
        var host = new TestWindowsTypingHost
        {
            ShortcutCharacterSearchVk = VK_W,
            ShortcutCharacterSearchModifiers = ShortcutModifiers.Shift | ShortcutModifiers.Alt,
            ShortcutVirtualKeyboardVk = VK_Q,
            ShortcutVirtualKeyboardModifiers = ShortcutModifiers.Ctrl | ShortcutModifiers.Alt,
        };
        var mapper = new KeyMapper(CreateMinimalLayout(), api, host);
        using var hook = new KeyboardHook(mapper, host);
        var proc = GetProc(hook);

        int searchCount = 0;
        int kbdCount = 0;
        hook.SearchRequested += () => searchCount++;
        hook.VirtualKeyboardRequested += () => kbdCount++;

        // Ctrl+Maj+W ne doit PAS déclencher la recherche (elle est sur Alt+Maj+W)
        Rappel(proc, WM_KEYDOWN, VK_LCONTROL, SC_LCONTROL);
        Rappel(proc, WM_KEYDOWN, VK_LSHIFT, SC_LSHIFT);
        Rappel(proc, WM_KEYDOWN, VK_W, SC_W);
        Rappel(proc, WM_KEYUP, VK_W, SC_W);
        Rappel(proc, WM_KEYUP, VK_LSHIFT, SC_LSHIFT);
        Rappel(proc, WM_KEYUP, VK_LCONTROL, SC_LCONTROL);
        Assert.Equal(0, searchCount);

        // Alt+Maj+W déclenche la recherche
        Rappel(proc, WM_SYSKEYDOWN, VK_LMENU, SC_LMENU);
        Rappel(proc, WM_SYSKEYDOWN, VK_LSHIFT, SC_LSHIFT);
        long retSearch = Rappel(proc, WM_SYSKEYDOWN, VK_W, SC_W);
        Rappel(proc, WM_SYSKEYUP, VK_W, SC_W);
        Rappel(proc, WM_KEYUP, VK_LSHIFT, SC_LSHIFT);
        Rappel(proc, WM_KEYUP, VK_LMENU, SC_LMENU);
        Assert.Equal(1, retSearch);
        Assert.Equal(1, searchCount);

        // Ctrl+Alt+Q déclenche le clavier virtuel
        Rappel(proc, WM_KEYDOWN, VK_LCONTROL, SC_LCONTROL);
        Rappel(proc, WM_SYSKEYDOWN, VK_LMENU, SC_LMENU);
        long retKbd = Rappel(proc, WM_SYSKEYDOWN, VK_Q, SC_Q);
        Rappel(proc, WM_SYSKEYUP, VK_Q, SC_Q);
        Rappel(proc, WM_KEYUP, VK_LMENU, SC_LMENU);
        Rappel(proc, WM_KEYUP, VK_LCONTROL, SC_LCONTROL);
        Assert.Equal(1, retKbd);
        Assert.Equal(1, kbdCount);
    }

    [Fact]
    public void Hook_MemeLettreAvecModificateursDifferents_LesDeuxFonctionnent()
    {
        var api = CreateApi();
        var host = new TestWindowsTypingHost
        {
            ShortcutCharacterSearchVk = VK_Q,
            ShortcutCharacterSearchModifiers = ShortcutModifiers.Shift | ShortcutModifiers.Alt,
            ShortcutVirtualKeyboardVk = VK_Q,
            ShortcutVirtualKeyboardModifiers = ShortcutModifiers.Ctrl | ShortcutModifiers.Alt,
        };
        var mapper = new KeyMapper(CreateMinimalLayout(), api, host);
        using var hook = new KeyboardHook(mapper, host);
        var proc = GetProc(hook);

        int searchCount = 0;
        int kbdCount = 0;
        hook.SearchRequested += () => searchCount++;
        hook.VirtualKeyboardRequested += () => kbdCount++;

        Rappel(proc, WM_SYSKEYDOWN, VK_LMENU, SC_LMENU);
        Rappel(proc, WM_SYSKEYDOWN, VK_LSHIFT, SC_LSHIFT);
        Rappel(proc, WM_SYSKEYDOWN, VK_Q, SC_Q);
        Rappel(proc, WM_SYSKEYUP, VK_Q, SC_Q);
        Rappel(proc, WM_KEYUP, VK_LSHIFT, SC_LSHIFT);
        Rappel(proc, WM_KEYUP, VK_LMENU, SC_LMENU);

        Rappel(proc, WM_KEYDOWN, VK_LCONTROL, SC_LCONTROL);
        Rappel(proc, WM_SYSKEYDOWN, VK_LMENU, SC_LMENU);
        Rappel(proc, WM_SYSKEYDOWN, VK_Q, SC_Q);
        Rappel(proc, WM_SYSKEYUP, VK_Q, SC_Q);
        Rappel(proc, WM_KEYUP, VK_LMENU, SC_LMENU);
        Rappel(proc, WM_KEYUP, VK_LCONTROL, SC_LCONTROL);

        Assert.Equal(1, searchCount);
        Assert.Equal(1, kbdCount);
    }

    [Fact]
    public void Hook_ToggleCtrlMajVerrMaj_ResteActifQuandAutresRaccourcisChangent()
    {
        var api = CreateApi();
        var host = new TestWindowsTypingHost
        {
            ShortcutCharacterSearchVk = VK_W,
            ShortcutCharacterSearchModifiers = ShortcutModifiers.Shift | ShortcutModifiers.Alt,
            ShortcutVirtualKeyboardVk = VK_Q,
            ShortcutVirtualKeyboardModifiers = ShortcutModifiers.Ctrl | ShortcutModifiers.Alt,
        };
        var mapper = new KeyMapper(CreateMinimalLayout(), api, host);
        using var hook = new KeyboardHook(mapper, host);
        var proc = GetProc(hook);

        int toggles = 0;
        mapper.ToggleRequested += () => toggles++;

        Rappel(proc, WM_KEYDOWN, VK_LCONTROL, SC_LCONTROL);
        Rappel(proc, WM_KEYDOWN, VK_LSHIFT, SC_LSHIFT);
        Rappel(proc, WM_KEYDOWN, VK_CAPITAL, SC_CAPS);
        Rappel(proc, WM_KEYUP, VK_CAPITAL, SC_CAPS);
        Rappel(proc, WM_KEYUP, VK_LSHIFT, SC_LSHIFT);
        Rappel(proc, WM_KEYUP, VK_LCONTROL, SC_LCONTROL);

        Assert.Equal(1, toggles);
    }
}
