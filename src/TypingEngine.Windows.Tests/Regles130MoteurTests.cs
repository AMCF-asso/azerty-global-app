using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Un test par règle du lot « moteur » de docs/machines-a-etats.md (app 1.3.0) : hook, rappel,
/// suivi du premier plan, sonde de saisie sécurisée. Le nom de chaque méthode commence par l'id
/// de la règle. Aucun hook Win32 n'est posé ici : le rappel est joué par réflexion, sur une
/// KBDLLHOOKSTRUCT en mémoire non managée (même montage que HookCallbackTests). Les règles qui
/// demandent un vrai bureau d'entrée sont dans Regles130EntreeReelleTests.
/// </summary>
internal sealed class BancRegles130Hook : IDisposable
{
    internal const uint WM_KEYDOWN = 0x100, WM_KEYUP = 0x101;
    internal const uint VK_LSHIFT = 0xA0, VK_LCONTROL = 0xA2;
    internal const uint SC_LSHIFT = 0x2A, SC_LCONTROL = 0x1D;
    internal const uint VK_A = 0x41, VK_Q = 0x51, VK_W = 0x57;
    internal const uint SC_E = 0x10;   // touche remappée sur « ê » : une vraie émission
    internal const uint SC_W = 0x2C, SC_Q = 0x1E;

    private readonly IntPtr _lParam = Marshal.AllocHGlobal(Marshal.SizeOf<Win32.KBDLLHOOKSTRUCT>());
    internal readonly MockWin32Api Api = new() { ScriptedProcessName = "notepad.exe" };
    internal readonly TestWindowsTypingHost Host = new() { ShortcutCharacterSearchVk = VK_W, ShortcutVirtualKeyboardVk = VK_Q };
    internal readonly KeyMapper Mapper;
    internal readonly KeyboardHook Hook;
    internal readonly List<string> Evenements = new();
    private readonly Win32.LowLevelKeyboardProc _proc;

    internal BancRegles130Hook()
    {
        var layout = new Layout();
        layout.Keys[SC_E] = new KeyDefinition { Position = "D01", Scancode = SC_E, Base = "ê", Shift = "Ê" };
        layout.Keys[SC_W] = new KeyDefinition { Position = "B01", Scancode = SC_W, Base = "w", Shift = "W" };
        layout.Keys[SC_Q] = new KeyDefinition { Position = "C01", Scancode = SC_Q, Base = "q", Shift = "Q" };
        // Modificateurs physiquement tenus : la resynchronisation ne les efface pas.
        foreach (var vk in new[] { VK_LSHIFT, VK_LCONTROL })
            Api.AsyncKeyStateScript[(int)vk] = unchecked((short)0x8000);
        Mapper = new KeyMapper(layout, Api, Host);
        Hook = new KeyboardHook(Mapper, Host);
        Hook.SearchRequested += () => Evenements.Add("recherche");
        Hook.VirtualKeyboardRequested += () => Evenements.Add("clavier");
        Hook.RawKeyDown += scan => Evenements.Add($"appui {scan:X2}");
        _proc = (Win32.LowLevelKeyboardProc)typeof(KeyboardHook)
            .GetField("_proc", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Hook)!;
    }

    public void Dispose()
    {
        Hook.Dispose();
        Marshal.FreeHGlobal(_lParam);
    }

    internal long Rappel(uint vk, uint scan, uint msg)
    {
        Marshal.StructureToPtr(new Win32.KBDLLHOOKSTRUCT { vkCode = vk, scanCode = scan, flags = 0, dwExtraInfo = default },
            _lParam, false);
        return _proc(0, (IntPtr)msg, _lParam).ToInt64();
    }

    internal void CtrlMaj()
    {
        Rappel(VK_LCONTROL, SC_LCONTROL, WM_KEYDOWN);
        Rappel(VK_LSHIFT, SC_LSHIFT, WM_KEYDOWN);
        Evenements.Clear();
    }
}

/// <summary>
/// Une IWin32Api qui délègue au mock et laisse le test intervenir pendant une lecture : un
/// rappel exécuté une fois à la prochaine lecture du premier plan, ou une exception levée une
/// fois par l'inspection du processus. Elle ne renseigne pas GetWindowThreadId (membre par
/// défaut, rend 0) : GetEmitContext garde alors le HKL du snapshot capturé.
/// </summary>
internal sealed class ApiObservableRegles130 : IWin32Api
{
    internal readonly MockWin32Api Inner;
    internal Action? AvantLectureDuPremierPlan;
    internal bool LeverALaProchaineInspection;

    internal ApiObservableRegles130(MockWin32Api inner) => Inner = inner;

    public IntPtr GetForegroundWindow()
    {
        var action = AvantLectureDuPremierPlan;
        AvantLectureDuPremierPlan = null;   // une seule fois : Recompute relit lui-même le premier plan
        action?.Invoke();
        return Inner.GetForegroundWindow();
    }

    public bool TryGetWindowProcess(IntPtr window, out string? processName, out string? fullPath, out IntPtr hkl, out uint pid)
    {
        if (LeverALaProchaineInspection)
        {
            LeverALaProchaineInspection = false;
            throw new InvalidOperationException("inspection du processus : échec scripté");
        }
        return Inner.TryGetWindowProcess(window, out processName, out fullPath, out hkl, out pid);
    }

