using System.Reflection;
using System.Runtime.InteropServices;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Audit de l'environnement Windows (docs/audit-environnement-windows.md), constat EW-1.
///
/// Un changement de bureau (Win+L, Ctrl+Alt+Suppr, invite UAC sur le bureau sécurisé) fait
/// relâcher les touches hors de portée du hook WH_KEYBOARD_LL, qui ne vit que sur le bureau de
/// l'utilisateur. Le moteur garde alors Windows ou Alt « tenus ». Ces tests jouent le rappel du
/// hook pour de vrai (délégué privé _proc, même banc que HookCallbackTests) avec un relâchement
/// manquant : GetAsyncKeyState dit la touche relâchée, le suivi interne la croit tenue.
///
/// Ce qu'ils prouvent : la réaction du code à un relâchement perdu. La perte elle-même, sur un
/// vrai poste verrouillé, s'établit par l'essai à la main E1 du document d'audit.
/// Comportement attendu : un raccourci de l'application ne se fie pas à un modificateur que
/// l'état physique dément.
/// </summary>
public sealed class EnvironnementWindowsTests : IDisposable
{
    private const uint WM_KEYDOWN = 0x100, WM_KEYUP = 0x101;
    private const uint LLKHF_EXTENDED = 0x01;
    private const uint VK_LSHIFT = 0xA0, VK_LCONTROL = 0xA2, VK_LMENU = 0xA4, VK_LWIN = 0x5B, VK_CAPITAL = 0x14, VK_L = 0x4C;
    private const uint SC_LSHIFT = 0x2A, SC_LCONTROL = 0x1D, SC_LMENU = 0x38, SC_LWIN = 0x5B, SC_CAPITAL = 0x3A, SC_L = 0x26;
    private const uint VK_W = 0x57, SC_W = 0x2C;

    private readonly IntPtr _lParam = Marshal.AllocHGlobal(Marshal.SizeOf<Win32.KBDLLHOOKSTRUCT>());
    private readonly MockWin32Api _api = new() { ScriptedProcessName = "notepad.exe" };
    private readonly TestWindowsTypingHost _host = new() { ShortcutCharacterSearchVk = VK_W };
    private readonly KeyMapper _mapper;
    private readonly KeyboardHook _hook;
    private readonly Win32.LowLevelKeyboardProc _proc;
    private readonly List<string> _evenements = new();

