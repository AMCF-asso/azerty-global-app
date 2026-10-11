using System;
using System.Collections.Generic;
using System.IO;
using AZERTYGlobal;
using TypingEngine.Core;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Banc des pièces « papier » de la refonte 1.3.0 (Tablature, clavier papier), rendues hors
/// fenêtre dans une image, pour les comparer aux maquettes du 07/10. Porte : AZERTY_PAPIER,
/// dossier de sortie. Sans elle, chaque [Fact] sort tout de suite.
/// </summary>
public sealed class PaperBench
{
    private static string? OutDir => Environment.GetEnvironmentVariable("AZERTY_PAPIER");

    [Fact]
    public void Lecons()
    {
        if (OutDir is not { Length: > 0 } dir) return;
        Render(Path.Combine(dir, "papier-lecons.png"), 1108, 729, hdc =>
        {
            PaperDraw.Fill(hdc, 0, 0, 1108, 729, PaperTheme.Paper);
            // [glyphe, maj, altgr, touche] de la maquette F.
            var sample = new (string G, bool S, bool A, string K)[]
            {
                ("«",false,true,"W"),(" ",true,true,"Esp"),("U",true,false,""),("n",false,false,""),(" ",false,false,""),
                ("c",false,false,""),("h",false,false,""),("e",false,false,""),("f",false,false,""),("-",false,false,""),
                ("d",false,false,""),("’",false,true,"4"),("œ",false,true,"O"),("u",false,false,""),("v",false,false,""),
                ("r",false,false,""),("e",false,false,""),(" ",false,true,"Esp"),("!",false,false,""),
                (" ",true,true,"Esp"),("»",false,true,"X"),(" ",false,false,""),("—",true,true,"T"),(" ",false,false,""),
                ("M",true,false,""),("a",false,false,""),("m",false,false,""),("a",false,false,""),("n",false,false,""),
            };
            var columns = new List<TabColumn>();
            for (int i = 0; i < sample.Length; i++)
            {
                var state = i < 17 ? TabState.Done : i == 17 ? TabState.Current : TabState.Upcoming;
                columns.Add(new TabColumn(sample[i].G, sample[i].S, sample[i].A, sample[i].K.Length > 0 ? sample[i].K : null, state));
            }
            var style = TablatureStyle.Lessons(1f);
            var surface = new Win32.RECT { left = 24, top = 76, right = 1084, bottom = 76 + 26 + style.Height + 22 + 2 };
            PaperDraw.Fill(hdc, surface, PaperTheme.Surface);
            PaperDraw.Frame(hdc, surface, PaperTheme.Border);
            var widths = TablatureRenderer.MeasureColumns(hdc, columns, style);
            var grid = new Win32.RECT { left = 25, top = 76 + 1 + 26, right = 1083, bottom = 0 };
            int scroll = TablatureRenderer.ScrollToKeep(widths, 17, grid.right - grid.left - style.LabelWidth, 0);
            TablatureRenderer.Draw(hdc, columns, widths, style, grid, scroll);

            var layout = LayoutLoader.LoadFromResource();
            var kb = new PaperKeyboardState { AltGr = false };
            kb.TargetScancodes.Add(0x39);
            kb.TargetContextLabels.Add("AltGr");
            PaperKeyboard.Draw(hdc, PaperKeyboard.PlaceAt(24, 390, 52f), layout, PaperKeyboardMode.Map, kb);

            var title = FontSpec.Of(AppFonts.Title, 26, TypeRamp.Semibold, 1f);
            PaperText.Draw(hdc, "Espace fine insécable", title, 860, 520, PaperTheme.Ink);
            var steps = new List<GestureStep> { new(false, true, "Space", "Espace") };
            PaperText.DrawParagraph(hdc, Gesture.Sentence(steps),
                FontSpec.Of(AppFonts.Text, 15, TypeRamp.Regular, 1f), FontSpec.Of(AppFonts.Text, 15, TypeRamp.Semibold, 1f),
                new Win32.RECT { left = 860, top = 540, right = 1084, bottom = 700 }, PaperTheme.TextSecondary, PaperTheme.Ink, 22);
        });
    }