    public short VkKeyScanExW(char ch, IntPtr hkl) => Inner.VkKeyScanExW(ch, hkl);
    public uint MapVirtualKeyExW(uint code, uint mapType, IntPtr hkl) => Inner.MapVirtualKeyExW(code, mapType, hkl);
    public short GetKeyState(int vk) => Inner.GetKeyState(vk);
    public short GetAsyncKeyState(int vk) => Inner.GetAsyncKeyState(vk);
    public IntPtr GetKeyboardLayout(uint threadId) => Inner.GetKeyboardLayout(threadId);
    public int ToUnicode(uint vk, uint scan, byte[] state, StringBuilder buffer, int capacity, uint flags) =>
        Inner.ToUnicode(vk, scan, state, buffer, capacity, flags);
    public int ToUnicodeEx(uint vk, uint scan, byte[] state, StringBuilder buffer, int capacity, uint flags, IntPtr hkl) =>
        Inner.ToUnicodeEx(vk, scan, state, buffer, capacity, flags, hkl);
    public uint SendInput(Win32.INPUT[] inputs) => Inner.SendInput(inputs);
    public bool TryGetProcessStartTime(uint pid, out long startTimeTicks) => Inner.TryGetProcessStartTime(pid, out startTimeTicks);
    public bool IsWindowPasswordField(IntPtr window) => Inner.IsWindowPasswordField(window);
    public bool TryEnumProcessModules(uint pid, out string[] moduleFileNames) => Inner.TryEnumProcessModules(pid, out moduleFileNames);
    public IntPtr SetWinEventHook(uint eventMin, uint eventMax, Win32.WinEventDelegate cb) => Inner.SetWinEventHook(eventMin, eventMax, cb);
    public bool UnhookWinEvent(IntPtr hook) => Inner.UnhookWinEvent(hook);
}

/// <summary>Hook, rappel et moteur : M1.2-I8, M1.5-I4, M1.6-I5, M1.6-I6, M2.1-I4, M2.3-I3, M2.3-I4.</summary>
public sealed class Regles130MoteurTests
{
    private const uint SC_DECLENCHEUR = 0x2B;   // touche de la couche grecque (Base « dk_greek »)
    private const uint VK_OEM_5 = 0xDC;

    private static uint? DeclencheurEnAttente(KeyMapper mapper)
    {
        var gestionnaire = (MaintainableLayerManager)typeof(KeyMapper)
            .GetField("_maintainableLayers", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(mapper)!;
        return gestionnaire.PendingTriggerScanCode;
    }

    private static KeyMapper MapperAvecCouche(MockWin32Api api, TestWindowsTypingHost host, out ForegroundMonitor monitor)
    {
        var layout = new Layout();
        layout.Keys[SC_DECLENCHEUR] = new KeyDefinition { Position = "B00", Scancode = SC_DECLENCHEUR, Base = "dk_greek", Shift = "dk_greek" };
        var mapper = new KeyMapper(layout, api, host);
        mapper.ApplyMaintainableLayerSettings(true, new[] { "dk_greek" }, 500);
        monitor = new ForegroundMonitor(api, IntPtr.Zero, host);
        mapper.SetForegroundMonitor(monitor);
        return mapper;
    }

    /// <summary>
    /// M1.2-I8 : la relâche du déclencheur est vue du gestionnaire hors suspension ; si une
    /// suspension tombe pendant l'appui, _pressedLayer reste jusqu'à SyncState ou un changement
    /// de processus. Énoncé tel que le document le borne (« garanti hors suspension »), donc vert :
    /// le « toujours » de l'invariant n'est pas tenu pendant une suspension, et le document le dit.
    /// </summary>
    [Fact]
    public void M1_2_I8_RelacheDuDeclencheur_VueHorsSuspension_PuisEffaceeApresSuspension()
    {
        var api = new MockWin32Api { ScriptedProcessName = "notepad.exe" };
        var host = new TestWindowsTypingHost();
        var mapper = MapperAvecCouche(api, host, out var monitor);
        using (monitor)
        {
            // Hors suspension : l'appui arme le déclencheur, la relâche le libère.
            Assert.True(mapper.ProcessKey(VK_OEM_5, SC_DECLENCHEUR, 0, true));
            Assert.Equal(SC_DECLENCHEUR, DeclencheurEnAttente(mapper));
            mapper.ProcessKey(VK_OEM_5, SC_DECLENCHEUR, 0, false);
            Assert.Null(DeclencheurEnAttente(mapper));

            // Pause volontaire pendant l'appui : la relâche n'atteint pas le gestionnaire.
            Assert.True(mapper.ProcessKey(VK_OEM_5, SC_DECLENCHEUR, 0, true));
            Assert.Equal(SC_DECLENCHEUR, DeclencheurEnAttente(mapper));
            mapper.EmissionPaused = true;
            Assert.False(mapper.ProcessKey(VK_OEM_5, SC_DECLENCHEUR, 0, false));
            Assert.Equal(SC_DECLENCHEUR, DeclencheurEnAttente(mapper));   // _pressedLayer reste
            // SyncState (reprise) l'efface.
            mapper.EmissionPaused = false;
            mapper.SyncState();
            Assert.Null(DeclencheurEnAttente(mapper));

            // Suspension imposée (premier plan périmé) pendant l'appui, puis changement de processus.
            Assert.True(mapper.ProcessKey(VK_OEM_5, SC_DECLENCHEUR, 0, true));
            api.ForegroundWindow = (IntPtr)0x7777;
            Assert.False(mapper.ProcessKey(VK_OEM_5, SC_DECLENCHEUR, 0, false));
            Assert.Equal(SC_DECLENCHEUR, DeclencheurEnAttente(mapper));
            api.ScriptedPid = 5678;
            api.ScriptedProcessStartTime = 222;
            api.SimulateForegroundChange("editor.exe", @"C:\editor.exe", api.CurrentHkl);
            mapper.RefreshForegroundContext();
            Assert.Null(DeclencheurEnAttente(mapper));
        }
    }

    private static (bool Absorbee, int Lots, int Lectures, int Restantes) FrappeAvecBasculeApres(int lecturesAvantBascule)
    {
        var api = new MockWin32Api { ScriptedProcessName = "notepad.exe" };
        var host = new TestWindowsTypingHost();
        var layout = new Layout();
        layout.Keys[BancRegles130Hook.SC_E] = new KeyDefinition { Position = "D01", Scancode = BancRegles130Hook.SC_E, Base = "ê", Shift = "Ê" };
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero, host);
        var mapper = new KeyMapper(layout, api, host);
        mapper.SetForegroundMonitor(monitor);
        var fenetre = api.ForegroundWindow;
        if (lecturesAvantBascule >= 0)
        {
            // Les premières lectures voient la fenêtre du snapshot, les suivantes une autre : le
            // premier plan change au milieu de l'appel, sans qu'aucun événement ne soit livré.
            for (int i = 0; i < lecturesAvantBascule; i++) api.ForegroundWindowScript.Enqueue(fenetre);
            api.ForegroundWindow = (IntPtr)0x7777;
        }
        int avant = api.ForegroundWindowReads;
        bool absorbee = mapper.ProcessKey(BancRegles130Hook.VK_A, BancRegles130Hook.SC_E, 0, true);
        return (absorbee, api.SendInputCalls.Count, api.ForegroundWindowReads - avant, api.ForegroundWindowScript.Count);
    }

