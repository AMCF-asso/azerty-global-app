using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public void ConflitDeDisposition_SurUneClasseIndélogeable_NEstPasCréé()
    {
        // L'erreur 1410 au bout de la chaîne : une fenêtre de l'ancienne classe vit encore,
        // la nouvelle ne doit pas naître sur l'ancienne procédure.
        WithBlockedClass(ProductIdentity.WindowClass("LayoutConflict"), () =>
        {
            using var fenêtre = new LayoutConflictWindow(true, () => { }, () => { });
            Assert.Equal(IntPtr.Zero, Handle(fenêtre));
        });
    }

    [Fact]
    public void DuréeDePause_SurUneClasseIndélogeable_NEstPasCréée()
    {
        WithBlockedClass(ProductIdentity.WindowClass("PauseDuration"), () =>
        {
            using var dialogue = new PauseDurationDialog();
            typeof(PauseDurationDialog).GetMethod("CreateWindow", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(dialogue, new object[] { IntPtr.Zero });
            Assert.Equal(IntPtr.Zero, Handle(dialogue));
        });
    }

    [Fact]
    public void DuréeDePause_ALeFondDesFenêtresÀContrôles()
    {
        // Le fond noir vu par Antoine le 26/09 : la classe n'avait aucun pinceau, et rien
        // n'effaçait la fenêtre. La fenêtre est créée sans être affichée.
        using var dialogue = new PauseDurationDialog();
        typeof(PauseDurationDialog).GetMethod("CreateWindow", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(dialogue, new object[] { IntPtr.Zero });
        IntPtr hwnd = Handle(dialogue);
        Assert.NotEqual(IntPtr.Zero, hwnd);

        IntPtr fond = GetClassLongPtrW(hwnd, Win32.GCLP_HBRBACKGROUND);
        Assert.Equal(OBJ_BRUSH, GetObjectType(fond));
        Assert.Equal(Marshal.SizeOf<LOGBRUSH>(), GetObjectW(fond, Marshal.SizeOf<LOGBRUSH>(), out var pinceau));
        Assert.Equal(LightTheme.Background, pinceau.lbColor);

        // Les étiquettes se posent sur ce fond, et non sur le gris du système.
        IntPtr étiquette = typeof(PauseDurationDialog).GetField("_hLabel", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(dialogue) is IntPtr h ? h : IntPtr.Zero;
        IntPtr hdc = CreateCompatibleDC(IntPtr.Zero);
        try
        {
            Assert.Equal(fond, Win32.SendMessageW(hwnd, Win32.WM_CTLCOLORSTATIC, hdc, étiquette));
        }
        finally
        {
            DeleteDC(hdc);
        }
    }

    [Fact]
    public void SeulLeSocleEnregistreEtDésenregistreDesClasses()
    {
        // X-01 : quatorze fenêtres enregistraient leur classe elles-mêmes, sans lire le
        // retour de RegisterClassExW, et huit y mettaient un pinceau qu'elles détruisaient
        // ensuite. Une fenêtre qui y revient échappe à la règle du socle.
        string src = Path.Combine(FindMicrosoftStoreRoot(), "src");
        var fautifs = Directory.GetFiles(src, "*.cs")
            .Where(f => Path.GetFileName(f) is not ("NativeWindow.cs" or "Win32.cs"))
            .SelectMany(f => File.ReadLines(f)
                .Where(l => l.Contains("RegisterClassExW(") || l.Contains("UnregisterClassW(")
                    || l.Contains("hbrBackground ="))
                .Where(l => !l.TrimStart().StartsWith("//"))
                .Select(l => $"{Path.GetFileName(f)} : {l.Trim()}"))
            .ToList();

        Assert.True(fautifs.Count == 0, string.Join(Environment.NewLine, fautifs));
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
        Assert.Equal((330, 154), NativeWindow.OuterSize(330, 154, WS_POPUP));

        var (w, h) = NativeWindow.OuterSize(330, 154, Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU);
        Assert.True(w > 330);
        Assert.True(h > 154);
    }

    [Fact]
    public void Redimensionnement_GardeLeCentre()
    {
        WithMessageWindow(100, 100, 200, 100, hwnd =>
        {
            NativeWindow.ResizeAroundCenter(hwnd, 100, 50, WS_POPUP);
            Assert.Equal((150, 125, 250, 175), Bounds(hwnd));
        });
    }

    [Fact]
    public void RectangleSuggéréParWmDpiChanged_EstAppliqué()
    {
        WithMessageWindow(0, 0, 50, 50, hwnd =>
        {
            var suggéré = new Win32.RECT { left = 10, top = 20, right = 510, bottom = 250 };
            IntPtr lParam = Marshal.AllocHGlobal(Marshal.SizeOf<Win32.RECT>());
            try
            {
                Marshal.StructureToPtr(suggéré, lParam, false);
                Assert.Equal(144, NativeWindow.ApplyDpiChange(hwnd, (IntPtr)((144 << 16) | 144), lParam));
            }
            finally
            {
                Marshal.FreeHGlobal(lParam);
            }
            Assert.Equal((10, 20, 510, 250), Bounds(hwnd));
        });
    }

    [Fact]
    public void D1_LaFenêtrePrendSaTailleAuDpiEtSeCentre()
    {
        WithMessageWindow(0, 0, 330, 154, hwnd =>
        {
            var work = new Win32.RECT { left = 0, top = 0, right = 1920, bottom = 1040 };
            NativeWindow.FitToDpi(hwnd, 330, 154, 144, WS_POPUP, 0, work);
            // 330 × 154 à 150 % : 495 × 231, centré dans 1920 × 1040.
            Assert.Equal((712, 404, 1207, 635), Bounds(hwnd));
        });
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

    /// <summary>
    /// Enregistre la classe avec une procédure étrangère et y garde une fenêtre
    /// message-only : Windows refuse alors de la désenregistrer. Le refus est journalisé,
    /// d'où une configuration temporaire.
    /// </summary>
    private static void WithBlockedClass(string className, Action body)
    {
        string dossier = Path.Combine(Path.GetTempPath(), "AZGSocle_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dossier);
        ConfigManager.OverrideConfigPathForTests(Path.Combine(dossier, "config.json"));

        IntPtr module = Win32.GetModuleHandleW(null);
        Win32.WNDPROC étrangère = (h, m, w, l) => Win32.DefWindowProcW(h, m, w, l);
        var wc = new Win32.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEXW>(),
            lpfnWndProc = étrangère,
            hInstance = module,
            lpszClassName = className,
        };
        Assert.NotEqual(0, Win32.RegisterClassExW(ref wc));
        IntPtr bloqueuse = Win32.CreateWindowExW(0, className, string.Empty, 0, 0, 0, 0, 0,
            HWND_MESSAGE, IntPtr.Zero, module, IntPtr.Zero);
        try
        {
            Assert.NotEqual(IntPtr.Zero, bloqueuse);
            body();
        }
        finally
        {
            Win32.DestroyWindow(bloqueuse);
            Win32.UnregisterClassW(className, module);
            GC.KeepAlive(étrangère);
        }
    }

    private const uint WS_POPUP = 0x80000000;

    /// <summary>Une fenêtre message-only sans cadre, jamais affichée, le temps du corps.</summary>
    private static void WithMessageWindow(int x, int y, int w, int h, Action<IntPtr> body)
    {
        string name = UniqueClassName();
        Win32.WNDPROC procédure = (hh, m, wp, lp) => Win32.DefWindowProcW(hh, m, wp, lp);
        Assert.True(NativeWindow.RegisterClass(name, procédure));
        IntPtr hwnd = Win32.CreateWindowExW(0, name, string.Empty, WS_POPUP, x, y, w, h,
            HWND_MESSAGE, IntPtr.Zero, Win32.GetModuleHandleW(null), IntPtr.Zero);
        try
        {
            Assert.NotEqual(IntPtr.Zero, hwnd);
            body(hwnd);
        }
        finally
        {
            Win32.DestroyWindow(hwnd);
            NativeWindow.UnregisterClass(name);
            GC.KeepAlive(procédure);
        }
    }

    private static (int, int, int, int) Bounds(IntPtr hwnd)
    {
        Assert.True(Win32.GetWindowRect(hwnd, out var r));
        return (r.left, r.top, r.right, r.bottom);
    }

    private static IntPtr Handle(object fenêtre) =>
        (IntPtr)fenêtre.GetType().GetField("_hWnd", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fenêtre)!;

    private static string FindMicrosoftStoreRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "NativeWindow.cs")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Racine Microsoft Store introuvable depuis les tests.");
    }

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

    [StructLayout(LayoutKind.Sequential)]
    private struct LOGBRUSH
    {
        public uint lbStyle;
        public uint lbColor;
        public UIntPtr lbHatch;
    }

    [DllImport("gdi32.dll")]
    private static extern int GetObjectW(IntPtr h, int size, out LOGBRUSH brush);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr h);
}