    /// <summary>Notation « Mots » sous le caractère courant (QCM du 11/10) : ligne sans portée,
    /// puis un geste par case de la planche (Ź, É avant « c », U, «, Recette).</summary>
    [Fact]
    public void LeconsNotation()
    {
        if (OutDir is not { Length: > 0 } dir) return;
        Render(Path.Combine(dir, "papier-lecons-notation.png"), 1108, 640, hdc =>
        {
            PaperDraw.Fill(hdc, 0, 0, 1108, 640, PaperTheme.Paper);
            var text = "Éclat de Źanna.";
            var style = TablatureStyle.LessonsText(1f);
            var columns = new List<TabColumn>();
            for (int i = 0; i < text.Length; i++)
                columns.Add(new TabColumn(text[i].ToString(), false, false, null,
                    i < 9 ? TabState.Done : i == 9 ? TabState.Current : TabState.Upcoming));
            var part = new Win32.RECT { left = 24, top = 24, right = 1084, bottom = 24 + 22 + style.Height + 10 + 128 + 14 + 2 };
            PaperDraw.Fill(hdc, part, PaperTheme.Surface);
            PaperDraw.Frame(hdc, part, PaperTheme.Border);
            var widths = TablatureRenderer.MeasureColumns(hdc, columns, style);
            var grid = new Win32.RECT { left = 25, top = 25 + 22, right = 1083, bottom = 25 + 22 + style.Height };
            TablatureRenderer.Draw(hdc, columns, widths, style, grid, 0);
            int x = grid.left + style.LabelWidth;
            for (int c = 0; c < 9; c++) x += widths[c];
            int center = x + widths[9] / 2;
            var notation = KeyNotationStyle.Lessons(1f);
            var (zSteps, zNotes, _) = KeyNotation.ForLesson(Gesture.Of("Ź"), 'a', false, false);
            var (zw, _) = KeyNotation.Measure(hdc, zSteps, notation, zNotes);
            int maxAbove = 2 * (notation.TagHeight + notation.Gap);
            int top = grid.bottom + 10;
            KeyNotation.Draw(hdc, zSteps, notation, center - zw / 2, top + maxAbove - KeyNotation.HeightAboveCases(zSteps, notation),
                PaperTheme.Action, zNotes, 0, PaperTheme.Surface);

            // Planche : un geste par case, sur la même ligne des cases.
            var cases = new (string Label, string Glyph, char? Next, bool Caps)[]
            {
                ("É puis c", "É", 'c', false), ("É, Verr. Maj. allumée", "É", 'C', true), ("U", "U", null, false), ("«", "«", null, false),
            };
            int cx = 24, cy = part.bottom + 24;
            foreach (var (label, glyph, next, caps) in cases)
            {
                PaperText.DrawLine(hdc, label, FontSpec.Of(AppFonts.Text, 13, TypeRamp.Regular, 1f),
                    new Win32.RECT { left = cx, top = cy, right = cx + 260, bottom = cy + 18 }, PaperTheme.TextSecondary, TextAlign.Left);
                var (steps, notes, done) = KeyNotation.ForLesson(Gesture.Of(glyph), next, caps, false);
                KeyNotation.Draw(hdc, steps, notation, cx, cy + 24 + maxAbove - KeyNotation.HeightAboveCases(steps, notation),
                    PaperTheme.Action, notes, done, PaperTheme.Paper);
                cx += 270;
            }
            var (rSteps, rNotes, _) = KeyNotation.ForLesson(Gesture.Of("É"), 'c', false, false);
            PaperText.DrawParagraph(hdc, KeyNotation.Recipe(rSteps, rNotes),
                FontSpec.Of(AppFonts.Text, 15, TypeRamp.Regular, 1f), FontSpec.Of(AppFonts.Text, 15, TypeRamp.Semibold, 1f),
                new Win32.RECT { left = 24, top = cy + 170, right = 1084, bottom = cy + 200 }, PaperTheme.TextSecondary, PaperTheme.Ink, 22);
            PaperText.DrawParagraph(hdc, KeyNotation.Recipe(zSteps, zNotes),
                FontSpec.Of(AppFonts.Text, 15, TypeRamp.Regular, 1f), FontSpec.Of(AppFonts.Text, 15, TypeRamp.Semibold, 1f),
                new Win32.RECT { left = 24, top = cy + 200, right = 1084, bottom = cy + 230 }, PaperTheme.TextSecondary, PaperTheme.Ink, 22);
        });
    }

