using System.ComponentModel;
using System.Runtime.InteropServices;

namespace TypingEngine.Windows.Tests;

/// <summary>
/// Audit du 30/10, M8 — le seul témoin du chemin entier : un vrai <c>WH_KEYBOARD_LL</c> posé par
/// <see cref="KeyboardHook"/>, une vraie frappe par <c>SendInput</c>, une vraie émission par
/// <see cref="RealWin32Api"/>. Tous les autres tests passent par une couche déléguée.
///
/// Le hook est lié au bureau d'entrée et <c>SendInput</c> n'y arrive que de là : le test ne
/// tourne donc ni sur le bureau dédié de <c>BureauCache.ps1</c> (le fil de test passe sur le
/// bureau d'entrée, aucun bureau d'entrée = échec, jamais saut) ni dans la série de
/// <c>tests-windows</c> (catégorie <c>EntreeReelle</c>, étape <c>tests-entree-reelle</c> de
/// <c>verify.ps1</c>).
///
/// Rien ne doit fuir vers la fenêtre au premier plan. Un hook « puits » est posé AVANT celui du
/// produit : Windows appelle le dernier posé en premier, le puits passe donc après lui. Il avale
/// toute injection portant le marqueur du test ou celui du produit, jamais une frappe physique.
/// La touche d'origine est avalée par le produit ; si elle lui échappait, le puits l'avalerait
/// aussi et le test le dirait.
/// </summary>
[Trait("Categorie", "EntreeReelle")]
public sealed class HookReelTests
{
    private const int WH_KEYBOARD_LL = 13;
    private const uint LLKHF_INJECTED = 0x10;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;
    private const uint WM_KEYUP = 0x0101;
    private const uint WM_SYSKEYUP = 0x0105;
    private const uint PM_REMOVE = 1;
    private const uint DESKTOP_ALL = 0x000F01FF;

    // Position AZERTY de A (scancode 0x10), remappée sur « ê » dans la disposition du test.
    private const uint ScanA = 0x10;
    private const string Sortie = "ê";
    private static readonly IntPtr MarqueurTest = (IntPtr)0x41475354;

    private sealed record Evenement(uint Vk, uint Scan, uint Flags, bool Appui, IntPtr Extra);

    [Fact]
    public void SansHookDuProduit_LaTouchePhysiqueArriveAuPuits()
    {
        // Témoin négatif : la chaîne de hooks et SendInput marchent sur ce bureau. Sans lui, un
        // « le produit avale la touche » pourrait n'être qu'un SendInput qui n'arrive nulle part.
        var evenements = Jouer(avecProduit: false);

        Assert.Contains(evenements, e => e.Scan == ScanA && e.Appui && e.Extra == MarqueurTest);
        Assert.DoesNotContain(evenements, e => e.Vk == KeyboardHook.VK_PACKET);
    }

    [Fact]
    public void AvecLeHookDuProduit_LaToucheEstRemappeeEtEmise()
    {
        var evenements = Jouer(avecProduit: true);

        // La touche d'origine n'arrive jamais au puits : le hook du produit l'a avalée.
        Assert.DoesNotContain(evenements, e => e.Scan == ScanA && e.Extra == MarqueurTest);

        // Le caractère remappé sort en vrai SendInput, appui puis relâchement.
        var paquets = evenements.Where(e => e.Vk == KeyboardHook.VK_PACKET && e.Scan == Sortie[0]).ToList();
        Assert.True(paquets.Count >= 2, $"paquets Unicode vus : {paquets.Count}");
        Assert.All(paquets, p => Assert.Equal(KeyboardHook.INJECTED_FLAG, p.Extra));
        Assert.True(paquets[0].Appui);
        Assert.False(paquets[^1].Appui);
    }