    /// <summary>
    /// M1.5-I4 : l'état de la suspension est lu de manière cohérente pendant un seul appel. Tenu
    /// depuis 1.3.0 : ProcessKey prend un instantané du premier plan à l'entrée et toutes les gardes
    /// de l'appel le lisent. Un seul appel de ProcessKey, le premier plan change après la k-ième
    /// lecture : la touche doit alors soit être absorbée ET émise, soit laissée passer ET non émise,
    /// jamais perdue ni doublée. Ajusté : témoin couplé à l'ancienne implémentation (il exigeait
    /// au moins 3 lectures par appel ; le correctif en fait une seule, c'est la garantie elle-même).
    /// </summary>
    [Fact]
    public void M1_5_I4_UnSeulAppel_LitLaSuspensionDeManiereCoherente()
    {
        // Témoin 1 : premier plan stable, la touche est absorbée et son caractère émis.
        var stable = FrappeAvecBasculeApres(-1);
        Assert.True(stable.Absorbee);
        Assert.True(stable.Lots >= 1, "montage : aucune émission en régime stable");
        Assert.True(stable.Lectures >= 1, "montage : le premier plan n'est jamais lu");

        // Témoin 2 : premier plan déjà périmé, la touche passe sans rien émettre.
        var perime = FrappeAvecBasculeApres(0);
        Assert.False(perime.Absorbee);
        Assert.Equal(0, perime.Lots);

        // Balayage : le changement tombe après la k-ième lecture, pour k jusqu'à 4 (les lectures
        // que faisait l'ancienne implémentation ; le correctif n'en fait qu'une). L'appel doit
        // rester cohérent quel que soit le moment du changement.
        var incoherents = new List<string>();
        for (int k = 1; k <= Math.Max(4, stable.Lectures); k++)
        {
            var r = FrappeAvecBasculeApres(k);
            bool emise = r.Lots >= 1;
            if (r.Absorbee != emise)
                incoherents.Add($"bascule après {k} lecture(s) : {(r.Absorbee ? "touche absorbée, rien émis (frappe perdue)" : "touche transmise ET caractère émis (doublon)")}");
        }
        Assert.True(incoherents.Count == 0, "lecture incohérente de la suspension dans un appel : " + string.Join(" ; ", incoherents));
    }

    private static (MockWin32Api Api, KeyMapper Mapper) MapperEmettant()
    {
        var api = new MockWin32Api();
        var layout = new Layout();
        layout.Keys[BancRegles130Hook.SC_E] = new KeyDefinition { Position = "D01", Scancode = BancRegles130Hook.SC_E, Base = "ê", Shift = "Ê" };
        return (api, new KeyMapper(layout, api, new TestWindowsTypingHost()));
    }

