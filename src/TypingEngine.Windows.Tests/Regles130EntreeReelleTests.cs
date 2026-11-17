using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Règles du lot « moteur » qui ne se prouvent que sur un vrai bureau d'entrée : un vrai
/// <c>WH_KEYBOARD_LL</c> posé par <see cref="KeyboardHook"/>, de vraies frappes par <c>SendInput</c>.
/// Même harnais que <see cref="HookReelTests"/> (hook « puits » posé avant celui du produit, qui avale
/// toute injection du test ou du produit : rien ne fuit vers la fenêtre au premier plan). Catégorie
/// <c>EntreeReelle</c> : exclus de la série <c>tests-windows</c>, joués par l'étape
/// <c>tests-entree-reelle</c> de <c>verify.ps1</c> sur le bureau courant. Ne pas les lancer sur un
/// poste utilisé : ils injectent des touches.
/// </summary>
[Trait("Categorie", "EntreeReelle")]
[Collection(EntreeReelleCollection.Nom)]
public sealed class Regles130EntreeReelleTests
{
    private const int WH_KEYBOARD_LL = 13;
    private const uint LLKHF_INJECTED = 0x10;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;
    private const uint WM_KEYUP = 0x0101;
    private const uint WM_SYSKEYUP = 0x0105;
    private const uint PM_REMOVE = 1;
    private const uint DESKTOP_ALL = 0x000F01FF;

    // Touche remappée : position AZERTY de A (scancode 0x10) sur « ê ».
    private const uint ScanRemappee = 0x10;
    // Touche absente de la disposition du test : le produit la laisse passer, sans la remapper.
    private const uint ScanNonRemappee = 0x2C;
    private const string Sortie = "ê";
    private static readonly IntPtr MarqueurTest = (IntPtr)0x41475354;

    private sealed record Evenement(uint Vk, uint Scan, uint Flags, bool Appui, IntPtr Extra);

    private sealed class Mesure
    {
        public List<Evenement> Vus { get; } = new();
        public int RawKeyDown;
    }

    /// <summary>
    /// M2.1-I1 (variante Reinstall, VERT attendu) : au plus un hook posé par processus à l'état stable.
    /// Install puis Reinstall répétés ; une touche non remappée, un seul RawKeyDown.
    /// </summary>
    [Fact]
    public void M2_1_I1_Reinstall_UnSeulHookALEtatStable_UneFrappeUnSeulRawKeyDown()
    {
        var m = Jouer(ScanNonRemappee, paires: 1, remappee: false,
            poser: hook =>
            {
                hook.Install();
                Assert.True(hook.Reinstall());
                Assert.True(hook.Reinstall());
            },
            entreLesTours: null);

        // Témoin : la chaîne de hooks et SendInput marchent, la frappe est arrivée au puits.
        Assert.Contains(m.Vus, e => e.Scan == ScanNonRemappee && e.Appui && e.Extra == MarqueurTest);
        Assert.Equal(1, m.RawKeyDown);
    }