    public EnvironnementWindowsTests()
    {
        var layout = new Layout();
        layout.Keys[SC_W] = new KeyDefinition { Position = "B01", Scancode = SC_W, Base = "w", Shift = "W" };
        _mapper = new KeyMapper(layout, _api, _host);
        _hook = new KeyboardHook(_mapper, _host);
        _mapper.ToggleRequested += () => _evenements.Add("bascule");
        _hook.SearchRequested += () => _evenements.Add("recherche");
        _proc = (Win32.LowLevelKeyboardProc)typeof(KeyboardHook)
            .GetField("_proc", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(_hook)!;
    }

    public void Dispose()
    {
        _hook.Dispose();
        Marshal.FreeHGlobal(_lParam);
    }

    private long Rappel(uint vk, uint scan, uint msg, uint flags = 0)
    {
        Marshal.StructureToPtr(new Win32.KBDLLHOOKSTRUCT { vkCode = vk, scanCode = scan, flags = flags },
            _lParam, false);
        return _proc(0, (IntPtr)msg, _lParam).ToInt64();
    }

    private void Physique(uint vk, bool enfoncee) =>
        _api.AsyncKeyStateScript[(int)vk] = enfoncee ? unchecked((short)0x8000) : (short)0;

    /// <summary>Win+L : le hook voit Windows et L s'enfoncer ; les relâchements partent sur
    /// l'écran de verrouillage. Au retour, rien n'est physiquement tenu.</summary>
    private void VerrouillageParWinL()
    {
        Physique(VK_LWIN, true);
        Physique(VK_L, true);
        Rappel(VK_LWIN, SC_LWIN, WM_KEYDOWN, LLKHF_EXTENDED);
        Rappel(VK_L, SC_L, WM_KEYDOWN);
        Physique(VK_LWIN, false);
        Physique(VK_L, false);
    }

    /// <summary>Ctrl+Alt+Suppr : Suppr est la séquence sécurisée, jamais livrée au hook ;
    /// Ctrl et Alt sont vus s'enfoncer, leurs relâchements partent sur le bureau sécurisé.</summary>
    private void VerrouillageParCtrlAltSuppr()
    {
        Physique(VK_LCONTROL, true);
        Physique(VK_LMENU, true);
        Rappel(VK_LCONTROL, SC_LCONTROL, WM_KEYDOWN);
        Rappel(VK_LMENU, SC_LMENU, WM_KEYDOWN);
        Physique(VK_LCONTROL, false);
        Physique(VK_LMENU, false);
    }

    /// <summary>Ctrl+Maj+Verr. maj, modificateurs réellement tenus pendant le geste.</summary>
    private long CtrlMajVerrMaj()
    {
        Physique(VK_LCONTROL, true);
        Physique(VK_LSHIFT, true);
        Rappel(VK_LCONTROL, SC_LCONTROL, WM_KEYDOWN);
        Rappel(VK_LSHIFT, SC_LSHIFT, WM_KEYDOWN);
        _evenements.Clear();
        return Rappel(VK_CAPITAL, SC_CAPITAL, WM_KEYDOWN);
    }

    // Constat EW-1 (P1 : remappage coupé par l'utilisateur)
    [Fact]
    public void EW1_RemappageCoupe_ApresWinL_CtrlMajVerrMaj_RallumeToujours()
    {
        _hook.Enabled = false;
        VerrouillageParWinL();

        long retour = CtrlMajVerrMaj();

        Assert.Equal(new[] { "bascule" }, _evenements);
        Assert.Equal(1, retour);
    }

    // Constat EW-1 (P3 : pause volontaire, raccourcis permis)
    [Fact]
    public void EW1_Pause_ApresWinL_CtrlMajVerrMaj_ReprendToujours()
    {
        _hook.Enabled = false;
        _hook.PassThroughAll = true;
        _hook.ShortcutsWhilePassThrough = true;
        VerrouillageParWinL();

        long retour = CtrlMajVerrMaj();

        Assert.Equal(new[] { "bascule" }, _evenements);
        Assert.Equal(1, retour);
    }

    // Constat EW-1 (P1, Ctrl+Alt+Suppr : Alt resté tenu)
    [Fact]
    public void EW1_RemappageCoupe_ApresCtrlAltSuppr_CtrlMajVerrMaj_RallumeToujours()
    {
        _hook.Enabled = false;
        VerrouillageParCtrlAltSuppr();

        long retour = CtrlMajVerrMaj();

        Assert.Equal(new[] { "bascule" }, _evenements);
        Assert.Equal(1, retour);
    }

    // Constat EW-1 (P0 : remappage actif, premier raccourci après le déverrouillage)
    [Fact]
    public void EW1_RemappageActif_ApresWinL_LePremierCtrlMajVerrMaj_BasculeEtNeTouchePasAuVerrMaj()
    {
        VerrouillageParWinL();

        long retour = CtrlMajVerrMaj();

        Assert.Equal(new[] { "bascule" }, _evenements);
        Assert.Equal(1, retour);
    }

    // Constat EW-1 (P0 : la recherche de caractère, même cause)
    [Fact]
    public void EW1_RemappageActif_ApresWinL_LePremierCtrlMajW_OuvreLaRecherche()
    {
        VerrouillageParWinL();
        Physique(VK_LCONTROL, true);
        Physique(VK_LSHIFT, true);
        Rappel(VK_LCONTROL, SC_LCONTROL, WM_KEYDOWN);
        Rappel(VK_LSHIFT, SC_LSHIFT, WM_KEYDOWN);
        _evenements.Clear();

        long retour = Rappel(VK_W, SC_W, WM_KEYDOWN);

        Assert.Equal(new[] { "recherche" }, _evenements);
        Assert.Equal(1, retour);
    }

    // Témoin : relâchement vu (pas de changement de bureau), le raccourci marche. Vert sur 396e449.
    [Fact]
    public void EW1_Temoin_RemappageCoupe_WinRelacheVu_CtrlMajVerrMaj_Rallume()
    {
        _hook.Enabled = false;
        Physique(VK_LWIN, true);
        Rappel(VK_LWIN, SC_LWIN, WM_KEYDOWN, LLKHF_EXTENDED);
        Rappel(VK_LWIN, SC_LWIN, WM_KEYUP, LLKHF_EXTENDED);
        Physique(VK_LWIN, false);

        long retour = CtrlMajVerrMaj();

        Assert.Equal(new[] { "bascule" }, _evenements);
        Assert.Equal(1, retour);
    }
}

/// <summary>
/// Audit de l'environnement Windows, constat EW-2 : changement de disposition en pleine frappe,
/// dans la même fenêtre (Alt+Maj, Ctrl+Maj, barre de langue).
///
/// Le HKL (disposition Windows de la fenêtre au premier plan) n'est relu que par
/// ForegroundMonitor.Recompute, déclenché par un changement de fenêtre ou de focus ;
/// IsSnapshotStale ne compare pas le HKL. Après une bascule FR → US dans la même fenêtre,
/// le laisser-passer (KeyMapper.CanPassThrough) raisonne encore sur la disposition FR et
/// laisse la disposition US réelle écrire « q » au lieu de « a », « 1 » au lieu de « & ».
///
/// Ce qu'ils prouvent : la réaction du code à un HKL qui change sans changement de fenêtre.
/// Que Windows ne déclenche alors aucun recalcul (WM_INPUTLANGCHANGE non reçu par la fenêtre
/// cachée de l'app) s'établit par l'essai à la main E5.
/// </summary>
public sealed class EnvironnementWindowsDispositionTests
{
    private const uint SC_E01 = 0x02, SC_D01 = 0x10;
    private const ushort VK_1 = 0x31, VK_7 = 0x37, VK_A = 0x41, VK_Q = 0x51;
    private static readonly IntPtr HklFr = (IntPtr)0x040C040C;
    private static readonly IntPtr HklUs = (IntPtr)0x04090409;