    /// <summary>
    /// M1.6-I5 : le nombre d'essais de réparation est borné. Tenu depuis 1.3.0 : au bout de
    /// KeyMapper.MaxInputRecoveryAttempts essais refusés, la réparation est abandonnée et journalisée
    /// (ReparationAbandonnee). Sous refus persistant, le nombre de lots de réparation (lots faits
    /// uniquement de relâchements) doit cesser de croître : après 20 frappes il doit être celui
    /// d'après 40. Ajusté : témoin couplé à l'ancienne implémentation (il comptait tous les lots ;
    /// après l'abandon, chaque frappe repart sur le chemin normal et émet son propre lot, refusé et
    /// compté comme perte : ce n'est pas un essai de réparation).
    /// </summary>
    [Fact]
    public void M1_6_I5_NombreDEssaisDeReparationBorne_SousRefusPersistant()
    {
        var api = new MockWin32Api();
        var host = new TestWindowsTypingHost();
        var layout = new Layout();
        layout.Keys[BancRegles130Hook.SC_E] = new KeyDefinition { Position = "D01", Scancode = BancRegles130Hook.SC_E, Base = "ê", Shift = "Ê" };
        var mapper = new KeyMapper(layout, api, host);
        api.SendInputResults.Enqueue(1);   // l'émission du « ê » (2 événements) est partielle
        api.SendInputResult = 0;           // puis Windows refuse tout, sans fin
        Assert.True(mapper.ProcessKey(BancRegles130Hook.VK_A, BancRegles130Hook.SC_E, 0, true));
        int Reparations() => api.SendInputCalls.Skip(1).Count(lot => lot.All(i => EstUnRelachement(i, 'ê')));
        int apresLaPremiere = Reparations();   // la première tentative suit l'émission
        Assert.Equal(1, apresLaPremiere);

        void Frappes(int n)
        {
            for (int i = 0; i < n; i++)
            {
                mapper.ProcessKey(BancRegles130Hook.VK_A, BancRegles130Hook.SC_E, 0, false);
                mapper.ProcessKey(BancRegles130Hook.VK_A, BancRegles130Hook.SC_E, 0, true);
            }
        }

        Frappes(5);
        int apres5 = Reparations();
        Assert.True(apres5 > apresLaPremiere, "montage : aucune nouvelle tentative de réparation");
        Frappes(15);
        int apres20 = Reparations();
        Frappes(20);
        int apres40 = Reparations();
        Assert.True(apres40 == apres20,
            $"essais non bornés : {apres20} lots de réparation après 20 frappes, {apres40} après 40 (refus persistant)");
        // Compléments, une fois la borne tenue : elle vaut le plafond annoncé, et l'abandon est journalisé une fois.
        Assert.Equal(KeyMapper.MaxInputRecoveryAttempts, apres20);
        Assert.Single(host.CriticalEvents, e => e.EventName == "ReparationAbandonnee");
    }

    private static bool EstUnRelachement(Win32.INPUT input, char caractere) =>
        (input.u.ki.dwFlags & 2) != 0 && input.u.ki.wScan == caractere;

    /// <summary>
    /// M1.6-I6 : deux réparations ne se superposent pas. Tant qu'une réparation est refusée, une
    /// nouvelle émission est bloquée avant tout envoi : le lot de réparation en attente reste celui
    /// du premier texte, et aucun second lot partiel ne peut l'écraser.
    /// </summary>
    [Fact]
    public void M1_6_I6_DeuxReparationsNeSeSuperposentPas()
    {
        var (api, mapper) = MapperEmettant();
        api.SendInputResults.Enqueue(1);   // « ê » : 1 événement sur 2
        api.SendInputResults.Enqueue(0);   // première réparation refusée
        var premier = mapper.TryEmitText("ê");
        Assert.False(premier.IsComplete);
        Assert.Equal(2, api.SendInputCalls.Count);

        // Nouvelle émission pendant la réparation refusée : bloquée, seul le lot de réparation repart.
        api.SendInputResults.Enqueue(0);
        var second = mapper.TryEmitText("b");
        Assert.True(second.Blocked);
        Assert.Equal(3, api.SendInputCalls.Count);
        Assert.All(api.SendInputCalls[2], i => Assert.True(EstUnRelachement(i, 'ê')));

        // Réparation acceptée : relâche du seul « ê », puis seulement alors le nouveau texte.
        var troisieme = mapper.TryEmitText("b");
        Assert.True(troisieme.IsComplete);
        Assert.Equal(5, api.SendInputCalls.Count);
        Assert.All(api.SendInputCalls[3], i => Assert.True(EstUnRelachement(i, 'ê')));
        Assert.Contains(api.SendInputCalls[4], i => i.u.ki.wScan == 'b');
        Assert.DoesNotContain(api.SendInputCalls.Take(4).SelectMany(l => l), i => i.u.ki.wScan == 'b');

        // Plus de réparation en attente : la suivante part seule.
        Assert.True(mapper.TryEmitText("c").IsComplete);
        Assert.Equal(6, api.SendInputCalls.Count);
    }

