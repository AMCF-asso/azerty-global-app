using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Une fenêtre de premier niveau créée sur un fil quelconque devient invisible
/// (<see cref="MasqueFenetresProcessus"/>). Les fenêtres ne sont jamais montrées : ces tests ne
/// font rien apparaître à l'écran. Plus le refus du banc entier hors CI.
/// </summary>
public class MasqueFenetresProcessusTests
{
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_LAYERED = 0x00080000;
    private const long WS_EX_TRANSPARENT = 0x00000020;


    private static bool Desactive =>
        Environment.GetEnvironmentVariable(FenetresInvisibles.VisibleVariable) == "1";

    /// <summary>Fil balayé : le crochet masque au retour de WM_CREATE, sans traitement de messages.</summary>
    [Fact]
    public void Fenetre_dun_fil_deja_balaye_nait_invisible()
    {
        if (Desactive)
            return;

        long avant = MasqueFenetresProcessus.Scans;
        (long exStyle, byte alpha) = SurUnFil(attendreBalayage: true, (hwnd, _) => Etat(hwnd));
        Assert.True(MasqueFenetresProcessus.Scans > avant, $"Le masqueur ne balaie plus ({avant} balayages).");

        Assert.NotEqual(0, exStyle & WS_EX_LAYERED);
        Assert.NotEqual(0, exStyle & WS_EX_TRANSPARENT);
        Assert.Equal(1, alpha);
    }

    /// <summary>Fil né à l'instant : l'écoute des événements masque au premier traitement de messages.</summary>
    [Fact]
    public void Fenetre_dun_fil_neuf_est_masquee_des_les_premiers_messages()
    {
        if (Desactive)
            return;

        (long exStyle, byte alpha) = SurUnFil(attendreBalayage: false, (hwnd, montre) =>
        {
            (long ex, byte a) etat;
            do
            {
                while (PeekMessageW(out MSG m, IntPtr.Zero, 0, 0, 1))
                    DispatchMessageW(ref m);
                etat = Etat(hwnd);
                if (etat.a == 1)
                    break;
                Thread.Sleep(5);
            }
            while (montre.ElapsedMilliseconds < 2000);
            return etat;
        });

        Assert.NotEqual(0, exStyle & WS_EX_LAYERED);
        Assert.Equal(1, alpha);
    }

    [Theory]
    [InlineData(false, null, null, true)]        // poste, aucun sujet : refusé
    [InlineData(false, "", null, true)]
    [InlineData(false, "tuto-ex3", null, false)] // poste, sujets touchés : accepté
    [InlineData(false, null, "1", false)]        // poste, banc entier demandé
    [InlineData(true, null, null, false)]        // CI : banc entier
    public void Banc_entier_refuse_sur_un_poste(bool onCi, string? only, string? all, bool refused)
    {
        Assert.Equal(refused, BancCapture.RefuseFullBench(onCi, only, all));
    }

    private static (long, byte) SurUnFil(bool attendreBalayage, Func<IntPtr, Stopwatch, (long, byte)> lire)
    {
        (long, byte) resultat = (0, 255);
        Exception? erreur = null;
        var fil = new Thread(() =>
        {
            try
            {
                if (attendreBalayage)
                {
                    PeekMessageW(out _, IntPtr.Zero, 0, 0, 0); // crée la file de messages du fil
                    Thread.Sleep(200);
                }
                IntPtr hwnd = CreateWindowExW(0, "STATIC", "masque", WS_POPUP, 0, 0, 50, 50,
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (hwnd == IntPtr.Zero)
                    throw new InvalidOperationException("CreateWindowExW a échoué");
                try
                {
                    resultat = lire(hwnd, Stopwatch.StartNew());
                }
                finally
                {
                    DestroyWindow(hwnd);
                }
            }
            catch (Exception e)
            {
                erreur = e;
            }
        });
        fil.SetApartmentState(ApartmentState.STA);
        fil.Start();
        fil.Join();
        if (erreur != null)
            throw erreur;
        return resultat;
    }

    private static (long ex, byte a) Etat(IntPtr hwnd)
    {
        long exStyle = GetWindowLongPtrW(hwnd, GWL_EXSTYLE).ToInt64();
        byte alpha = 255;
        if ((exStyle & WS_EX_LAYERED) != 0)
            GetLayeredWindowAttributes(hwnd, out _, out alpha, out _);
        return (exStyle, alpha);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int x;
        public int y;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(int exStyle, string className, string windowName, int style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessageW(out MSG msg, IntPtr hWnd, uint min, uint max, uint remove);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessageW(ref MSG msg);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLayeredWindowAttributes(IntPtr hwnd, out uint crKey, out byte bAlpha, out uint dwFlags);
}