    [Fact]
    public void Recherche()
    {
        if (OutDir is not { Length: > 0 } dir) return;
        Render(Path.Combine(dir, "papier-recherche.png"), 560, 520, hdc =>
        {
            PaperDraw.Fill(hdc, 0, 0, 560, 520, PaperTheme.Paper);
            string[] chars = { "é", "ĺ", "ź", "Ź", "ǵ" };
            int y = 60;
            PaperDraw.Fill(hdc, 0, y, 560, y + 5 * 50, PaperTheme.Surface);
            for (int i = 0; i < chars.Length; i++)
            {
                bool chosen = i == 3;
                if (chosen)
                {
                    PaperDraw.Fill(hdc, 0, y, 560, y + 50, PaperTheme.ActionTint);
                    PaperDraw.Fill(hdc, 0, y, 3, y + 50, PaperTheme.Action);
                }
                PaperDraw.Fill(hdc, 0, y + 49, 560, y + 50, PaperTheme.Border);
                PaperText.DrawLine(hdc, chars[i], FontSpec.Of(AppFonts.Title, 28, TypeRamp.Semibold, 1f),
                    new Win32.RECT { left = 10, top = y, right = 62, bottom = y + 50 }, chosen ? PaperTheme.Action : PaperTheme.Ink, TextAlign.Center);
                TablatureRenderer.DrawShortGesture(hdc, Gesture.Of(chars[i]), chosen, 1f, 544, y + 25);
                y += 50;
            }
            var stepsZ = Gesture.Of("Ź");
            PaperText.DrawLine(hdc, "Ź", FontSpec.Of(AppFonts.Title, 84, TypeRamp.Semibold, 1f),
                new Win32.RECT { left = 4, top = y + 18, right = 120, bottom = y + 160 }, PaperTheme.Action, TextAlign.Center);
            var style = TablatureStyle.Search(1f);
            var columns = TabColumn.FromGesture(stepsZ);
            var widths = TablatureRenderer.MeasureColumns(hdc, columns, style);
            TablatureRenderer.Draw(hdc, columns, widths, style, new Win32.RECT { left = 128, top = y + 50, right = 540, bottom = 0 });
            PaperText.DrawParagraph(hdc, Gesture.Sentence(stepsZ),
                FontSpec.Of(AppFonts.Text, 15, TypeRamp.Regular, 1f), FontSpec.Of(AppFonts.Text, 15, TypeRamp.Semibold, 1f),
                new Win32.RECT { left = 128, top = y + 50 + style.Height + 12, right = 540, bottom = 520 }, PaperTheme.TextSecondary, PaperTheme.Ink, 22);
        });
    }

    [Fact]
    public void Clavier()
    {
        if (OutDir is not { Length: > 0 } dir) return;
        var layout = LayoutLoader.LoadFromResource();
        Render(Path.Combine(dir, "papier-clavier.png"), 854, 1000, hdc =>
        {
            PaperDraw.Fill(hdc, 0, 0, 854, 1000, PaperTheme.Paper);
            var altGr = new PaperKeyboardState { AltGr = true };
            PaperKeyboard.Draw(hdc, PaperKeyboard.PlaceAt(24, 20, 52f), layout, PaperKeyboardMode.ActiveLayer, altGr);
            var dead = new PaperKeyboardState { ActiveDeadKey = "dk_acute" };
            PaperKeyboard.Draw(hdc, PaperKeyboard.PlaceAt(24, 340, 52f), layout, PaperKeyboardMode.ActiveLayer, dead);
            var steps = new PaperKeyboardState { Shift = true };
            steps.StepBadges[0x28] = 1;
            steps.StepBadges[0x11] = 2;
            var z = Gesture.Of("Ź");
            var columns = TabColumn.FromGesture(z);
            var style = TablatureStyle.Banner(1f);
            var widths = TablatureRenderer.MeasureColumns(hdc, columns, style);
            TablatureRenderer.Draw(hdc, columns, widths, style, new Win32.RECT { left = 24, top = 650, right = 400, bottom = 0 });
            PaperKeyboard.Draw(hdc, PaperKeyboard.PlaceAt(24, 720, 52f), layout, PaperKeyboardMode.ActiveLayer, steps);
        });
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
