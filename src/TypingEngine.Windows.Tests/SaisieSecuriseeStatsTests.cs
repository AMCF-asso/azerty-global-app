using System.Reflection;
using System.Runtime.InteropServices;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Audit 1.3.0, consigne 6 du lot tolérance (choix d'Antoine du 21/10) : un texte tapé dans
/// un champ mot de passe ne laisse rien dans les statistiques. L'émission passe par
/// <see cref="IWindowsTypingHost.RecordEmittedText"/> (KeyMapper, fin de l'émission), que
/// l'hôte réel verse dans UsageStats : le témoin est l'hôte, qui ne doit rien recevoir. Le
/// rappel du hook est joué pour de vrai, comme dans <see cref="HookCallbackTests"/>.
/// </summary>
public sealed class SaisieSecuriseeStatsTests : IDisposable
{
    private const uint WM_KEYDOWN = 0x100, WM_KEYUP = 0x101;
    private const uint VK_E = 0x45, SC_E = 0x12;

    private readonly IntPtr _lParam = Marshal.AllocHGlobal(Marshal.SizeOf<Win32.KBDLLHOOKSTRUCT>());
    private readonly MockWin32Api _api = new() { ScriptedProcessName = "notepad.exe" };
    private readonly TestWindowsTypingHost _host = new();
    private readonly KeyMapper _mapper;
    private readonly KeyboardHook _hook;
    private readonly Win32.LowLevelKeyboardProc _proc;

    public SaisieSecuriseeStatsTests()
    {
        var layout = new Layout();
        // Une touche que Windows ne donne pas seule : la disposition doit l'émettre.
        layout.Keys[SC_E] = new KeyDefinition { Position = "D03", Scancode = SC_E, Base = "ê", Shift = "Ê" };
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

    private void Frappe(uint vk, uint scan)
    {
        foreach (uint msg in new[] { WM_KEYDOWN, WM_KEYUP })
        {
            Marshal.StructureToPtr(new Win32.KBDLLHOOKSTRUCT { vkCode = vk, scanCode = scan }, _lParam, false);
            _proc(0, (IntPtr)msg, _lParam);
        }
    }

    [Fact]
    public void Texte_tape_dans_un_champ_mot_de_passe_rien_dans_les_statistiques()
    {
        _api.ScriptedSecureInput = true;
        using var monitor = new ForegroundMonitor(_api, IntPtr.Zero, _host);
        _mapper.SetForegroundMonitor(monitor);
        Assert.True(monitor.IsSecureInput);

        Frappe(VK_E, SC_E);
        Frappe(VK_E, SC_E);

        Assert.Empty(_host.EmittedTexts);
    }

    /// <summary>Témoin positif : hors champ sécurisé, la même frappe est comptée.</summary>
    [Fact]
    public void Hors_champ_securise_la_frappe_est_comptee()
    {
        using var monitor = new ForegroundMonitor(_api, IntPtr.Zero, _host);
        _mapper.SetForegroundMonitor(monitor);
        Assert.False(monitor.IsSecureInput);

        Frappe(VK_E, SC_E);

        Assert.Equal(new[] { "ê" }, _host.EmittedTexts);
    }
}