    /// <summary>
    /// M2.1-I4 : après Dispose, le rappel peut encore tourner avec _hookId nul, sans effet. Un
    /// identifiant factice non nul est posé par réflexion pour que Dispose parcoure sa vraie
    /// branche (UnhookWindowsHookEx sur un identifiant inconnu rend faux, sans conséquence) ;
    /// aucun hook réel n'est posé.
    /// </summary>
    [Fact]
    public void M2_1_I4_ApresDispose_LeRappelTourneSansEffet()
    {
        using var banc = new BancRegles130Hook();
        typeof(KeyboardHook).GetField("_hookId", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(banc.Hook, (IntPtr)0x7777);
        Assert.True(banc.Hook.IsInstalled);

        banc.Hook.Dispose();
        Assert.False(banc.Hook.IsInstalled);
        banc.Hook.Dispose();   // idempotent
        Assert.False(banc.Hook.IsInstalled);

        // Le rappel, encore appelé par un message en vol : touche laissée passer, appui annoncé, rien d'autre.
        Assert.Equal(0, banc.Rappel(BancRegles130Hook.VK_W, BancRegles130Hook.SC_W, BancRegles130Hook.WM_KEYDOWN));
        Assert.Equal(new[] { "appui 2C" }, banc.Evenements);
        Assert.Empty(banc.Api.SendInputCalls);
        Assert.False(banc.Hook.IsInstalled);
    }

    /// <summary>
    /// M2.3-I3 : en pass-through total (P2) aucune notification, aucun raccourci, aucune émission ;
    /// seuls les modificateurs restent suivis. Enabled reste vrai : l'inertie vient du retour
    /// anticipé, pas de l'état coupé. Témoin P0 d'abord : les mêmes frappes y agissent.
    /// </summary>
    [Fact]
    public void M2_3_I3_PassThroughTotal_AucuneNotificationRaccourciNiEmission()
    {
        using (var p0 = new BancRegles130Hook())
        {
            Assert.Equal(1, p0.Rappel(BancRegles130Hook.VK_A, BancRegles130Hook.SC_E, BancRegles130Hook.WM_KEYDOWN));
            Assert.NotEmpty(p0.Api.SendInputCalls);
            Assert.Contains("appui 10", p0.Evenements);
            p0.Rappel(BancRegles130Hook.VK_A, BancRegles130Hook.SC_E, BancRegles130Hook.WM_KEYUP);
            p0.CtrlMaj();
            Assert.Equal(1, p0.Rappel(BancRegles130Hook.VK_W, BancRegles130Hook.SC_W, BancRegles130Hook.WM_KEYDOWN));
            Assert.Equal(new[] { "recherche" }, p0.Evenements);
        }

        using var p2 = new BancRegles130Hook();
        p2.Hook.PassThroughAll = true;
        Assert.False(p2.Hook.ShortcutsWhilePassThrough);
        // Touche remappée : transmise telle quelle, rien n'est émis, rien n'est annoncé.
        Assert.Equal(0, p2.Rappel(BancRegles130Hook.VK_A, BancRegles130Hook.SC_E, BancRegles130Hook.WM_KEYDOWN));
        p2.Rappel(BancRegles130Hook.VK_A, BancRegles130Hook.SC_E, BancRegles130Hook.WM_KEYUP);
        // Les modificateurs sont toujours suivis, mais Ctrl+Maj+W n'ouvre rien.
        p2.CtrlMaj();
        Assert.True(p2.Mapper.IsToggleShortcut());
        Assert.Equal(0, p2.Rappel(BancRegles130Hook.VK_W, BancRegles130Hook.SC_W, BancRegles130Hook.WM_KEYDOWN));
        Assert.Empty(p2.Evenements);
        Assert.Empty(p2.Api.SendInputCalls);
    }

    /// <summary>
    /// M2.3-I4 : en P3 (pass-through avec raccourcis) RawKeyDown n'est pas notifié, les raccourcis
    /// restent détectés. Enabled est faux, comme après ApplyHookState pour une pause. Témoin P0 :
    /// la même frappe y est annoncée.
    /// </summary>
    [Fact]
    public void M2_3_I4_PassThroughAvecRaccourcis_PasDeRawKeyDown_RaccourcisDetectes()
    {
        using (var p0 = new BancRegles130Hook())
        {
            Assert.Equal(0, p0.Rappel(BancRegles130Hook.VK_W, BancRegles130Hook.SC_W, BancRegles130Hook.WM_KEYDOWN));
            Assert.Equal(new[] { "appui 2C" }, p0.Evenements);
        }

        using var p3 = new BancRegles130Hook();
        p3.Hook.Enabled = false;
        p3.Hook.PassThroughAll = true;
        p3.Hook.ShortcutsWhilePassThrough = true;
        Assert.Equal(0, p3.Rappel(BancRegles130Hook.VK_W, BancRegles130Hook.SC_W, BancRegles130Hook.WM_KEYDOWN));
        p3.Rappel(BancRegles130Hook.VK_W, BancRegles130Hook.SC_W, BancRegles130Hook.WM_KEYUP);
        Assert.Empty(p3.Evenements);   // aucun appui annoncé

        p3.CtrlMaj();
        Assert.Equal(1, p3.Rappel(BancRegles130Hook.VK_W, BancRegles130Hook.SC_W, BancRegles130Hook.WM_KEYDOWN));
        Assert.Equal(new[] { "recherche" }, p3.Evenements);   // le raccourci reste servi, sans appui annoncé
    }
}

/// <summary>Suivi du premier plan : M2.4-I1, I3, I5, I6, I7.</summary>
public sealed class Regles130MoteurTestsPremierPlan
{
    private static readonly IntPtr Hkl1 = (IntPtr)0x040C040C;
    private static readonly IntPtr Hkl2 = (IntPtr)0x04090409;

    private static MockWin32Api Mock(string? process, string? path = null) => new()
    {
        ScriptedProcessName = process,
        ScriptedFullPath = path,
        ForegroundWindow = process == null ? IntPtr.Zero : (IntPtr)0x1234,
    };

    /// <summary>
    /// M2.4-I1 : (mode, HKL) vient d'une seule capture du snapshot. Le premier plan est relu pendant
    /// GetEmitContext et un Recompute s'y exécute (override et HKL changés, même fenêtre) : la paire
    /// rendue doit rester celle du snapshot capturé, jamais un mode ancien avec un HKL neuf.
    /// </summary>
    [Fact]
    public void M2_4_I1_GetEmitContext_LitModeEtHklDUnSeulSnapshot()
    {
        var host = new TestWindowsTypingHost();
        var inner = Mock("notepad.exe", @"C:\Windows\notepad.exe");
        inner.CurrentHkl = Hkl1;
        var api = new ApiObservableRegles130(inner);
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero, host);
        Assert.Equal((CompatibilityMode.Default, Hkl1), monitor.GetEmitContext());   // témoin : sans changement

        int declenchements = 0;
        api.AvantLectureDuPremierPlan = () =>
        {
            declenchements++;
            host.SetCompatibilityOverride("notepad.exe", "forceOn");
            inner.CurrentHkl = Hkl2;
            monitor.Recompute();
        };
        var contexte = monitor.GetEmitContext();

        // Témoin : le Recompute a bien eu lieu pendant l'appel, le snapshot courant est le neuf.
        Assert.Equal(1, declenchements);
        Assert.Equal(CompatibilityMode.NativeCombo, monitor.CurrentMode);
        Assert.Equal(Hkl2, monitor.CurrentHkl);
        Assert.Equal((CompatibilityMode.Default, Hkl1), contexte);
    }

