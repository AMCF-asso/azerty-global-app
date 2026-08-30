using System;
using System.IO;
using System.Threading;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Banc de captures des fenêtres refondues — chantiers CH1 à CH5.
///
/// Il existe parce que Smart App Control refuse de lancer l'exécutable fraîchement compilé,
/// faute de réputation : mesuré le 2026-08-28, la notification « we can't confirm who published
/// AZERTY Global.exe » remplace le lancement. Le même jour, `dotnet test -c Release` a chargé
/// cette DLL et exécuté 351 tests. Le contrôle visuel passe donc par le processus de test, qui
/// est la seule voie ouverte sur ce poste sans désactiver une protection irréversible.
///
/// ⚠️ Ce n'est pas un test : il n'assert rien qu'un humain ne doive regarder. Il ne s'exécute
/// que si la variable d'environnement <c>AZERTY_CAPTURE</c> nomme un dossier de sortie, ce qui
/// le laisse hors de la CI et hors des trois compteurs de la suite.
///
/// PowerShell :
/// <code>
/// $env:AZERTY_CAPTURE = "D:\captures"
/// dotnet test src\AZERTYGlobal.Tests\AZERTYGlobal.Tests.csproj -c Release --filter FullyQualifiedName~CaptureBench
/// </code>
/// </summary>
public class CaptureBench
{
    // La cellule unique par processus n'est plus nécessaire. Le handle nul de la seconde
    // fenêtre et le fond blanc de Durée de pause étaient un seul défaut, corrigé le 2026-08-29 :
    // la brosse du cache de Theme, posée en fond de classe, était détruite par le système au
    // désenregistrement de la classe de la fenêtre précédente, et RegisterClassExW échouait
    // ensuite sur ce handle mort. Les six cellules se rendent désormais d'un seul processus.
    // AZERTY_CAPTURE_THEME et AZERTY_CAPTURE_DPI restent utiles pour n'en rejouer qu'une.
    private const string GateVariable = "AZERTY_CAPTURE";

    /// <summary>Les trois échelles de la matrice d'arrêt visuel, en DPI et en pourcentage.</summary>
    private static (int Dpi, int Percent)[] Scales
    {
        get
        {
            string? only = Environment.GetEnvironmentVariable("AZERTY_CAPTURE_DPI");
            if (string.IsNullOrWhiteSpace(only))
                return new[] { (96, 100), (120, 125), (144, 150) };

            var list = new System.Collections.Generic.List<(int, int)>();
            foreach (var part in only.Split(','))
            {
                if (int.TryParse(part.Trim(), out int dpi) && dpi > 0)
                    list.Add((dpi, dpi * 100 / 96));
            }
            return list.ToArray();
        }
    }

