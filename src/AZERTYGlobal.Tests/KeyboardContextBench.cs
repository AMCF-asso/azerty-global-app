using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using TypingEngine.Windows;
using TypingEngine.Windows.Testing;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Banc de CH4a (décision S4 du 2026-09-02) : rend les trois fenêtres qui portent un clavier —
/// Leçons, Clavier virtuel, Module d'essai — entières, dans les deux thèmes, à la police réelle du
/// produit. La fenêtre Leçons est rendue dans les trois états de modifieur et sur les deux cas
/// d'indice que la table de surlignage distingue (frappe directe, armement d'une touche morte).
/// Le Clavier virtuel et le Module d'essai sont rendus tels qu'ils sont, avec leur peinture
/// propre (décision S4-1) : ils ne passent pas par le moteur, ils donnent le contexte, et ils
/// migrent à CH4b et F1a.
///
/// Les variantes que l'arrêt visuel du 2026-09-02 a tranchées (police par position, tables A et C,
/// fonds `surface` et papier) ne sont plus rendues : le produit n'en connaît plus qu'une.
///
/// Le DPI est celui du poste : ces trois fenêtres lisent GetDeviceCaps directement, le crochet
/// <c>ThemeWindow.OverrideDpiForTests</c> ne les atteint pas. Le nom du fichier porte l'échelle
/// mesurée.
///
/// Fermé par variable d'environnement, comme <see cref="CaptureBench"/> : hors CI, hors compteurs.
///
/// PowerShell :
/// <code>
/// $env:AZERTY_CONTEXTE = "D:\captures\ch4a"
/// dotnet test src\AZERTYGlobal.Tests\AZERTYGlobal.Tests.csproj -c Debug --filter FullyQualifiedName~KeyboardContextBench
/// </code>
/// </summary>
public class KeyboardContextBench
{
    private const string GateVariable = "AZERTY_CONTEXTE";

    // Cas « frappe directe » : É, Verr. Maj + 2 sur AZERTY Global — l'indice surligne la touche et le verrou.
    private const string DirectModule = "majuscules-accentuees";
    private const string DirectLesson = "caps-e-acute";

    // Cas « touche morte » : ñ s'obtient par la touche morte circonflexe puis n — l'indice surligne
    // d'abord l'armement (rang 1).
    private const string DeadKeyModule = "langues-etrangeres";
    private const string DeadKeyLesson = "espagnol";

    private const uint VK_LSHIFT = 0xA0;
    private const uint VK_RMENU = 0xA5;
    private const uint LLKHF_EXTENDED = 0x01;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowW(string lpClassName, string? lpWindowName);

    [Fact]
    public void RendLesTroisFenetresEnContexte()
    {
        string? outDir = Environment.GetEnvironmentVariable(GateVariable);
        if (string.IsNullOrWhiteSpace(outDir))
            return;

        Directory.CreateDirectory(outDir);

        var input = new Win32.GdiplusStartupInput { GdiplusVersion = 1 };
        Win32.GdiplusStartup(out IntPtr token, ref input, IntPtr.Zero);

        var layout = LayoutLoader.LoadFromResource();
        var mapper = new KeyMapper(layout, new MockWin32Api());
        var hook = new KeyboardHook(mapper); // jamais installé : aucun SetWindowsHookEx

        IntPtr hdcScreen = Win32.GetDC(IntPtr.Zero);
        int dpi = Win32.GetDeviceCaps(hdcScreen, 88);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);
        int percent = dpi > 0 ? dpi * 100 / 96 : 100;