    /// <summary>
    /// M2.1-I1 (variante « Install deux fois », ROUGE attendu) : <c>Install</c> écrase _hookId sans
    /// décrocher le premier hook (KeyboardHook.cs, « non garanti dans Install ») ; la garde est externe
    /// (ShouldInstallHookAtLoopStart). Deux hooks coexistent : la même frappe lève deux RawKeyDown.
    /// Séparé de la variante Reinstall pour rester isolable. Le premier hook est décroché en
    /// finally pour ne rien laisser sur le bureau d'entrée.
    /// </summary>
    [Fact]
    public void M2_1_I1_InstallDeuxFois_DeuxHooksPoses_RougeAttendu()
    {
        var m = Jouer(ScanNonRemappee, paires: 1, remappee: false,
            poser: hook =>
            {
                hook.Install();
                var champ = typeof(KeyboardHook).GetField("_hookId",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
                var premier = (IntPtr)champ.GetValue(hook)!;
                hook.Install();   // le second écrase _hookId : le premier n'est plus joignable par Dispose
                hook.RegisterLeakedHookForCleanup(premier);
            },
            entreLesTours: null);

        Assert.Contains(m.Vus, e => e.Scan == ScanNonRemappee && e.Appui && e.Extra == MarqueurTest);
        Assert.Equal(1, m.RawKeyDown);   // rouge attendu : 2 (deux hooks)
    }

    /// <summary>
    /// M2.1-I2 et M3.8-I2 (même test) : aucune frappe perdue ni doublée pendant des Reinstall répétés.
    /// Une rafale de N touches remappées, un Reinstall à chaque tour de pompe : N paires exactement de
    /// paquets Unicode (appui puis relâche du caractère remappé), aucune touche d'origine qui fuit.
    /// </summary>
    [Fact]
    public void M2_1_I2_M3_8_I2_RafaleDeTouchesRemappees_PendantDesReinstallRepetes_NParesExactement()
    {
        const int paires = 40;
        int reinstalls = 0;
        var m = Jouer(ScanRemappee, paires, remappee: true,
            poser: hook => hook.Install(),
            entreLesTours: hook =>
            {
                Assert.True(hook.Reinstall());
                reinstalls++;
            });

        // Témoins : des Reinstall ont eu lieu pendant la rafale, aucune touche d'origine ne fuit.
        Assert.True(reinstalls >= 5, $"Reinstall appelés : {reinstalls}");
        Assert.DoesNotContain(m.Vus, e => e.Scan == ScanRemappee && e.Extra == MarqueurTest);

        var paquets = m.Vus.Where(e => e.Vk == KeyboardHook.VK_PACKET && e.Scan == Sortie[0]).ToList();
        Assert.Equal(paires, paquets.Count(p => p.Appui));
        Assert.Equal(paires, paquets.Count(p => !p.Appui));
    }

    private static Mesure Jouer(uint scan, int paires, bool remappee, Action<KeyboardHook> poser, Action<KeyboardHook>? entreLesTours)
    {
        Mesure? mesure = null;
        Exception? echec = null;
        // Fil dédié : SetThreadDesktop refuse un fil qui a déjà une fenêtre ou un hook.
        var fil = new Thread(() =>
        {
            try { mesure = JouerSurLeBureauDEntree(scan, paires, remappee, poser, entreLesTours); }
            catch (Exception ex) { echec = ex; }
        }) { IsBackground = true, Name = "Regles130EntreeReelle" };
        fil.Start();
        Assert.True(fil.Join(TimeSpan.FromSeconds(60)), "le fil du test ne s'est pas terminé");
        if (echec != null)
            throw new InvalidOperationException($"mesure impossible : {echec.Message}", echec);
        return mesure!;
    }

    private static Mesure JouerSurLeBureauDEntree(uint scan, int paires, bool remappee, Action<KeyboardHook> poser, Action<KeyboardHook>? entreLesTours)
    {
        IntPtr bureau = OpenInputDesktop(0, false, DESKTOP_ALL);
        if (bureau == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "OpenInputDesktop : pas de bureau d'entrée (session déconnectée ou verrouillée)");
        try
        {
            if (!SetThreadDesktop(bureau))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SetThreadDesktop");

            var mesure = new Mesure();
            Win32.LowLevelKeyboardProc puits = (nCode, wParam, lParam) =>
            {
                if (nCode >= 0)
                {
                    var h = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
                    bool injecte = (h.flags & LLKHF_INJECTED) != 0;
                    if (injecte && (h.dwExtraInfo == MarqueurTest || h.dwExtraInfo == KeyboardHook.INJECTED_FLAG))
                    {
                        int msg = wParam.ToInt32();
                        mesure.Vus.Add(new Evenement(h.vkCode, h.scanCode, h.flags, msg != WM_KEYUP && msg != WM_SYSKEYUP, h.dwExtraInfo));
                        return (IntPtr)1;
                    }
                }
                return Win32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
            };
            IntPtr hPuits = Win32.SetWindowsHookEx(WH_KEYBOARD_LL, puits, Win32.GetModuleHandleW(null), 0);
            if (hPuits == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SetWindowsHookEx (puits)");

            KeyboardHook? produit = null;
            try
            {
                var layout = new Layout();
                layout.Keys[ScanRemappee] = new KeyDefinition { Position = "C01", Scancode = ScanRemappee, Base = Sortie, Shift = Sortie };
                var mapper = new KeyMapper(layout, new RealWin32Api());
                // Une injection étrangère n'est une frappe physique qu'en session distante active
                // (C5, revu par l'audit M1) : c'est ce que ces tests éprouvent, ils le déclarent.
                produit = new KeyboardHook(mapper) { RemoteSessionActive = true, Enabled = true };
                produit.RawKeyDown += s => { if (s == scan) mesure.RawKeyDown++; };
                poser(produit);

                var envoi = Task.Run(() =>
                {
                    for (int i = 0; i < paires; i++)
                    {
                        var entrees = new[]
                        {
                            Touche(scan, KEYEVENTF_SCANCODE),
                            Touche(scan, KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP),
                        };
                        uint n = Win32.SendInput((uint)entrees.Length, entrees, Marshal.SizeOf<Win32.INPUT>());
                        if (n != entrees.Length)
                            throw new Win32Exception(Marshal.GetLastWin32Error(), $"SendInput a accepté {n} événement(s) sur {entrees.Length}");
                        Thread.Sleep(5);
                    }
                });

                // Les hooks bas niveau ne tournent que si le fil qui les a posés pompe. Chaque tour
                // pompe puis laisse le test agir sur le hook (Reinstall), sur le même fil que le rappel.
                var fin = DateTime.UtcNow.AddSeconds(20);
                DateTime? envoyeA = null;
                while (DateTime.UtcNow < fin)
                {
                    while (PeekMessageW(out var msg, IntPtr.Zero, 0, 0, PM_REMOVE))
                        DispatchMessageW(ref msg);
                    entreLesTours?.Invoke(produit);
                    if (envoyeA == null && envoi.IsCompleted) envoyeA = DateTime.UtcNow;
                    // Marge après l'envoi : on pompe encore pour laisser apparaître un doublon éventuel.
                    if (envoyeA != null && DateTime.UtcNow - envoyeA > TimeSpan.FromMilliseconds(1000)) break;
                    Thread.Sleep(3);
                }
                envoi.GetAwaiter().GetResult();
            }
            finally
            {
                produit?.Dispose();
                produit?.UnhookLeakedHooks();
                Win32.UnhookWindowsHookEx(hPuits);
                GC.KeepAlive(puits);
            }
            return mesure;
        }
        finally
        {
            CloseDesktop(bureau);
        }
    }

    private static Win32.INPUT Touche(uint scan, uint flags) => new()
    {
        type = 1, // INPUT_KEYBOARD
        u = new Win32.INPUTUNION { ki = new Win32.KEYBDINPUT { wVk = 0, wScan = (ushort)scan, dwFlags = flags, dwExtraInfo = MarqueurTest } },
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd; public uint message; public UIntPtr wParam; public IntPtr lParam;
        public uint time; public int x, y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetThreadDesktop(IntPtr desktop);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll")]
    private static extern bool PeekMessageW(out MSG msg, IntPtr hwnd, uint min, uint max, uint remove);
    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessageW(ref MSG msg);
}

/// <summary>
/// Les hooks que <c>Install</c> écrase sans décrocher ne sont plus joignables par <c>Dispose</c> : le
/// test de la variante « Install deux fois » les note ici pour les décrocher en fin de mesure.
/// </summary>
internal static class Regles130HooksPerdus
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<KeyboardHook, List<IntPtr>> Perdus = new();

    internal static void RegisterLeakedHookForCleanup(this KeyboardHook hook, IntPtr id) =>
        Perdus.GetOrCreateValue(hook).Add(id);

    internal static void UnhookLeakedHooks(this KeyboardHook hook)
    {
        if (!Perdus.TryGetValue(hook, out var ids)) return;
        foreach (var id in ids)
            Win32.UnhookWindowsHookEx(id);
        ids.Clear();
    }
}
