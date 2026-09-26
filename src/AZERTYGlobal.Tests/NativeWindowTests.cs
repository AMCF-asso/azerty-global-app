using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit du 25/09, lot 8 (X-01, F-01) — le socle commun des fenêtres, <see cref="NativeWindow"/>.
///
/// Les témoins qui touchent Win32 enregistrent des classes au nom unique et ne créent, au
/// plus, qu'une fenêtre « message-only » (parent HWND_MESSAGE) : elle n'est jamais affichée,
/// rien ne s'ouvre à l'écran. Le rendu des fenêtres, lui, se vérifie au banc de captures.
/// </summary>
public class NativeWindowTests
{
    private static readonly IntPtr HWND_MESSAGE = new(-3);
    private const int OBJ_BRUSH = 2;

    // ═══ Enregistrement de classe : la règle, sans Win32 ═══

    [Fact]
    public void ClasseEnregistréeDuPremierCoup_RienNEstDésenregistré()
    {
        var appels = new List<string>();
        bool ok = NativeWindow.RegisterOrReplace(
            () => { appels.Add("register"); return true; },
            () => { appels.Add("erreur"); return 0; },
            () => { appels.Add("unregister"); return true; });

        Assert.True(ok);
        Assert.Equal(new[] { "register" }, appels);
    }

    [Fact]
    public void ClasseDéjàEnregistrée_EstRemplacéeEtNonRéutilisée()
    {
        // L'erreur 1410 veut dire qu'une classe a survécu à une instance précédente, avec la
        // procédure de cette instance : la réutiliser ferait partir la nouvelle fenêtre sur
        // un objet disposé.
        var appels = new List<string>();
        int essais = 0;
        bool ok = NativeWindow.RegisterOrReplace(
            () => { appels.Add("register"); return ++essais > 1; },
            () => NativeWindow.ERROR_CLASS_ALREADY_EXISTS,
            () => { appels.Add("unregister"); return true; });

        Assert.True(ok);
        Assert.Equal(new[] { "register", "unregister", "register" }, appels);
    }

    [Fact]
    public void ClasseDéjàEnregistréeEtIndélogeable_EstUnÉchec()
    {
        // Une fenêtre de l'ancienne classe vit encore : Windows refuse de la désenregistrer.
        // L'échec doit remonter, pour que la fenêtre ne soit pas créée sur l'ancienne classe.
        int enregistrements = 0;
        bool ok = NativeWindow.RegisterOrReplace(
            () => { enregistrements++; return false; },
            () => NativeWindow.ERROR_CLASS_ALREADY_EXISTS,
            () => false);

        Assert.False(ok);
        Assert.Equal(1, enregistrements);
    }

    [Fact]
    public void AutreErreurDEnregistrement_EstUnÉchecSansDésenregistrer()
    {
        bool désenregistrée = false;
        bool ok = NativeWindow.RegisterOrReplace(
            () => false,
            () => 87, // ERROR_INVALID_PARAMETER
            () => { désenregistrée = true; return true; });

        Assert.False(ok);
        Assert.False(désenregistrée);
    }

    // ═══ Enregistrement de classe : Win32 réel ═══