    private static (CompatibilityMode Mode, CompatibilitySuspendReason Reason) Etat(string process, string path, string? surcharge)
    {
        var host = new TestWindowsTypingHost();
        if (surcharge != null) host.SetCompatibilityOverride(process, surcharge);
        using var monitor = new ForegroundMonitor(Mock(process, path), IntPtr.Zero, host);
        return (monitor.CurrentMode, monitor.CurrentSuspendReason);
    }

    /// <summary>
    /// M2.4-I3 : un anti-cheat surclasse forceOn ; un accès distant surclasse forceOn et forceOff.
    /// Le mode seul ne discrimine pas forceOff (même mode DisabledAntiCheat) : le motif est asserté.
    /// </summary>
    [Fact]
    public void M2_4_I3_AntiCheatSurclasseForceOn_AccesDistantSurclasseLesDeux()
    {
        // Témoins : l'override agit sur une application ordinaire.
        Assert.Equal((CompatibilityMode.NativeCombo, CompatibilitySuspendReason.None),
            Etat("notepad.exe", @"C:\Windows\notepad.exe", "forceOn"));
        Assert.Equal((CompatibilityMode.DisabledAntiCheat, CompatibilitySuspendReason.UserOverride),
            Etat("notepad.exe", @"C:\Windows\notepad.exe", "forceOff"));

        Assert.Equal((CompatibilityMode.DisabledAntiCheat, CompatibilitySuspendReason.AntiCheat),
            Etat("VALORANT.exe", @"C:\Riot\Valorant\VALORANT.exe", "forceOn"));
        Assert.Equal((CompatibilityMode.DisabledAntiCheat, CompatibilitySuspendReason.RemoteAccess),
            Etat("parsecd.exe", @"C:\Program Files\Parsec\parsecd.exe", "forceOff"));
        Assert.Equal((CompatibilityMode.DisabledAntiCheat, CompatibilitySuspendReason.RemoteAccess),
            Etat("parsecd.exe", @"C:\Program Files\Parsec\parsecd.exe", "forceOn"));
    }

    /// <summary>
    /// M2.4-I5 : ForegroundChanged est levé à chaque Recompute qui a un nom de processus, jamais par
    /// le hook clavier. Deux Recompute sans changement : deux levées ; sans nom de processus, aucune ;
    /// un rappel du hook joué (touche remappée émise) ne lève rien.
    /// </summary>
    [Fact]
    public void M2_4_I5_ForegroundChanged_LeveParChaqueRecomputeNomme_JamaisParLeHook()
    {
        // Recompute nommés, sans aucun changement : une levée chacun.
        var mock = Mock("notepad.exe", @"C:\Windows\notepad.exe");
        using var nomme = new ForegroundMonitor(mock, IntPtr.Zero, new TestWindowsTypingHost());
        int levees = 0;
        nomme.ForegroundChanged += () => levees++;
        nomme.Recompute();
        nomme.Recompute();
        Assert.Equal(2, levees);

        // Sans nom de processus (aucun premier plan) : un Recompute inchangé ne lève rien.
        using var anonyme = new ForegroundMonitor(Mock(null), IntPtr.Zero, new TestWindowsTypingHost());
        int leveesAnonyme = 0;
        anonyme.ForegroundChanged += () => leveesAnonyme++;
        anonyme.Recompute();
        Assert.Equal(0, leveesAnonyme);

        // Le hook : une touche remappée est absorbée et émise, le moniteur ne lève rien.
        using var banc = new BancRegles130Hook();
        using var monitor = new ForegroundMonitor(banc.Api, IntPtr.Zero, banc.Host);
        banc.Mapper.SetForegroundMonitor(monitor);
        int leveesHook = 0;
        monitor.ForegroundChanged += () => leveesHook++;
        Assert.Equal(1, banc.Rappel(BancRegles130Hook.VK_A, BancRegles130Hook.SC_E, BancRegles130Hook.WM_KEYDOWN));
        banc.Rappel(BancRegles130Hook.VK_A, BancRegles130Hook.SC_E, BancRegles130Hook.WM_KEYUP);
        Assert.Equal(new[] { "appui 10" }, banc.Evenements);
        Assert.NotEmpty(banc.Api.SendInputCalls);
        Assert.Equal(0, leveesHook);
    }

    /// <summary>
    /// M2.4-I6 : un suivi indisponible n'est jamais rétabli. NON GARANTI par du code de reprise :
    /// état absorbant documenté. Test de caractérisation, vert : le suivi échoue à la construction,
    /// reste indisponible malgré Recompute et changements de premier plan, et aucun
    /// WinEventHook n'est reposé.
    /// </summary>
    [Fact]
    public void M2_4_I6_SuiviIndisponible_N_EstJamaisRetabli_EtatAbsorbant()
    {
        var mock = new MockWin32Api { ScriptedProcessName = "notepad.exe", ShouldFailSetWinEventHook = true };
        using var monitor = new ForegroundMonitor(mock, IntPtr.Zero, new TestWindowsTypingHost());
        Assert.False(monitor.IsTrackingAvailable);
        Assert.Empty(mock.WinEventRanges);

        mock.ShouldFailSetWinEventHook = false;   // Windows accepterait maintenant les crochets
        monitor.Recompute();
        mock.SimulateForegroundChange("editor.exe", @"C:\editor.exe", (IntPtr)0x04090409);
        monitor.Recompute();

        Assert.Empty(mock.WinEventRanges);   // aucune réinstallation tentée
        Assert.False(monitor.IsTrackingAvailable);
        Assert.Equal(CompatibilitySuspendReason.UnknownForeground, monitor.CurrentSuspendReason);
        Assert.Equal(CompatibilityMode.DisabledAntiCheat, monitor.GetEmitContext().Mode);
    }

