using System.Reflection;
using System.Runtime.InteropServices;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Audit des chemins d'erreur (rapport-D K115 T1, T2, T3) : une exception dans le rappel du hook
/// (abonné défaillant) ne doit ni doubler un caractère déjà émis, ni laisser une touche coincée,
/// ni laisser passer Verr.Maj. du basculement. Même mécanique que HookCallbackTests : délégué
/// privé _proc, KBDLLHOOKSTRUCT en mémoire non managée ; aucun hook réel n'est installé.
/// </summary>
public sealed class CheminsErreurHookTests : IDisposable
{
    private const uint WM_KEYDOWN = 0x100, WM_KEYUP = 0x101;
    private const uint VK_LSHIFT = 0xA0, VK_LCONTROL = 0xA2, VK_CAPITAL = 0x14;
    private const uint SC_LSHIFT = 0x2A, SC_LCONTROL = 0x1D, SC_CAPITAL = 0x3A;
    private const uint VK_2 = 0x32, SC_2 = 0x03; // touche « é » / « 2 »

    private readonly IntPtr _lParam = Marshal.AllocHGlobal(Marshal.SizeOf<Win32.KBDLLHOOKSTRUCT>());
    private readonly MockWin32Api _api = new() { ScriptedProcessName = "notepad.exe" };
    private readonly TestWindowsTypingHost _host = new();
    private readonly KeyMapper _mapper;
    private readonly KeyboardHook _hook;
    private readonly Win32.LowLevelKeyboardProc _proc;

    public CheminsErreurHookTests()
    {
        var layout = new Layout();
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

    /// <summary>
    /// T1 — l'abonné de KeyDecided lève APRÈS l'émission. Le caractère est déjà parti par SendInput :
    /// la touche d'origine doit rester bloquée (retour 1), sinon la cible reçoit « é » puis le caractère natif.
    /// </summary>
    // rapport-D K115 T1
    [Fact]
    [Trait("Audit", "chemins-erreur")]
    public void ExceptionApresEmission_LaToucheOriginaleResteBloquee()
    {
        _mapper.KeyDecided += _ => throw new InvalidOperationException("abonné défaillant");

        long retour = Rappel(VK_2, SC_2, WM_KEYDOWN);

        Assert.NotEmpty(_api.SendInputCalls); // le caractère a bien été émis
        Assert.Equal(1, retour);              // et la touche physique ne doit pas le doubler
    }

    /// <summary>
    /// T2 — l'abonné de StateChanged lève AVANT toute émission (resynchronisation de Verr.Maj.).
    /// L'appui et le relâchement de la même touche doivent suivre le même chemin.
    /// </summary>
    // rapport-D K115 T2
    [Fact]
    [Trait("Audit", "chemins-erreur")]
    public void ExceptionAvantEmission_AppuiEtRelachementSuiventLeMemeChemin()
    {
        _api.KeyStateScript[(int)VK_CAPITAL] = 1; // Verr.Maj. allumé côté système, état interne éteint
        _mapper.StateChanged += () => throw new InvalidOperationException("abonné défaillant");

        long appui = Rappel(VK_2, SC_2, WM_KEYDOWN);
        long relachement = Rappel(VK_2, SC_2, WM_KEYUP);

        Assert.Equal(appui, relachement);
    }

    /// <summary>
    /// T3 — Ctrl+Maj+Verr.Maj. : si l'abonné de ToggleRequested lève, Verr.Maj. ne doit pas passer.
    /// </summary>
    // rapport-D K115 T3
    [Fact]
    [Trait("Audit", "chemins-erreur")]
    public void ExceptionDansLeBasculeOnOff_VerrMajNEstPasLaisseePasser()
    {
        Rappel(VK_LCONTROL, SC_LCONTROL, WM_KEYDOWN);
        Rappel(VK_LSHIFT, SC_LSHIFT, WM_KEYDOWN);
        _mapper.ToggleRequested += () => throw new InvalidOperationException("abonné défaillant");

        long retour = Rappel(VK_CAPITAL, SC_CAPITAL, WM_KEYDOWN);

        Assert.Equal(1, retour);
    }
}