    private readonly MockWin32Api _api = new()
    {
        ScriptedProcessName = "notepad.exe",
        ScriptedFullPath = @"C:\Windows\notepad.exe",
        ScriptedModules = new[] { "kernel32.dll", "user32.dll" },
        ForegroundWindow = (IntPtr)0x1234,
    };

    private (KeyMapper mapper, ForegroundMonitor premierPlan) MonterEnFrancaisPuisBasculerEnUs()
    {
        _api.MapVirtualKeyScript[(SC_D01, 1, HklFr)] = VK_A;
        _api.VkKeyScanScript[('a', HklFr)] = (short)VK_A;
        _api.MapVirtualKeyScript[(SC_E01, 1, HklFr)] = VK_1;
        _api.VkKeyScanScript[('&', HklFr)] = (short)VK_1;
        _api.MapVirtualKeyScript[(SC_D01, 1, HklUs)] = VK_Q;
        _api.VkKeyScanScript[('a', HklUs)] = (short)VK_A;
        _api.MapVirtualKeyScript[(SC_E01, 1, HklUs)] = VK_1;
        _api.VkKeyScanScript[('&', HklUs)] = (short)((1 << 8) | VK_7);

        _api.CurrentHkl = HklFr;
        var premierPlan = new ForegroundMonitor(_api, IntPtr.Zero);
        var layout = new Layout();
        layout.Keys[SC_D01] = new KeyDefinition { Position = "D01", Scancode = SC_D01, Base = "a", Shift = "A", Caps = "A", CapsShift = "a" };
        layout.Keys[SC_E01] = new KeyDefinition { Position = "E01", Scancode = SC_E01, Base = "&", Shift = "1" };
        var mapper = new KeyMapper(layout, _api);
        mapper.SetForegroundMonitor(premierPlan);

        // Bascule de disposition dans la même fenêtre : aucun évènement de premier plan ni de focus.
        _api.CurrentHkl = HklUs;
        _api.SendInputCalls.Clear();
        return (mapper, premierPlan);
    }

    private void AssertEmissionUnicode(char attendu)
    {
        Assert.Single(_api.SendInputCalls);
        Assert.Equal(attendu, (char)_api.SendInputCalls[0][0].u.ki.wScan);
    }

    // Constat EW-2 : lettre, D01 doit donner « a », la disposition US réelle écrirait « q ».
    [Fact]
    public void EW2_BasculeFrVersUsMemeFenetre_D01_EcritA()
    {
        var (mapper, premierPlan) = MonterEnFrancaisPuisBasculerEnUs();
        using (premierPlan)
        {
            bool prise = mapper.ProcessKey(VK_Q, SC_D01, 0, true);

            Assert.True(prise);
            AssertEmissionUnicode('a');
        }
    }

    // Constat EW-2 : rangée des chiffres, E01 doit donner « & », la disposition US écrirait « 1 ».
    [Fact]
    public void EW2_BasculeFrVersUsMemeFenetre_E01_EcritEsperluette()
    {
        var (mapper, premierPlan) = MonterEnFrancaisPuisBasculerEnUs();
        using (premierPlan)
        {
            bool prise = mapper.ProcessKey(VK_1, SC_E01, 0, true);

            Assert.True(prise);
            AssertEmissionUnicode('&');
        }
    }

    // Témoin : même bascule suivie d'un recalcul (ce que ferait un changement de focus). Vert sur 396e449.
    [Fact]
    public void EW2_Temoin_BasculePuisRecalcul_D01_EcritA()
    {
        var (mapper, premierPlan) = MonterEnFrancaisPuisBasculerEnUs();
        using (premierPlan)
        {
            premierPlan.Recompute();

            bool prise = mapper.ProcessKey(VK_Q, SC_D01, 0, true);

            Assert.True(prise);
            AssertEmissionUnicode('a');
        }
    }
}