    private static List<Evenement> Jouer(bool avecProduit)
    {
        List<Evenement>? vus = null;
        Exception? echec = null;
        // Fil dédié : SetThreadDesktop refuse un fil qui a déjà une fenêtre ou un hook.
        var fil = new Thread(() =>
        {
            try { vus = JouerSurLeBureauDEntree(avecProduit); }
            catch (Exception ex) { echec = ex; }
        }) { IsBackground = true, Name = "HookReel" };
        fil.Start();
        Assert.True(fil.Join(TimeSpan.FromSeconds(30)), "le fil du test ne s'est pas terminé");
        if (echec != null)
            throw new InvalidOperationException($"mesure impossible : {echec.Message}", echec);
        return vus!;
    }

    private static List<Evenement> JouerSurLeBureauDEntree(bool avecProduit)
    {
        IntPtr bureau = OpenInputDesktop(0, false, DESKTOP_ALL);
        if (bureau == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "OpenInputDesktop : pas de bureau d'entrée (session déconnectée ou verrouillée)");
        try
        {
            if (!SetThreadDesktop(bureau))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "SetThreadDesktop");

            var vus = new List<Evenement>();
            Win32.LowLevelKeyboardProc puits = (nCode, wParam, lParam) =>
            {
                if (nCode >= 0)
                {
                    var h = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
                    bool injecte = (h.flags & LLKHF_INJECTED) != 0;
                    if (injecte && (h.dwExtraInfo == MarqueurTest || h.dwExtraInfo == KeyboardHook.INJECTED_FLAG))
                    {
                        int msg = wParam.ToInt32();
                        vus.Add(new Evenement(h.vkCode, h.scanCode, h.flags, msg != WM_KEYUP && msg != WM_SYSKEYUP, h.dwExtraInfo));
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
                if (avecProduit)
                {
                    var layout = new Layout();
                    layout.Keys[ScanA] = new KeyDefinition { Position = "C01", Scancode = ScanA, Base = Sortie, Shift = Sortie };
                    var mapper = new KeyMapper(layout, new RealWin32Api());
                    // Une injection étrangère n'est une frappe physique qu'en session distante
                    // active (C5, revu par l'audit M1) : c'est le cas que ce test éprouve, il le déclare.
                    produit = new KeyboardHook(mapper) { RemoteSessionActive = true, Enabled = true };
                    produit.Install();
                    Assert.True(produit.IsInstalled);
                }

                var envoi = Task.Run(() =>
                {
                    var entrees = new[]
                    {
                        Touche(KEYEVENTF_SCANCODE),
                        Touche(KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP),
                    };
                    uint n = Win32.SendInput((uint)entrees.Length, entrees, Marshal.SizeOf<Win32.INPUT>());
                    if (n != entrees.Length)
                        throw new Win32Exception(Marshal.GetLastWin32Error(), $"SendInput a accepté {n} événement(s) sur {entrees.Length}");
                });

                // Les hooks bas niveau ne tournent que si le fil qui les a posés pompe.
                var fin = DateTime.UtcNow.AddSeconds(4);
                DateTime? envoyeA = null;
                while (DateTime.UtcNow < fin)
                {
                    while (PeekMessageW(out var msg, IntPtr.Zero, 0, 0, PM_REMOVE))
                        DispatchMessageW(ref msg);
                    if (envoyeA == null && envoi.IsCompleted) envoyeA = DateTime.UtcNow;
                    // Marge après l'envoi : l'émission du produit part du rappel, elle revient au puits ensuite.
                    if (envoyeA != null && DateTime.UtcNow - envoyeA > TimeSpan.FromMilliseconds(600)) break;
                    Thread.Sleep(5);
                }
                envoi.GetAwaiter().GetResult();
            }
            finally
            {
                produit?.Dispose();
                Win32.UnhookWindowsHookEx(hPuits);
                GC.KeepAlive(puits);
            }
            return vus;
        }
        finally
        {
            CloseDesktop(bureau);
        }
    }

    private static Win32.INPUT Touche(uint flags) => new()
    {
        type = 1, // INPUT_KEYBOARD
        u = new Win32.INPUTUNION { ki = new Win32.KEYBDINPUT { wVk = 0, wScan = (ushort)ScanA, dwFlags = flags, dwExtraInfo = MarqueurTest } },
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
