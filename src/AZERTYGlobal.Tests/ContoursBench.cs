using System;
using System.IO;
using AZERTYGlobal;
using TypingEngine.Core;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Banc des contours bleus du clavier de l'accueil (étape 1) : la même pièce, rendue avec
/// plusieurs rayons d'arrondi, pour qu'Antoine choisisse sur image (remontée n° 2, 07/11).
/// Porte : AZERTY_PAPIER (dossier de sortie) ; sans elle, le [Fact] sort tout de suite.
/// </summary>
public sealed class ContoursBench
{
    private static string? OutDir => Environment.GetEnvironmentVariable("AZERTY_PAPIER");

    [Fact]
    public void Variantes()
    {
        if (OutDir is not { Length: > 0 } dir) return;
        var variants = new (string Name, float Ratio, bool Smooth)[]
        {
            ("A · actuel : arrondi 0,24, non lissé", 0.24f, false),
            ("B · arrondi 0,24, lissé", 0.24f, true),
            ("C · arrondi 0,36, lissé", 0.36f, true),
            ("D · pastille 0,50, lissé", 0.50f, true),
        };
        float unit = 38f * 2f;
        var (kbW, kbH) = PaperKeyboard.Size(unit);
        int pad = 40, labelH = 70, width = kbW + 2 * pad, block = labelH + kbH + pad;
        var layout = LayoutLoader.LoadFromResource();
        float saved = PaperKeyboard.FrameRadiusRatio;
        bool savedSmooth = PaperKeyboard.FrameSmooth;
        try
        {
            for (int i = 0; i < variants.Length; i++)
            {
                var v = variants[i];
                PaperKeyboard.FrameRadiusRatio = v.Ratio;
                PaperKeyboard.FrameSmooth = v.Smooth;
                Render(Path.Combine(dir, $"contours-{(char)('a' + i)}.png"), width, block + pad, hdc =>
                {
                    PaperDraw.Fill(hdc, 0, 0, width, block + pad, PaperTheme.Surface);
                    var title = FontSpec.Of(AppFonts.Title, 40, TypeRamp.Semibold, 1f);
                    PaperText.Draw(hdc, v.Name, title, pad, pad + 40, PaperTheme.Ink);
                    var state = new PaperKeyboardState { CenteredBase = true };
                    state.FramedScancodes.UnionWith(OnboardingWindow.Step1ChangedKeys);
                    PaperKeyboard.Draw(hdc, PaperKeyboard.PlaceAt(pad, pad + labelH, unit), layout, PaperKeyboardMode.Map, state, fonts: null);
                });
            }
        }
        finally
        {
            PaperKeyboard.FrameRadiusRatio = saved;
            PaperKeyboard.FrameSmooth = savedSmooth;
        }
    }

    /// <summary>Accent seul de la touche ù (´) : 4 réglages, même clavier (remontée du 07/11, « gros trait »).</summary>
    [Fact]
    public void Accents()
    {
        if (OutDir is not { Length: > 0 } dir) return;
        var variants = new (string Name, float Ratio, string Font, int Weight, float Baseline)[]
        {
            ("A · actuel : Mono 0,8, medium", 0.8f, AppFonts.Mono, TypeRamp.Medium, 0.62f),
            ("B · Mono 0,6, regular", 0.6f, AppFonts.Mono, TypeRamp.Regular, 0.55f),
            ("C · Texte 0,8, regular", 0.8f, AppFonts.Text, TypeRamp.Regular, 0.62f),
            ("D · Texte 0,6, regular", 0.6f, AppFonts.Text, TypeRamp.Regular, 0.55f),
        };
        float unit = 38f * 2f;
        var (kbW, kbH) = PaperKeyboard.Size(unit);
        int pad = 40, labelH = 70, width = kbW + 2 * pad, block = labelH + kbH + pad;
        var layout = LayoutLoader.LoadFromResource();
        var saved = (PaperKeyboard.ThinAccentRatio, PaperKeyboard.ThinAccentFont, PaperKeyboard.ThinAccentWeight, PaperKeyboard.ThinAccentBaseline);
        try
        {
            for (int i = 0; i < variants.Length; i++)
            {
                var v = variants[i];
                (PaperKeyboard.ThinAccentRatio, PaperKeyboard.ThinAccentFont, PaperKeyboard.ThinAccentWeight, PaperKeyboard.ThinAccentBaseline)
                    = (v.Ratio, v.Font, v.Weight, v.Baseline);
                Render(Path.Combine(dir, $"accent-{(char)('a' + i)}.png"), width, block + pad, hdc =>
                {
                    PaperDraw.Fill(hdc, 0, 0, width, block + pad, PaperTheme.Surface);
                    var title = FontSpec.Of(AppFonts.Title, 40, TypeRamp.Semibold, 1f);
                    PaperText.Draw(hdc, v.Name, title, pad, pad + 40, PaperTheme.Ink);
                    var state = new PaperKeyboardState { CenteredBase = true };
                    state.FramedScancodes.UnionWith(OnboardingWindow.Step1ChangedKeys);
                    PaperKeyboard.Draw(hdc, PaperKeyboard.PlaceAt(pad, pad + labelH, unit), layout, PaperKeyboardMode.Map, state, fonts: null);
                });
            }
        }
        finally
        {
            (PaperKeyboard.ThinAccentRatio, PaperKeyboard.ThinAccentFont, PaperKeyboard.ThinAccentWeight, PaperKeyboard.ThinAccentBaseline) = saved;
        }
    }

    private static void Render(string file, int width, int height, Action<IntPtr> paint)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var input = new Win32.GdiplusStartupInput { GdiplusVersion = 1 };
        Win32.GdiplusStartup(out IntPtr token, ref input, IntPtr.Zero);
        IntPtr screen = Win32.GetDC(IntPtr.Zero);
        IntPtr hdc = Win32.CreateCompatibleDC(screen);
        IntPtr bitmap = Win32.CreateCompatibleBitmap(screen, width, height);
        Win32.ReleaseDC(IntPtr.Zero, screen);
        IntPtr old = Win32.SelectObject(hdc, bitmap);
        try
        {
            paint(hdc);
            Win32.SelectObject(hdc, old);
            BancCapture.SavePng(bitmap, file);
        }
        finally
        {
            Win32.DeleteDC(hdc);
            Win32.DeleteObject(bitmap);
            Win32.GdiplusShutdown(token);
        }
    }
}
