using System;
using System.IO;
using System.Runtime.InteropServices;
using AZERTYGlobal;
using TypingEngine.Windows;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Banc du lot 3b de l'audit 1.3.0 (A10, A11) : bulle de bascule, indicateur de couche et
/// icône de la zone de notification, rendus hors fenêtre dans une image, sur un fond clair et
/// un fond sombre. Porte : AZERTY_PAPIER, dossier de sortie, comme <see cref="PaperBench"/>.
/// Sans elle, chaque [Fact] sort tout de suite.
/// </summary>
public sealed class NotifBench
{
    private static string? OutDir => Environment.GetEnvironmentVariable("AZERTY_PAPIER");

    // Fonds d'essai : document blanc, barre des tâches claire et sombre de Windows 11.
    private const uint Light = 0x00FFFFFF;
    private const uint TaskbarLight = 0x00F3F3F3;
    private const uint TaskbarDark = 0x00202020;

    [Fact]
    public void Bulle()
    {
        if (OutDir is not { Length: > 0 } dir) return;
        Render(Path.Combine(dir, "notif-bulle.png"), 2 * 280, 2 * 96, hdc =>
        {
            int row = 0;
            foreach (uint background in new[] { Light, TaskbarDark })
            {
                PaperDraw.Fill(hdc, 0, row * 96, 560, row * 96 + 96, background);
                int column = 0;
                foreach (bool activated in new[] { true, false })
                {
                    var rect = new Win32.RECT { left = column * 280 + 20, top = row * 96 + 20, right = column * 280 + 260, bottom = row * 96 + 76 };
                    PaintBubble(hdc, rect, activated);
                    column++;
                }
                row++;
            }
        });
    }

    [Fact]
    public void Indicateur()
    {
        if (OutDir is not { Length: > 0 } dir) return;
        var labels = new (string Label, MaintainableLayerMode Mode)[]
        {
            ("Grec", MaintainableLayerMode.Inactive),
            ("Grec · 1", MaintainableLayerMode.OneShot),
            ("Grec" + L.Layers_IndicatorLockedSuffix, MaintainableLayerMode.Locked),
        };
        Render(Path.Combine(dir, "notif-indicateur.png"), 3 * 160, 2 * 64, hdc =>
        {
            int row = 0;
            foreach (uint background in new[] { Light, TaskbarDark })
            {
                PaperDraw.Fill(hdc, 0, row * 64, 480, row * 64 + 64, background);
                for (int i = 0; i < labels.Length; i++)
                {
                    var (width, height) = LayerIndicatorWindow.IndicatorSize(labels[i].Label.Length, 96);
                    var rect = new Win32.RECT { left = i * 160 + 10, top = row * 64 + 16, right = i * 160 + 10 + width, bottom = row * 64 + 16 + height };
                    PaintIndicator(hdc, rect, labels[i].Label, labels[i].Mode);
                }
                row++;
            }
        });
    }

    [Fact]
    public void Icone()
    {
        if (OutDir is not { Length: > 0 } dir) return;
        // Quatre états (actif, désactivé, Verr. Maj., suspendu pour un anti-triche) à 16, 20,
        // 24 et 32 px (100 à 200 %), sur les deux barres des tâches.
        var states = new (bool Active, bool Caps, bool Suspended)[]
        {
            (true, false, false), (false, false, false), (true, true, false), (false, false, true),
        };
        int[] sizes = { 16, 20, 24, 32 };
        Render(Path.Combine(dir, "notif-icone.png"), states.Length * 48, 2 * sizes.Length * 40, hdc =>
        {
            int row = 0;
            foreach (uint background in new[] { TaskbarLight, TaskbarDark })
            {
                foreach (int size in sizes)
                {
                    PaperDraw.Fill(hdc, 0, row * 40, states.Length * 48, row * 40 + 40, background);
                    for (int i = 0; i < states.Length; i++)
                    {
                        IntPtr icon = CreateTrayIcon(states[i].Active, states[i].Caps, states[i].Suspended, size);
                        Assert.NotEqual(IntPtr.Zero, icon);
                        DrawIconEx(hdc, i * 48 + 8, row * 40 + 4, icon, size, size, 0, IntPtr.Zero, 3);
                        Win32.DestroyIcon(icon);
                    }
                    row++;
                }
            }
        });
    }

    private static void PaintBubble(IntPtr hdc, Win32.RECT rect, bool activated)
    {
        IntPtr brush = Win32.CreateSolidBrush(DarkTheme.Surface);
        IntPtr font = Win32.CreateFontW(-TypeRamp.Body, 0, 0, 0, TypeRamp.Semibold, 0, 0, 0, 0, 0, 0, 5, 0, TypeRamp.Family);
        try
        {
            Win32.SetViewportOrgEx(hdc, rect.left, rect.top, IntPtr.Zero);
            ToggleNotification.Paint(hdc, new Win32.RECT { right = rect.right - rect.left, bottom = rect.bottom - rect.top }, activated, brush, font);
            Win32.SetViewportOrgEx(hdc, 0, 0, IntPtr.Zero);
        }
        finally
        {
            Win32.DeleteObject(font);
            Win32.DeleteObject(brush);
        }
    }

    private static void PaintIndicator(IntPtr hdc, Win32.RECT rect, string label, MaintainableLayerMode mode)
    {
        IntPtr brush = Win32.CreateSolidBrush(DarkTheme.Surface);
        IntPtr font = Win32.CreateFontW(-TypeRamp.Body, 0, 0, 0, TypeRamp.Semibold, 0, 0, 0, 0, 0, 0, 5, 0, TypeRamp.Family);
        try
        {
            LayerIndicatorWindow.Paint(hdc, rect, label, mode, brush, font);
        }
        finally
        {
            Win32.DeleteObject(font);
            Win32.DeleteObject(brush);
        }
    }

    private static IntPtr CreateTrayIcon(bool active, bool caps, bool suspended, int size)
        => TrayApplication.CreateTextIcon("AG", active, caps, suspended, size);

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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DrawIconEx(IntPtr hdc, int x, int y, IntPtr icon, int width, int height,
        uint step, IntPtr flickerFreeBrush, uint flags);
}