        int written = 0;
        var failures = new List<string>();
        try
        {
            string? onlyTheme = Environment.GetEnvironmentVariable("AZERTY_CONTEXTE_THEME");

            foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                string theme = variant == ThemeVariant.Light ? "clair" : "sombre";
                if (!string.IsNullOrWhiteSpace(onlyTheme)
                    && !string.Equals(onlyTheme, theme, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using (Theme.OverrideForTests(variant))
                {
                    var attempts = new List<(string Name, Func<bool> Run)>
                    {
                        ($"clavier-virtuel-{theme}", () => CaptureVirtualKeyboard(outDir, $"clavier-virtuel-{theme}-{percent}.png", layout)),
                        ($"module-essai-{theme}", () => CaptureLearningModule(outDir, $"module-essai-{theme}-{percent}.png", layout, mapper, hook)),
                    };

                    // Leçons : les trois états de modifieur, sans indice.
                    foreach (var (modifNom, shift, altGr) in new[] { ("aucun", false, false), ("maj", true, false), ("altgr", false, true) })
                    {
                        string file = $"lecons-etat-{modifNom}-{theme}-{percent}.png";
                        attempts.Add((file, () => CaptureLessons(outDir, file, layout, mapper, hook,
                            DirectModule, DirectLesson, withHint: false, shift, altGr)));
                    }

                    // Leçons : les deux cas d'indice que la table de surlignage distingue.
                    foreach (var (casNom, module, lesson) in new[] { ("direct", DirectModule, DirectLesson), ("etape1", DeadKeyModule, DeadKeyLesson) })
                    {
                        string file = $"lecons-indice-{casNom}-{theme}-{percent}.png";
                        attempts.Add((file, () => CaptureLessons(outDir, file, layout, mapper, hook,
                            module, lesson, withHint: true, shift: false, altGr: false)));
                    }

                    foreach (var (name, run) in attempts)
                    {
                        try
                        {
                            written += run() ? 1 : 0;
                        }
                        catch (Exception ex)
                        {
                            failures.Add($"{name} : {ex.Message}");
                        }
                    }
                }
            }
        }
        finally
        {
            if (token != IntPtr.Zero)
                Win32.GdiplusShutdown(token);
        }

        Assert.True(failures.Count == 0,
            $"{written} capture(s) ecrite(s), {failures.Count} echec(s) :" +
            Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    private static bool CaptureLessons(string outDir, string file, Layout layout, KeyMapper mapper, KeyboardHook hook,
        string moduleId, string lessonId, bool withHint, bool shift, bool altGr)
    {
        var window = new LessonsWindow(layout, mapper, hook);
        try
        {
            // Les modifieurs se posent sur le mapper, comme le hook le ferait : la fenêtre les lit
            // à la peinture (_mapper.ShiftDown / AltGrDown), et le mock absorbe toute injection.
            if (shift) mapper.TrackModifiers(VK_LSHIFT, 0x2A, 0, isKeyDown: true);
            if (altGr) mapper.TrackModifiers(VK_RMENU, 0x38, LLKHF_EXTENDED, isKeyDown: true);

            IntPtr hwnd = window.OpenForCapture(moduleId, lessonId, 0, withHint);
            return Capture(hwnd, Path.Combine(outDir, file));
        }
        finally
        {
            if (shift) mapper.TrackModifiers(VK_LSHIFT, 0x2A, 0, isKeyDown: false);
            if (altGr) mapper.TrackModifiers(VK_RMENU, 0x38, LLKHF_EXTENDED, isKeyDown: false);
            Teardown(window);
        }
    }

    private static bool CaptureVirtualKeyboard(string outDir, string file, Layout layout)
    {
        var keyboard = new VirtualKeyboard(layout);
        try
        {
            // ShowWindow direct : Show() compterait une ouverture dans les statistiques d'usage.
            Win32.ShowWindow(keyboard.Handle, 8); // SW_SHOWNA, comme le produit
            return Capture(keyboard.Handle, Path.Combine(outDir, file));
        }
        finally
        {
            Teardown(keyboard);
        }
    }

    private static bool CaptureLearningModule(string outDir, string file, Layout layout, KeyMapper mapper, KeyboardHook hook)
    {
        // Pas de propriétaire : EnableWindow(0, false) dans Show() échoue sans effet. Le module
        // n'expose pas son handle ; sa classe de fenêtre est unique dans le processus.
        var module = new LearningModule(IntPtr.Zero, mapper, hook, layout);
        try
        {
            module.Show();
            IntPtr hwnd = FindWindowW(ProductIdentity.WindowClass("Learning"), null);
            return Capture(hwnd, Path.Combine(outDir, file));
        }
        finally
        {
            Teardown(module);
        }
    }

    private static void Teardown(IDisposable window)
    {
        window.Dispose();
        Pump(5);
    }

    /// <summary>Même rendu que <see cref="CaptureBench"/> : PrintWindow avec la composition DWM.</summary>
    private static bool Capture(IntPtr hwnd, string file)
    {
        if (hwnd == IntPtr.Zero)
            throw new InvalidOperationException($"{file} : handle nul");

        Pump(20);

        if (!Win32.GetWindowRect(hwnd, out var rect))
            throw new InvalidOperationException($"{file} : GetWindowRect a echoue");

        int w = rect.right - rect.left;
        int h = rect.bottom - rect.top;
        if (w <= 0 || h <= 0)
            throw new InvalidOperationException($"{file} : taille {w}x{h}");

        IntPtr hdcScreen = Win32.GetDC(IntPtr.Zero);
        IntPtr hdc = Win32.CreateCompatibleDC(hdcScreen);
        IntPtr hBitmap = Win32.CreateCompatibleBitmap(hdcScreen, w, h);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);

        IntPtr previous = Win32.SelectObject(hdc, hBitmap);
        bool rendered = Win32.PrintWindow(hwnd, hdc, Win32.PW_RENDERFULLCONTENT);
        Win32.SelectObject(hdc, previous);

        bool saved = false;
        if (rendered
            && Win32.GdipCreateBitmapFromHBITMAP(hBitmap, IntPtr.Zero, out IntPtr image) == 0
            && image != IntPtr.Zero)
        {
            var encoder = Win32.PngEncoderClsid;
            saved = Win32.GdipSaveImageToFile(image, file, ref encoder, IntPtr.Zero) == 0;
            Win32.GdipDisposeImage(image);
        }

        Win32.DeleteObject(hBitmap);
        Win32.DeleteDC(hdc);
        if (!saved)
            throw new InvalidOperationException($"{file} : PrintWindow={rendered}, taille {w}x{h}");
        return saved;
    }

    private static void Pump(int rounds)
    {
        for (int i = 0; i < rounds; i++)
        {
            while (Win32.PeekMessageW(out var msg, IntPtr.Zero, 0, 0, Win32.PM_REMOVE) != 0)
            {
                Win32.TranslateMessage(ref msg);
                Win32.DispatchMessageW(ref msg);
            }

            Thread.Sleep(15);
        }
    }
}