    [Fact]
    public void ClasseSurvivante_LaFenêtreRepartSurLaNouvelleProcédure()
    {
        string name = UniqueClassName();
        Win32.WNDPROC ancienne = (h, m, w, l) => Win32.DefWindowProcW(h, m, w, l);
        Win32.WNDPROC nouvelle = (h, m, w, l) => Win32.DefWindowProcW(h, m, w, l);
        IntPtr module = Win32.GetModuleHandleW(null);

        // La classe d'une instance précédente, jamais désenregistrée.
        var survivante = new Win32.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEXW>(),
            lpfnWndProc = ancienne,
            hInstance = module,
            lpszClassName = name,
        };
        Assert.NotEqual(0, Win32.RegisterClassExW(ref survivante));
        try
        {
            Assert.True(NativeWindow.RegisterClass(name, nouvelle));

            var info = ReadClass(module, name);
            Assert.Equal(Marshal.GetFunctionPointerForDelegate(nouvelle), info.lpfnWndProc);
            // Aucun pinceau à l'enregistrement : le fond passe par ApplyClassBackground.
            Assert.Equal(IntPtr.Zero, info.hbrBackground);
        }
        finally
        {
            NativeWindow.UnregisterClass(name);
            GC.KeepAlive(ancienne);
            GC.KeepAlive(nouvelle);
        }
    }

    [Fact]
    public void FondDeClasse_PoséParFenêtre_EtDétruitParWindowsAuDésenregistrement()
    {
        string name = UniqueClassName();
        Win32.WNDPROC procédure = (h, m, w, l) => Win32.DefWindowProcW(h, m, w, l);
        Assert.True(NativeWindow.RegisterClass(name, procédure));
        IntPtr hwnd = Win32.CreateWindowExW(0, name, string.Empty, 0, 0, 0, 0, 0,
            HWND_MESSAGE, IntPtr.Zero, Win32.GetModuleHandleW(null), IntPtr.Zero);
        try
        {
            Assert.NotEqual(IntPtr.Zero, hwnd);

            IntPtr premier = NativeWindow.ApplyClassBackground(hwnd, 0x00DDDDDD);
            Assert.Equal(premier, GetClassLongPtrW(hwnd, Win32.GCLP_HBRBACKGROUND));
            Assert.Equal(OBJ_BRUSH, GetObjectType(premier));

            // Un second fond remplace le premier et le détruit : il venait de ce socle.
            // ⚠️ GetObjectType ne le voit pas : gdi32 garde en cache les pinceaux pleins que
            // l'application détruit, et le handle reste lisible (mesuré le 26/09). Un second
            // DeleteObject, lui, échoue sur un pinceau déjà détruit.
            IntPtr second = NativeWindow.ApplyClassBackground(hwnd, 0x00112233);
            Assert.Equal(second, GetClassLongPtrW(hwnd, Win32.GCLP_HBRBACKGROUND));
            Assert.False(DeleteObject(premier), "le premier fond n'a pas été détruit");

            Win32.DestroyWindow(hwnd);
            hwnd = IntPtr.Zero;
            NativeWindow.UnregisterClass(name);

            // C'est la règle du socle : la fenêtre ne détruit pas son fond, Windows s'en charge.
            // Cette destruction-là, faite par le système, GetObjectType la voit.
            Assert.Equal(0, GetObjectType(second));
        }
        finally
        {
            if (hwnd != IntPtr.Zero)
                Win32.DestroyWindow(hwnd);
            NativeWindow.UnregisterClass(name);
            GC.KeepAlive(procédure);
        }
    }

    [Fact]
    public void FondDeClasse_SansFenêtre_NeCréeRien()
    {
        Assert.Equal(IntPtr.Zero, NativeWindow.ApplyClassBackground(IntPtr.Zero, 0x00DDDDDD));
    }

    // ═══ DPI ═══

    [Fact]
    public void DpiIllisible_Vaut96()
    {
        Assert.Equal(96, NativeWindow.DpiOf(IntPtr.Zero));
        Assert.Equal(96, NativeWindow.DpiFromChange(IntPtr.Zero, IntPtr.Zero));
    }

    [Fact]
    public void WmDpiChanged_LeDpiEstLeMotHaut()
    {
        // Les deux mots valent le même DPI en pratique ; ils diffèrent ici pour que le
        // témoin voie une lecture du mauvais mot.
        IntPtr wParam = (IntPtr)((144 << 16) | 96);
        Assert.Equal(144, NativeWindow.DpiFromChange(IntPtr.Zero, wParam));
        Assert.Equal(168, NativeWindow.ApplyDpiChange(IntPtr.Zero, (IntPtr)((168 << 16) | 168), IntPtr.Zero));
    }

    [Theory]
    [InlineData(144, 1.0f, 1.5f)]
    [InlineData(96, 1.5f, 1.0f)]
    public void ÉchelleCorrigée_QuandLÉcranDiffère(int dpi, float actuelle, float attendue)
    {
        Assert.Equal(attendue, NativeWindow.CorrectedScale(dpi, actuelle));
    }

    [Theory]
    [InlineData(120, 1.25f)]
    [InlineData(0, 1.0f)]
    public void ÉchelleCorrigée_RienQuandElleNeChangePasOuEstIllisible(int dpi, float actuelle)
    {
        Assert.Null(NativeWindow.CorrectedScale(dpi, actuelle));
    }

    // ═══ Placement ═══

    [Fact]
    public void Centrage_DansLaZoneDeTravail()
    {
        var work = new Win32.RECT { left = 1920, top = 40, right = 2920, bottom = 840 };
        Assert.Equal((2220, 290), NativeWindow.CenterIn(work, 400, 300));
    }

    [Fact]
    public void Centrage_FenêtrePlusGrandeQueLaZone_PartDeSonCoin()
    {
        // Centrée, elle aurait sa barre de titre au-dessus et à gauche de l'écran.
        var work = new Win32.RECT { left = 0, top = 0, right = 1024, bottom = 728 };
        Assert.Equal((0, 0), NativeWindow.CenterIn(work, 1200, 900));
    }

    [Fact]
    public void TailleExtérieure_AjouteLeCadre()
    {
        const uint WS_POPUP = 0x80000000;
        Assert.Equal((330, 154), NativeWindow.OuterSize(330, 154, WS_POPUP));

        var (w, h) = NativeWindow.OuterSize(330, 154, Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU);
        Assert.True(w > 330);
        Assert.True(h > 154);
    }

    // ═══ Mise en page D1 ═══

    [Fact]
    public void MiseEnPageD1_ÀLÉchelleAvecSaPolice()
    {
        var layout = new ControlLayout();
        layout.Track((IntPtr)0x11, 18, 16, 280, 22);
        layout.Track((IntPtr)0x22, 24, 20, 470, 26, font: 1);

        var plan = layout.Plan(144).ToArray();

        Assert.Equal(((IntPtr)0x11, 0, 27, 24, 420, 33), plan[0]);
        Assert.Equal(((IntPtr)0x22, 1, 36, 30, 705, 39), plan[1]);
        Assert.Equal(((IntPtr)0x11, 0, 18, 16, 280, 22), layout.Plan(96).First());
    }

    // ═══ Outillage ═══

    private static string UniqueClassName() => "AZERTYGlobal.Tests.Socle." + Guid.NewGuid().ToString("N");

    private static ClassInfo ReadClass(IntPtr module, string name)
    {
        var info = new ClassInfo { cbSize = (uint)Marshal.SizeOf<ClassInfo>() };
        Assert.True(GetClassInfoExW(module, name, ref info), "GetClassInfoExW a échoué");
        return info;
    }

    /// <summary>WNDCLASSEXW lue telle quelle : la procédure en pointeur, pour la comparer.</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ClassInfo
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public IntPtr lpszMenuName;
        public IntPtr lpszClassName;
        public IntPtr hIconSm;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClassInfoExW(IntPtr hInstance, string lpszClass, ref ClassInfo lpwcx);

    [DllImport("user32.dll")]
    private static extern IntPtr GetClassLongPtrW(IntPtr hWnd, int nIndex);

    [DllImport("gdi32.dll")]
    private static extern int GetObjectType(IntPtr h);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr h);
}
