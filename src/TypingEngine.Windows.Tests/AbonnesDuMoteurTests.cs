using System.Reflection;
using System.Runtime.InteropServices;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// CE-T1, prévention (audit des chemins d'erreur, 17 novembre 2026) : les tests d'audit CE-01 et
/// CE-02 visent StateChanged, KeyDecided, ToggleRequested et ForegroundChanged. Les autres
/// événements du rappel du hook (recherche, clavier virtuel, appui annoncé) avaient le même défaut :
/// un abonné qui lève faisait passer la touche d'origine. Chaque abonné est maintenant prévenu à
/// part (SubscriberGuard) ; ces témoins, rouges sur c06dec1, fixent les deux promesses : la touche
/// garde la décision du moteur, et les abonnés suivants sont quand même prévenus.
/// </summary>
public sealed class AbonnesDuMoteurTests : IDisposable
{
    private const uint WM_KEYDOWN = 0x100;
    private const uint VK_LSHIFT = 0xA0, VK_LCONTROL = 0xA2;
    private const uint SC_LSHIFT = 0x2A, SC_LCONTROL = 0x1D;
    private const uint VK_Q = 0x51, VK_W = 0x57, VK_2 = 0x32;
    private const uint SC_W = 0x2C, SC_Q = 0x1E, SC_2 = 0x03;

    private readonly IntPtr _lParam = Marshal.AllocHGlobal(Marshal.SizeOf<Win32.KBDLLHOOKSTRUCT>());
    private readonly MockWin32Api _api = new() { ScriptedProcessName = "notepad.exe" };
    private readonly TestWindowsTypingHost _host = new() { ShortcutCharacterSearchVk = VK_W, ShortcutVirtualKeyboardVk = VK_Q };
    private readonly KeyMapper _mapper;
    private readonly KeyboardHook _hook;
    private readonly Win32.LowLevelKeyboardProc _proc;
    private readonly List<string> _prevenus = new();

    public AbonnesDuMoteurTests()
    {
        var layout = new Layout();
        layout.Keys[SC_W] = new KeyDefinition { Position = "B01", Scancode = SC_W, Base = "w", Shift = "W" };
        layout.Keys[SC_Q] = new KeyDefinition { Position = "C01", Scancode = SC_Q, Base = "q", Shift = "Q" };
        layout.Keys[SC_2] = new KeyDefinition { Position = "E02", Scancode = SC_2, Base = "é", Shift = "2" };
        foreach (var vk in new[] { VK_LSHIFT, VK_LCONTROL })
            _api.AsyncKeyStateScript[(int)vk] = unchecked((short)0x8000);
        _mapper = new KeyMapper(layout, _api, _host);
        _hook = new KeyboardHook(_mapper, _host);
        _proc = (Win32.LowLevelKeyboardProc)typeof(KeyboardHook)
            .GetField("_proc", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(_hook)!;
    }

    public void Dispose()
    {
        _hook.Dispose();
        Marshal.FreeHGlobal(_lParam);
    }

    private long Rappel(uint vk, uint scan, uint msg)
    {
        Marshal.StructureToPtr(new Win32.KBDLLHOOKSTRUCT { vkCode = vk, scanCode = scan, flags = 0, dwExtraInfo = default },
            _lParam, false);
        return _proc(0, (IntPtr)msg, _lParam).ToInt64();
    }

    private void CtrlMaj()
    {
        Rappel(VK_LCONTROL, SC_LCONTROL, WM_KEYDOWN);
        Rappel(VK_LSHIFT, SC_LSHIFT, WM_KEYDOWN);
    }

    private static void Leve() => throw new InvalidOperationException("abonné défaillant");

    [Fact]
    public void Recherche_AbonneQuiLeve_LaToucheResteBloqueeEtLeSuivantEstPrevenu()
    {
        _hook.SearchRequested += Leve;
        _hook.SearchRequested += () => _prevenus.Add("recherche");
        CtrlMaj();

        Assert.Equal(1, Rappel(VK_W, SC_W, WM_KEYDOWN));
        Assert.Equal(new[] { "recherche" }, _prevenus);
    }

    [Fact]
    public void ClavierVirtuel_AbonneQuiLeve_LaToucheResteBloqueeEtLeSuivantEstPrevenu()
    {
        _hook.VirtualKeyboardRequested += Leve;
        _hook.VirtualKeyboardRequested += () => _prevenus.Add("clavier");
        CtrlMaj();

        Assert.Equal(1, Rappel(VK_Q, SC_Q, WM_KEYDOWN));
        Assert.Equal(new[] { "clavier" }, _prevenus);
    }

    [Fact]
    public void AppuiAnnonce_AbonneQuiLeve_LaToucheEstQuandMemeRemappee()
    {
        _hook.RawKeyDown += _ => Leve();
        _hook.RawKeyDownDetailed += (_, _) => _prevenus.Add("appui détaillé");

        Assert.Equal(1, Rappel(VK_2, SC_2, WM_KEYDOWN));
        Assert.NotEmpty(_api.SendInputCalls); // « é » émis, la touche « 2 » bloquée
        Assert.Equal(new[] { "appui détaillé" }, _prevenus);
    }

    [Fact]
    public void ChangementDEtat_PremierAbonneQuiLeve_LeSecondEstPrevenu()
    {
        _api.KeyStateScript[0x14] = 1; // Verr. Maj. allumé côté système : StateChanged au prochain appui
        _mapper.StateChanged += Leve;
        _mapper.StateChanged += () => _prevenus.Add("état");

        Rappel(VK_2, SC_2, WM_KEYDOWN);

        Assert.Contains("état", _prevenus);
    }

    [Fact]
    public void PremierPlan_PremierAbonneQuiLeve_LeSecondEstPrevenu()
    {
        var api = new MockWin32Api { ScriptedProcessName = "notepad.exe", ScriptedFullPath = @"C:\Windows\notepad.exe" };
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero, new TestWindowsTypingHost());
        monitor.ForegroundChanged += Leve;
        monitor.ForegroundChanged += () => _prevenus.Add("premier plan");

        monitor.Recompute();

        Assert.Equal(new[] { "premier plan" }, _prevenus);
    }
}
