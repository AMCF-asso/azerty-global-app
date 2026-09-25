using System;
using System.Collections.Generic;
using System.IO;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Banc des fenêtres qui portent un clavier : Leçons (trois états de modifieur et les deux cas
/// d'indice), clavier virtuel, mini-tutoriel de l'accueil, et une nappe du moteur de rendu
/// seul. Une image par sujet, par langue et par échelle.
///
/// Le mapper repose sur <see cref="MockWin32Api"/> : toute injection y est absorbée, et
/// <c>SendInput</c> n'est jamais appelé. Le hook clavier est construit, jamais installé :
/// aucun <c>SetWindowsHookEx</c>. Porté du banc de main (<c>5ee46c8</c>) sans la charte ni ses
/// thèmes, et sans aucun crochet dans <c>src/</c> : voir <see cref="BancCapture"/>.
///
/// Fermé par la variable <c>AZERTY_CONTEXTE</c>, comme <see cref="CaptureBench"/> l'est par
/// <c>AZERTY_CAPTURE</c> : sans elle, il sort tout de suite et compte comme réussi.
///
/// PowerShell :
/// <code>
/// $env:AZERTY_CONTEXTE = "1"
/// $env:AZERTY_CAPTURE_DIR = "D:\captures\avant"
/// dotnet test src\AZERTYGlobal.Tests\AZERTYGlobal.Tests.csproj -c Release --filter FullyQualifiedName~KeyboardContextBench
/// </code>
/// </summary>
public class KeyboardContextBench
{
    private const string GateVariable = "AZERTY_CONTEXTE";

    // Cas « frappe directe » : É, Verr. Maj + 2 — l'indice surligne la touche et le verrou.
    private const string DirectModule = "majuscules-accentuees";
    private const string DirectLesson = "caps-e-acute";

    // Cas « touche morte » : ñ s'obtient par la touche morte puis n — l'indice surligne
    // d'abord l'armement.
    private const string DeadKeyModule = "langues-etrangeres";
    private const string DeadKeyLesson = "espagnol";

    private const uint VK_LSHIFT = 0xA0;
    private const uint VK_RMENU = 0xA5;
    private const uint LLKHF_EXTENDED = 0x01;

    [Fact]
    public void RendLesFenetresAClavierAuxEchellesDemandees()
    {
        string? outDir = BancCapture.OutputDirectory(GateVariable);
        if (outDir == null)
            return;

        Directory.CreateDirectory(outDir);
        var journal = new BancCapture.Journal(outDir, "banc-clavier.tsv");
        var failures = new List<string>();

        using (var isolation = new BancCapture.Isolation())
        {
            var layout = LayoutLoader.LoadFromResource();
            var mapper = new KeyMapper(layout, new MockWin32Api());
            var hook = new KeyboardHook(mapper); // jamais installé

            foreach (string language in BancCapture.Languages)
            {
                isolation.SetLanguage(language);
                foreach (int dpi in BancCapture.Dpis)
                {
                    var target = new BancCapture.Target(outDir, language, dpi, journal);
                    var attempts = new List<(string Name, Action Run)>
                    {
                        ("clavier-virtuel", () => CaptureVirtualKeyboard(target, layout)),
                        ("tutoriel", () => CaptureLearningModule(target, layout, mapper, hook)),
                        ("clavier-nappe", () => RenderPlank(target, layout)),
                    };

                    // Leçons : les trois états de modifieur, sans indice.
                    foreach (var (state, shift, altGr) in new[] { ("aucun", false, false), ("maj", true, false), ("altgr", false, true) })
                    {
                        string slug = $"lecons-etat-{state}";
                        attempts.Add((slug, () => CaptureLessons(target, slug, layout, mapper, hook,
                            DirectModule, DirectLesson, withHint: false, shift, altGr)));
                    }

                    // Leçons : les deux cas d'indice que la table de surlignage distingue.
                    foreach (var (hintCase, module, lesson) in new[] { ("direct", DirectModule, DirectLesson), ("etape1", DeadKeyModule, DeadKeyLesson) })
                    {
                        string slug = $"lecons-indice-{hintCase}";
                        attempts.Add((slug, () => CaptureLessons(target, slug, layout, mapper, hook,
                            module, lesson, withHint: true, shift: false, altGr: false)));
                    }

                    foreach (var (name, run) in attempts)
                    {
                        try
                        {
                            run();
                        }
                        catch (Exception ex)
                        {
                            failures.Add($"{name}-{language}-{BancCapture.Percent(dpi)} : {ex.GetType().Name} : {ex.Message}");
                        }
                    }
                }
            }

            // En fin de banc : comctl32 et les écrans ont alors servi.
            journal.Header = isolation.Describe();
        }

        journal.Write();
        Assert.True(failures.Count == 0,
            $"{journal.Count} capture(s) écrite(s), {failures.Count} échec(s) :" +
            Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Leçons, ouvertes comme le faisait la porte <c>OpenForCapture</c> de main, par réflexion :
    /// exercice choisi sans enregistrer la position, échelle recalculée, fenêtre affichée, puis
    /// l'indice s'il est demandé. <c>Show()</c> n'est pas appelé : il toucherait au verrou
    /// majuscule et armerait les minuteries de séance.
    /// </summary>
    private static void CaptureLessons(BancCapture.Target target, string slug, Layout layout, KeyMapper mapper,
        KeyboardHook hook, string moduleId, string lessonId, bool withHint, bool shift, bool altGr)
    {
        // Chaque image part d'un premier affichage : aucune position mémorisée.
        ConfigManager.ClearWindowBounds(ConfigManager.LessonsWindowBoundsKey);
        var window = new LessonsWindow(layout, mapper, hook);
        try
        {
            // Les modifieurs se posent sur le mapper, comme le hook le ferait : la fenêtre les
            // lit à la peinture, et le mock absorbe toute injection.
            if (shift) mapper.TrackModifiers(VK_LSHIFT, 0x2A, 0, isKeyDown: true);
            if (altGr) mapper.TrackModifiers(VK_RMENU, 0x38, LLKHF_EXTENDED, isKeyDown: true);

            if (!(bool)BancCapture.Call(window, "SelectExercise", moduleId, lessonId, 0, false)!)
                throw new InvalidOperationException($"exercice {moduleId}/{lessonId} introuvable");
            BancCapture.Call(window, "UpdateRenderScaleFromCurrentClient", true);
            IntPtr hwnd = window.Handle;
            Win32.ShowWindow(hwnd, 1);
            int windowDpi = BancCapture.ApplyDpi(hwnd, target.Dpi);
            // Après la mise à l'échelle : visible, la fenêtre enregistre ses bornes à chaque
            // WM_SIZE, et la suivante naîtrait à la taille de la précédente.
            BancCapture.SetField(window, "_visible", true);
            if (withHint)
                BancCapture.Call(window, "ShowHintCore", true);
            Win32.InvalidateRect(hwnd, IntPtr.Zero, true);
            BancCapture.MarkActive(hwnd);
            target.Shoot(hwnd, slug, windowDpi);
        }
        finally
        {
            if (shift) mapper.TrackModifiers(VK_LSHIFT, 0x2A, 0, isKeyDown: false);
            if (altGr) mapper.TrackModifiers(VK_RMENU, 0x38, LLKHF_EXTENDED, isKeyDown: false);
            BancCapture.Teardown(window);
        }
    }

    /// <summary>
    /// Clavier virtuel : ShowWindow direct, comme le produit (SW_SHOWNA), parce que
    /// <c>Show()</c> compterait une ouverture dans les statistiques. Sa taille ne dépend pas du
    /// DPI à la création : aux échelles simulées, l'image montre la fenêtre après un passage
    /// vers un écran à ce DPI. Jamais activé par le produit, il garde sa barre de titre inactive.
    /// </summary>
    private static void CaptureVirtualKeyboard(BancCapture.Target target, Layout layout)
    {
        var keyboard = new VirtualKeyboard(layout);
        try
        {
            IntPtr hwnd = BancCapture.Handle(keyboard);
            Win32.ShowWindow(hwnd, 8); // SW_SHOWNA
            int windowDpi = BancCapture.ApplyDpi(hwnd, target.Dpi);
            target.Shoot(hwnd, "clavier-virtuel", windowDpi);
        }
        finally
        {
            BancCapture.Teardown(keyboard);
        }
    }

    /// <summary>
    /// Mini-tutoriel de l'accueil, sans propriétaire. <c>Show()</c> n'est pas appelé : il
    /// arracherait le premier plan au poste (AttachThreadInput, relances par minuterie), et
    /// une perte de focus affiche « Cliquez pour reprendre » au hasard du poste. La fenêtre est
    /// affichée sans activation, dans l'état « focus présent » où elle naît.
    /// </summary>
    private static void CaptureLearningModule(BancCapture.Target target, Layout layout, KeyMapper mapper, KeyboardHook hook)
    {
        var module = new LearningModule(IntPtr.Zero, mapper, hook, layout);
        try
        {
            IntPtr hwnd = BancCapture.Handle(module);
            Win32.ShowWindow(hwnd, 4); // SW_SHOWNOACTIVATE
            int windowDpi = BancCapture.ApplyDpi(hwnd, target.Dpi);
            BancCapture.MarkActive(hwnd);
            target.Shoot(hwnd, "tutoriel", windowDpi);
        }
        finally
        {
            BancCapture.Teardown(module);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Nappe du moteur de rendu
    // ═══════════════════════════════════════════════════════════════

    private const int PlankMargin = 28;
    private const int PlankCaption = 30;
    private const int PlankKeyboardWidth = 1100;
    private const uint PlankBackground = 0x00201C18; // fond de la fenêtre Leçons
    private const uint PlankCaptionColor = 0x00C8C8C8;

    private static readonly (string Caption, Func<KeyboardRenderState> State)[] PlankBands =
    {
        ("Aucun modifieur tenu", () => new KeyboardRenderState()),
        ("Maj tenu", () => new KeyboardRenderState { Shift = true }),
        ("AltGr tenu", () => new KeyboardRenderState { AltGr = true }),
    };

    /// <summary>
    /// Le clavier complet (profil <c>Full</c>) rendu par <c>KeyboardRenderer.Draw</c> lui-même,
    /// avec les cinq polices que la fenêtre Leçons lui passe, dans trois états de modifieur.
    /// Hors fenêtre : c'est le même code de peinture, sans la mise en page autour. Les légendes
    /// sont celles du banc, pas des textes du produit.
    /// </summary>
    private static void RenderPlank(BancCapture.Target target, Layout layout)
    {
        int S(int value) => (int)Math.Round(value * target.Dpi / 96.0, MidpointRounding.AwayFromZero);

        float maxRight = 0f, maxBottom = 0f;
        foreach (var key in KeyboardRenderer.VisualKeys)
        {
            maxRight = Math.Max(maxRight, key.X + key.W);
            maxBottom = Math.Max(maxBottom, key.Y + key.H);
        }

        int keyboardW = S(PlankKeyboardWidth);
        int keyboardH = (int)(keyboardW / maxRight * maxBottom);
        int margin = S(PlankMargin);
        int caption = S(PlankCaption);
        int band = caption + keyboardH;
        int width = keyboardW + margin * 2;
        int height = margin * 2 + band * PlankBands.Length + margin * (PlankBands.Length - 1);

        IntPtr hdcScreen = Win32.GetDC(IntPtr.Zero);
        IntPtr hdc = Win32.CreateCompatibleDC(hdcScreen);
        IntPtr bitmap = Win32.CreateCompatibleBitmap(hdcScreen, width, height);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);
        IntPtr previous = Win32.SelectObject(hdc, bitmap);

        IntPtr background = Win32.CreateSolidBrush(PlankBackground);
        IntPtr main = Win32.CreateFontW(S(28), 0, 0, 0, 600, 0, 0, 0, 0, 0, 0, 4, 0, "Consolas");
        IntPtr dead = Win32.CreateFontW(S(24), 0, 0, 0, 600, 0, 0, 0, 0, 0, 0, 4, 0, "Consolas");
        IntPtr small = Win32.CreateFontW(S(20), 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 4, 0, "Consolas");
        IntPtr tiny = Win32.CreateFontW(S(16), 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 4, 0, "Segoe UI");
        IntPtr context = Win32.CreateFontW(S(20), 0, 0, 0, 500, 0, 0, 0, 0, 0, 0, 4, 0, "Segoe UI");
        IntPtr captionFont = Win32.CreateFontW(-S(15), 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        try
        {
            var full = new Win32.RECT { left = 0, top = 0, right = width, bottom = height };
            Win32.FillRect(hdc, ref full, background);
            Win32.SetBkMode(hdc, Win32.TRANSPARENT);

            int y = margin;
            foreach (var (text, state) in PlankBands)
            {
                var captionRect = new Win32.RECT { left = margin, top = y, right = width - margin, bottom = y + caption };
                Win32.SelectObject(hdc, captionFont);
                Win32.SetTextColor(hdc, PlankCaptionColor);
                Win32.DrawTextW(hdc, text, text.Length, ref captionRect, Win32.DT_LEFT | Win32.DT_VCENTER | Win32.DT_SINGLELINE);

                var bounds = new Win32.RECT { left = margin, top = y + caption, right = margin + keyboardW, bottom = y + caption + keyboardH };
                KeyboardRenderer.Draw(hdc, bounds, layout, KeyboardRenderProfile.Full, state(), main, dead, small, tiny, context);
                y += band + margin;
            }

            Win32.SelectObject(hdc, previous);
            string file = target.File("clavier-nappe");
            BancCapture.SavePng(bitmap, file);
            target.Journal.Add(file, width, height, target.Dpi, target.Dpi, "gdi");
        }
        finally
        {
            Win32.SelectObject(hdc, previous);
            foreach (IntPtr font in new[] { main, dead, small, tiny, context, captionFont })
                Win32.DeleteObject(font);
            Win32.DeleteObject(background);
            Win32.DeleteObject(bitmap);
            Win32.DeleteDC(hdc);
        }
    }
}