    /// <summary>
    /// M2.4-I7 : une exception dans Recompute donne le snapshot de repli (UnknownForeground, saisie
    /// sécurisée, émission refusée), ForegroundChanged est levé, le snapshot est périmé donc réparé
    /// au prochain Recompute du chien de garde, qui rend l'état normal.
    /// </summary>
    [Fact]
    public void M2_4_I7_ExceptionDansRecompute_SnapshotDeRepliSecurise_PuisRepare()
    {
        var api = new ApiObservableRegles130(Mock("notepad.exe", @"C:\Windows\notepad.exe"));
        using var monitor = new ForegroundMonitor(api, IntPtr.Zero, new TestWindowsTypingHost());
        int levees = 0;
        monitor.ForegroundChanged += () => levees++;
        // Témoin : état normal avant l'exception.
        Assert.False(monitor.IsSecureInput);
        Assert.Equal(CompatibilityMode.Default, monitor.GetEmitContext().Mode);
        Assert.False(monitor.IsSnapshotStale);

        api.LeverALaProchaineInspection = true;
        monitor.Recompute();
        Assert.False(api.LeverALaProchaineInspection);   // l'exception a bien été levée par l'inspection
        Assert.Equal(1, levees);
        Assert.True(monitor.IsSecureInput);
        Assert.Equal(CompatibilitySuspendReason.UnknownForeground, monitor.CurrentSuspendReason);
        Assert.Equal(CompatibilityMode.DisabledAntiCheat, monitor.GetEmitContext().Mode);
        Assert.True(monitor.IsSnapshotStale);

        // Réparation : l'API est saine, le recalcul suivant rend l'état normal.
        monitor.Recompute();
        Assert.False(monitor.IsSecureInput);
        Assert.Equal(CompatibilitySuspendReason.None, monitor.CurrentSuspendReason);
        Assert.Equal(CompatibilityMode.Default, monitor.GetEmitContext().Mode);
        Assert.False(monitor.IsSnapshotStale);
    }
}

/// <summary>Sonde de saisie sécurisée : M2.5-I1, I3, I4, I5.</summary>
public sealed class Regles130MoteurTestsSonde
{
    // Horloge injectée et figée : aucun abandon d'ouvrier, seule l'attente de la requête est réelle.
    private sealed class Horloge
    {
        private long _now = 1_000_000;
        public long Now => Volatile.Read(ref _now);
        public void Avance(long ms) => Interlocked.Add(ref _now, ms);
    }

    private static readonly IntPtr Fenetre1 = (IntPtr)1, Fenetre2 = (IntPtr)2, Fenetre3 = (IntPtr)3;

    /// <summary>
    /// M2.5-I1 : une requête sans résultat frais rend vrai (sécurisé). Un résultat frais non sécurisé
    /// existe pour la requête 1 ; la requête 2 reste bloquée : elle ne doit pas hériter de ce résultat.
    /// </summary>
    [Fact]
    public void M2_5_I1_RequeteSansResultatFrais_RendSecurise()
    {
        var horloge = new Horloge();
        using var libere2 = new ManualResetEventSlim();
        using var entree2 = new SemaphoreSlim(0);
        using var sonde = new SecureInputProbe(() => true, w =>
        {
            if (w != Fenetre2) return false;
            entree2.Release();
            libere2.Wait();
            return true;
        }, timeout: 100, retryDelay: 0, abandonAfter: 2000, clock: () => horloge.Now);
        try
        {
            Assert.False(sonde.Query(Fenetre1));   // témoin : résultat frais, non sécurisé
            Assert.Equal(1, sonde.LastResultRequestId);

            bool requete2 = sonde.Query(Fenetre2);
            Assert.True(entree2.Wait(10_000));       // témoin : l'ouvrier a bien pris la requête 2 et y reste bloqué
            Assert.Equal(1, sonde.LastResultRequestId);
            Assert.True(requete2);
        }
        finally { libere2.Set(); }
    }

    /// <summary>
    /// M2.5-I3 : un résultat périmé n'est jamais renvoyé à une requête plus récente. La requête 1
    /// expire (sécurisé), son ouvrier rend ensuite « non sécurisé » en retard, puis la requête 2 se
    /// bloque à son tour : elle ne doit pas recevoir le résultat de la 1.
    /// </summary>
    [Fact]
    public void M2_5_I3_ResultatPerime_N_EstJamaisRenduAUneRequetePlusRecente()
    {
        var horloge = new Horloge();
        using var libere1 = new ManualResetEventSlim();
        using var libere2 = new ManualResetEventSlim();
        using var entree1 = new SemaphoreSlim(0);
        using var entree2 = new SemaphoreSlim(0);
        using var sonde = new SecureInputProbe(() => true, w =>
        {
            if (w == Fenetre1) { entree1.Release(); libere1.Wait(); return false; }
            entree2.Release();
            libere2.Wait();
            return true;
        }, timeout: 100, retryDelay: 0, abandonAfter: 2000, clock: () => horloge.Now);
        try
        {
            Assert.True(sonde.Query(Fenetre1));   // expire : sécurisé
            Assert.True(entree1.Wait(10_000));
            libere1.Set();                        // retour tardif : non sécurisé, pour la requête 1
            Assert.True(SpinWait.SpinUntil(() => sonde.LastResultRequestId == 1, 10_000));   // témoin : le résultat périmé est en place

            bool requete2 = sonde.Query(Fenetre2);
            Assert.True(entree2.Wait(10_000));
            Assert.Equal(1, sonde.LastResultRequestId);
            Assert.True(requete2);
        }
        finally { libere1.Set(); libere2.Set(); }
    }