    [Fact]
    public void RendLaMatriceDesFenetresTemoins()
    {
        string? outDir = Environment.GetEnvironmentVariable(GateVariable);
        if (string.IsNullOrWhiteSpace(outDir))
            return;

        Directory.CreateDirectory(outDir);

        var input = new Win32.GdiplusStartupInput { GdiplusVersion = 1 };
        Win32.GdiplusStartup(out IntPtr token, ref input, IntPtr.Zero);

        int written = 0;
        var failures = new System.Collections.Generic.List<string>();
        try
        {
            string? onlyTheme = Environment.GetEnvironmentVariable("AZERTY_CAPTURE_THEME");

            foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                string theme = variant == ThemeVariant.Light ? "clair" : "sombre";
                if (!string.IsNullOrWhiteSpace(onlyTheme)
                    && !string.Equals(onlyTheme, theme, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var (dpi, percent) in Scales)
                {
                    // Le DPI est forcé plutôt que réglé sur le poste : changer l'échelle
                    // d'affichage de Windows déconnecterait la session en cours.
                    using (Theme.OverrideForTests(variant))
                    using (ThemeWindow.OverrideDpiForTests(dpi))
                    {
                        foreach (var attempt in new Func<bool>[]
                                 { () => CaptureAbout(outDir, theme, percent),
                                   () => CapturePause(outDir, theme, percent),
                                   () => CaptureLayers(outDir, theme, percent),
                                   () => CaptureStats(outDir, theme, percent),
                                   () => CaptureConflict(outDir, theme, percent),
                                   () => CaptureSettings(outDir, theme, percent),
                                   () => CaptureOnboarding(outDir, theme, percent) })
                        {
                            try
                            {
                                written += attempt() ? 1 : 0;
                            }
                            catch (Exception ex)
                            {
                                failures.Add(ex.Message);
                            }
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

    private static bool CaptureAbout(string outDir, string theme, int percent)
    {
        var window = new AboutWindow();
        try
        {
            window.Show();
            return Capture(window.Handle, Path.Combine(outDir, $"a-propos-{theme}-{percent}.png"));
        }
        finally
        {
            Teardown(window);
        }
    }

    private static bool CapturePause(string outDir, string theme, int percent)
    {
        var dialog = new PauseDurationDialog();
        try
        {
            IntPtr hwnd = dialog.OpenForCapture();
            return Capture(hwnd, Path.Combine(outDir, $"duree-de-pause-{theme}-{percent}.png"));
        }
        finally
        {
            Teardown(dialog);
        }
    }

    private static bool CaptureConflict(string outDir, string theme, int percent)
    {
        // Les deux actions ne sont jamais declenchees : le banc rend la fenetre, il ne clique pas.
        var window = new LayoutConflictWindow(isAtStartup: true, () => { }, () => { });
        try
        {
            window.Show();
            return Capture(window.Handle, Path.Combine(outDir, $"conflit-{theme}-{percent}.png"));
        }
        finally
        {
            Teardown(window);
        }
    }

    private static bool CaptureStats(string outDir, string theme, int percent)
    {
        var window = new UsageStatsWindow();
        try
        {
            window.Show();
            return Capture(window.Handle, Path.Combine(outDir, $"statistiques-{theme}-{percent}.png"));
        }
        finally
        {
            Teardown(window);
        }
    }

    /// <summary>
    /// Les trois onglets de Paramètres, un fichier chacun. Depuis le 2026-08-30 la fenêtre est
    /// onglétée : ne capturer que l'onglet ouvert par défaut laisserait deux tiers de son
    /// contenu hors du contrôle visuel.
    /// </summary>
    private static bool CaptureSettings(string outDir, string theme, int percent)
    {
        var window = new SettingsWindow();
        try
        {
            window.Show();
            bool all = true;
            for (int tab = 0; tab < SettingsWindow.CaptureTabCount; tab++)
            {
                window.ShowTabForCapture(tab);
                all &= Capture(window.Handle,
                    Path.Combine(outDir,
                        $"parametres-{SettingsWindow.TabSlug(tab)}-{theme}-{percent}.png"));
            }
            return all;
        }
        finally
        {
            Teardown(window);
        }
    }

    private static bool CaptureOnboarding(string outDir, string theme, int percent)
    {
        var window = new OnboardingWindow();
        try
        {
            window.Show();
            // Les trois étapes, un fichier chacune. Ne capturer que la première laissait les
            // quatre plus gros rôles typographiques hors du contrôle visuel.
            bool all = true;
            for (int step = 0; step < OnboardingWindow.StepCountForCapture; step++)
            {
                window.ShowStepForCapture(step);
                all &= Capture(window.Handle,
                    Path.Combine(outDir, $"onboarding-etape{step + 1}-{theme}-{percent}.png"));
            }
            return all;
        }
        finally
        {
            Teardown(window);
        }
    }

    private static bool CaptureLayers(string outDir, string theme, int percent)
    {
        var window = new MaintainableLayersWindow();
        try
        {
            window.Show();
            return Capture(window.Handle, Path.Combine(outDir, $"couches-{theme}-{percent}.png"));
        }
        finally
        {
            Teardown(window);
        }
    }

    /// <summary>
    /// Detruit la fenetre, puis laisse la file de messages se vider avant la suivante. Chaque
    /// fenetre desenregistre sa classe dans son Dispose, et une classe ne se libere que lorsque
    /// la derniere fenetre qui la porte a fini d etre detruite : sans cette pompe, la fenetre
    /// suivante trouve la classe encore prise et CreateWindowExW rend un handle nul.
    /// </summary>
    private static void Teardown(IDisposable window)
    {
        window.Dispose();
        Pump(5);
    }

    /// <summary>
    /// Rend la fenêtre entière, cadre compris. PrintWindow avec PW_RENDERFULLCONTENT est ce qui
    /// ramène la composition DWM : sans ce drapeau, la barre de titre revient vide, et c'est
    /// justement sa couleur qu'un contrôle visuel doit juger.
    /// </summary>
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

    /// <summary>
    /// Laisse la fenêtre se peindre. Une boucle GetMessage bloquerait : la fenêtre ne se ferme
    /// pas d'elle-même, et rien ne posterait le WM_QUIT qui en sortirait.
    /// </summary>
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
