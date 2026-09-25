using System.Reflection;
using System.Runtime.InteropServices;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Audit du 25/09, M-04 et M-05 : le rappel du hook joué pour de vrai (délégué privé _proc, lu
/// par réflexion comme le fait le banc moteur), sur une KBDLLHOOKSTRUCT en mémoire non managée.
/// Aucun hook n'est installé ; CallNextHookEx, appelé pour les touches laissées passer, rend 0
/// hors d'une chaîne de hooks.
///
/// M-04 met l'appui et Ctrl+Maj avant MatchesShortcutKey. Ces témoins fixent ce que les deux
/// raccourcis font et dans quels cas : appui seulement, Ctrl+Maj sans Alt, hors saisie sécurisée,
/// touche lue par sa position AZERTY Global. M-05 lit la structure sans Marshal.PtrToStructure :
/// les champs lus (touche, scancode, drapeaux, marqueur d'injection) décident ici de tout.
/// </summary>
public sealed class HookCallbackTests : IDisposable
{
    private const uint WM_KEYDOWN = 0x100, WM_KEYUP = 0x101;
    private const uint VK_LSHIFT = 0xA0, VK_LCONTROL = 0xA2, VK_LMENU = 0xA4;
    private const uint SC_LSHIFT = 0x2A, SC_LCONTROL = 0x1D, SC_LMENU = 0x38;
    private const uint VK_Q = 0x51, VK_W = 0x57, VK_Z = 0x5A;
    private const uint SC_W = 0x2C, SC_Q = 0x1E, SC_Z = 0x11; // positions AZERTY de W, Q et Z

    private readonly IntPtr _lParam = Marshal.AllocHGlobal(Marshal.SizeOf<Win32.KBDLLHOOKSTRUCT>());
    private readonly MockWin32Api _api = new() { ScriptedProcessName = "notepad.exe" };
    private readonly TestWindowsTypingHost _host = new() { ShortcutCharacterSearchVk = VK_W, ShortcutVirtualKeyboardVk = VK_Q };
    private readonly KeyMapper _mapper;
    private readonly KeyboardHook _hook;
    private readonly Win32.LowLevelKeyboardProc _proc;
    private readonly List<string> _evenements = new();

    public HookCallbackTests()
    {
        var layout = new Layout();
        layout.Keys[SC_W] = new KeyDefinition { Position = "B01", Scancode = SC_W, Base = "w", Shift = "W" };
        layout.Keys[SC_Q] = new KeyDefinition { Position = "C01", Scancode = SC_Q, Base = "q", Shift = "Q" };
        layout.Keys[SC_Z] = new KeyDefinition { Position = "D02", Scancode = SC_Z, Base = "z", Shift = "Z" };
        // Modificateurs physiquement tenus : la resynchronisation ne les efface pas.
        foreach (var vk in new[] { VK_LSHIFT, VK_LCONTROL, VK_LMENU })
            _api.AsyncKeyStateScript[(int)vk] = unchecked((short)0x8000);
        _mapper = new KeyMapper(layout, _api, _host);
        _hook = new KeyboardHook(_mapper, _host);
        _hook.SearchRequested += () => _evenements.Add("recherche");
        _hook.VirtualKeyboardRequested += () => _evenements.Add("clavier");
        _hook.RawKeyDown += scan => _evenements.Add($"appui {scan:X2}");
        _proc = (Win32.LowLevelKeyboardProc)typeof(KeyboardHook)
            .GetField("_proc", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(_hook)!;
    }

    public void Dispose()
    {
        _hook.Dispose();
        Marshal.FreeHGlobal(_lParam);
    }

    private long Rappel(uint vk, uint scan, uint msg, uint flags = 0, IntPtr extra = default)
    {
        Marshal.StructureToPtr(new Win32.KBDLLHOOKSTRUCT { vkCode = vk, scanCode = scan, flags = flags, dwExtraInfo = extra },
            _lParam, false);
        return _proc(0, (IntPtr)msg, _lParam).ToInt64();
    }

    private void CtrlMaj()
    {
        Rappel(VK_LCONTROL, SC_LCONTROL, WM_KEYDOWN);
        Rappel(VK_LSHIFT, SC_LSHIFT, WM_KEYDOWN);
        _evenements.Clear();
    }

    [Fact]
    public void CtrlMajW_OuvreLaRecherche_EtBloqueLaTouche()
    {
        CtrlMaj();
        Assert.Equal(1, Rappel(VK_W, SC_W, WM_KEYDOWN));
        Assert.Equal(new[] { "recherche" }, _evenements);
    }

    [Fact]
    public void CtrlMajQ_OuvreLeClavierVirtuel_EtBloqueLaTouche()
    {
        CtrlMaj();
        Assert.Equal(1, Rappel(VK_Q, SC_Q, WM_KEYDOWN));
        Assert.Equal(new[] { "clavier" }, _evenements);
    }

    [Fact]
    public void UnRelachementNOuvreRien()
    {
        CtrlMaj();
        Rappel(VK_W, SC_W, WM_KEYUP);
        Rappel(VK_Q, SC_Q, WM_KEYUP);
        Assert.Empty(_evenements);
    }

    [Fact]
    public void SansCtrlMaj_LaToucheSuitLeCheminOrdinaire()
    {
        // « w » sur la touche W : laissé passer tel quel, et annoncé au clavier virtuel.
        Assert.Equal(0, Rappel(VK_W, SC_W, WM_KEYDOWN));
        Assert.Equal(new[] { "appui 2C" }, _evenements);
    }

    [Fact]
    public void AvecAlt_AucunRaccourci()
    {
        Rappel(VK_LMENU, SC_LMENU, WM_KEYDOWN);
        CtrlMaj();
        Rappel(VK_W, SC_W, WM_KEYDOWN);
        Rappel(VK_Q, SC_Q, WM_KEYDOWN);
        Assert.DoesNotContain("recherche", _evenements);
        Assert.DoesNotContain("clavier", _evenements);
    }

    [Fact]
    public void LaToucheEstLueParSaPositionAzertyGlobal()
    {
        CtrlMaj();
        Rappel(VK_W, SC_Z, WM_KEYDOWN); // code virtuel W, mais position de Z
        Assert.DoesNotContain("recherche", _evenements);
        Rappel(VK_Z, SC_W, WM_KEYDOWN); // position de W, quel que soit le code virtuel
        Assert.Contains("recherche", _evenements);
    }

    [Fact]
    public void EnSaisieSecurisee_AucunRaccourci()
    {
        _api.ScriptedSecureInput = true;
        using var monitor = new ForegroundMonitor(_api, IntPtr.Zero, _host);
        _mapper.SetForegroundMonitor(monitor);
        Assert.True(_mapper.AdvancedFeaturesSuppressed);
        CtrlMaj();
        Rappel(VK_W, SC_W, WM_KEYDOWN);
        Rappel(VK_Q, SC_Q, WM_KEYDOWN);
        Assert.DoesNotContain("recherche", _evenements);
        Assert.DoesNotContain("clavier", _evenements);
    }

    [Fact]
    public void NosInjectionsEtCellesDUnTiersNeSontPasTraitees()
    {
        Assert.Equal(0, Rappel(VK_W, SC_W, WM_KEYDOWN, extra: KeyboardHook.INJECTED_FLAG));
        Assert.Equal(0, Rappel(VK_W, SC_W, WM_KEYDOWN, flags: 0x10)); // LLKHF_INJECTED, sans notre marqueur
        Assert.Empty(_evenements);
        Rappel(VK_W, SC_W, WM_KEYDOWN);
        Assert.Equal(new[] { "appui 2C" }, _evenements);
    }
}