    /// <summary>Une sonde dont la fenêtre 1 reste bloquée jusqu'à libération, puis rend <paramref name="retour1"/> ; les autres répondent faux vite.</summary>
    private static SecureInputProbe SondeAvecFenetre1Bloquee(Horloge horloge, ManualResetEventSlim libere, SemaphoreSlim entree, bool retour1) =>
        new(() => true, w =>
        {
            if (w != Fenetre1) return false;
            entree.Release();
            libere.Wait();
            return retour1;
        }, timeout: 100, retryDelay: 0, abandonAfter: 2000, clock: () => horloge.Now);

    /// <summary>
    /// M2.5-I4 : ResultChangedLate n'est levé que si le délai de la requête est dépassé ET que le
    /// résultat diffère du précédent. Une sonde par cas : (b) résultat différent dans le délai, (c)
    /// résultat tardif mais identique au précédent, (a) tardif et différent.
    /// </summary>
    [Fact]
    public void M2_5_I4_ResultChangedLate_SeulementSiTardifEtDifferentDuPrecedent()
    {
        // (b) Dans le délai, résultat différent du précédent (initial : sécurisé) : aucune levée.
        var horlogeB = new Horloge();
        using var libereB = new ManualResetEventSlim();
        using var entreeB = new SemaphoreSlim(0);
        using var sondeB = SondeAvecFenetre1Bloquee(horlogeB, libereB, entreeB, false);
        int leveesB = 0;
        sondeB.ResultChangedLate += () => Interlocked.Increment(ref leveesB);
        try
        {
            Assert.False(sondeB.Query(Fenetre2));
            Assert.False(sondeB.Query(Fenetre3));   // l'ouvrier est repassé après le contrôle de la requête 2
            Assert.Equal(0, Volatile.Read(ref leveesB));
        }
        finally { libereB.Set(); }

        // (c) Tardif, mais identique au précédent (sécurisé) : aucune levée.
        var horlogeC = new Horloge();
        using var libereC = new ManualResetEventSlim();
        using var entreeC = new SemaphoreSlim(0);
        using var sondeC = SondeAvecFenetre1Bloquee(horlogeC, libereC, entreeC, true);
        int leveesC = 0;
        sondeC.ResultChangedLate += () => Interlocked.Increment(ref leveesC);
        try
        {
            Assert.True(sondeC.Query(Fenetre1));   // expire
            Assert.True(entreeC.Wait(10_000));
            horlogeC.Avance(500);                   // au-delà du délai de la requête, sous le délai d'abandon
            libereC.Set();
            Assert.True(SpinWait.SpinUntil(() => sondeC.LastResultRequestId == 1, 10_000));
            Assert.False(sondeC.Query(Fenetre2));   // l'ouvrier a fini de contrôler la requête 1
            Assert.Equal(0, Volatile.Read(ref leveesC));
        }
        finally { libereC.Set(); }

        // (a) Tardif et différent du précédent : une levée.
        var horlogeA = new Horloge();
        using var libereA = new ManualResetEventSlim();
        using var entreeA = new SemaphoreSlim(0);
        using var sondeA = SondeAvecFenetre1Bloquee(horlogeA, libereA, entreeA, false);
        int leveesA = 0;
        sondeA.ResultChangedLate += () => Interlocked.Increment(ref leveesA);
        try
        {
            Assert.True(sondeA.Query(Fenetre1));   // expire
            Assert.True(entreeA.Wait(10_000));
            horlogeA.Avance(500);
            libereA.Set();
            Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref leveesA) == 1, 10_000));
            Assert.Equal(1, Volatile.Read(ref leveesA));
        }
        finally { libereA.Set(); }
    }

    /// <summary>
    /// M2.5-I5 : un _done résiduel sans résultat utile est sans conséquence. La requête 1 expire, son
    /// retour tardif laisse _done signalé, puis la requête 2 (réponse en 50 ms, non sécurisée) ne doit
    /// pas conclure sur ce signal : elle attend son propre résultat et rend faux.
    /// </summary>
    [Fact]
    public void M2_5_I5_SignalDoneResiduel_NeConclutPasSurUnResultatPerime()
    {
        var horloge = new Horloge();
        using var libere1 = new ManualResetEventSlim();
        using var entree1 = new SemaphoreSlim(0);
        using var sonde = new SecureInputProbe(() => true, w =>
        {
            if (w == Fenetre1) { entree1.Release(); libere1.Wait(); return false; }
            Thread.Sleep(50);   // la réponse arrive après le signal résiduel
            return false;
        }, timeout: 800, retryDelay: 0, abandonAfter: 2000, clock: () => horloge.Now);
        try
        {
            Assert.True(sonde.Query(Fenetre1));   // expire : sécurisé
            Assert.True(entree1.Wait(10_000));
            libere1.Set();                         // retour tardif : _done signalé sans personne pour l'attendre
            Assert.True(SpinWait.SpinUntil(() => sonde.LastResultRequestId == 1, 10_000));
            Thread.Sleep(50);                      // le signal suit l'écriture du résultat de quelques instructions

            Assert.False(sonde.Query(Fenetre2));
            Assert.Equal(2, sonde.LastResultRequestId);
        }
        finally { libere1.Set(); }
    }
}
