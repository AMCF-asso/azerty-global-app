using System;
using System.IO;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Planche des états de touche — arrêt visuel du chantier CH4.
///
/// La table d'états du §6 de l'audit est marquée « candidate, arrêt sur pièces à CH4 », et
/// l'audit lui-même n'avait pas d'axe pour ce que les trois claviers actuels colorent en
/// réalité : le rang d'une frappe dans une séquence. Trois tables candidates existent donc
/// dans <see cref="HighlightScheme"/>, et cette planche les rend côte à côte pour qu'une
/// seule survive.
///
/// ⚠️ Ce n'est pas un test : il n'assert rien qu'un humain ne doive regarder. Fermé par
/// <c>AZERTY_KEYS</c>, comme le banc l'est par <c>AZERTY_CAPTURE</c> — donc hors CI et hors
/// des trois compteurs de la suite.
///
/// PowerShell :
/// <code>
/// $env:AZERTY_KEYS = "D:\captures\ch4"
/// dotnet test src\AZERTYGlobal.Tests\AZERTYGlobal.Tests.csproj -c Release --filter FullyQualifiedName~KeyboardStatesBoard
/// </code>
/// </summary>
public class KeyboardStatesBoard
{
    private const int Dpi = 96;
    private const int Margin = 24;
    private const int KeySize = 44;
    private const int CellWidth = 96;
    private const int RowHeight = 76;
    private const int LabelWidth = 200;

    [Fact]
    public void RendLaPlancheDesTouches()
    {
        string? outDir = Environment.GetEnvironmentVariable("AZERTY_KEYS");
        if (string.IsNullOrWhiteSpace(outDir))
            return;

        Directory.CreateDirectory(outDir);

        var input = new Win32.GdiplusStartupInput { GdiplusVersion = 1 };
        Win32.GdiplusStartup(out IntPtr token, ref input, IntPtr.Zero);
        try
        {
            foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                using (Theme.OverrideForTests(variant))
                {
                    string nom = variant == ThemeVariant.Light ? "clair" : "sombre";
                    RenderStates(Theme.Current, Path.Combine(outDir, $"touches-etats-{nom}.png"));
                    RenderHighlights(Theme.Current, Path.Combine(outDir, $"touches-surlignage-{nom}.png"));
                }
            }
        }
        finally
        {
            if (token != IntPtr.Zero)
                Win32.GdiplusShutdown(token);
        }
    }

    private static readonly (string Label, KeyState State)[] States =
    {
        ("repos", KeyState.Rest),
        ("survolée", KeyState.Hovered),
        ("enfoncée / cible", KeyState.Pressed),
        ("modifieur actif", KeyState.ModifierActive),
        ("erreur", KeyState.Error),
        ("désactivée", KeyState.Disabled),
    };

    private static readonly (string Label, KeyHighlight Highlight)[] Highlights =
    {
        ("accès direct", KeyHighlight.Direct),
        ("arme une touche morte", KeyHighlight.DeadKeyActivation),
        ("étape 1 de 2", KeyHighlight.Step1),
        ("étape 2 de 2", KeyHighlight.Step2),
    };

    private static readonly (string Label, HighlightScheme Scheme)[] Schemes =
    {
        ("A — par étape", HighlightScheme.ParEtape),
        ("B — direct vs séquence", HighlightScheme.DirectVsSequence),
        ("C — rôle unique numéroté", HighlightScheme.RoleUniqueNumerote),
    };

    /// <summary>Les six états de la charte, sur une touche de lettre puis sur un modifieur.</summary>
    private static void RenderStates(Palette palette, string file)
    {
        var rows = new[] { "Touche de lettre", "Modifieur" };
        int width = Margin * 2 + LabelWidth + CellWidth * States.Length;
        int height = Margin * 2 + RowHeight * (rows.Length + 1);

        Render(file, width, height, palette, (hdc, label, caption, mono) =>
        {
            for (int c = 0; c < States.Length; c++)
                Text(hdc, Cell(c, 0), States[c].Label, caption, palette.TextSecondary);

            for (int r = 0; r < rows.Length; r++)
            {
                Text(hdc, RowLabel(r), rows[r], caption, palette.TextSecondary);

                for (int c = 0; c < States.Length; c++)
                {
                    var paint = KeyboardTheme.Paint(States[c].State, palette);
                    if (r == 0)
                        KeyboardTheme.DrawKeyCap(hdc, KeyRect(c, r + 1), paint, "Q", "æ",
                            mono, caption, Dpi);
                    else
                        KeyboardTheme.DrawKeyCap(hdc, KeyRect(c, r + 1), paint, "AltGr", null,
                            caption, caption, Dpi);
                }
            }
        });
    }

    /// <summary>Les trois tables candidates de surlignage, une par ligne.</summary>
    private static void RenderHighlights(Palette palette, string file)
    {
        int width = Margin * 2 + LabelWidth + CellWidth * (Highlights.Length + 1);
        int height = Margin * 2 + RowHeight * (Schemes.Length + 1);

        Render(file, width, height, palette, (hdc, label, caption, mono) =>
        {
            Text(hdc, Cell(0, 0), "repos (référence)", caption, palette.TextSecondary);
            for (int c = 0; c < Highlights.Length; c++)
                Text(hdc, Cell(c + 1, 0), Highlights[c].Label, caption, palette.TextSecondary);

            for (int r = 0; r < Schemes.Length; r++)
            {
                Text(hdc, RowLabel(r), Schemes[r].Label, caption, palette.TextSecondary);

                KeyboardTheme.DrawKeyCap(hdc, KeyRect(0, r + 1),
                    KeyboardTheme.Paint(KeyState.Rest, palette), "E", "€", mono, caption, Dpi);

                for (int c = 0; c < Highlights.Length; c++)
                {
                    var highlight = Highlights[c].Highlight;
                    var paint = KeyboardTheme.HighlightPaint(highlight, palette, Schemes[r].Scheme);
                    var rect = KeyRect(c + 1, r + 1);
                    bool badge = KeyboardTheme.ShowsRankBadge(Schemes[r].Scheme)
                        && KeyboardTheme.RankOf(highlight) > 0;
                    KeyboardTheme.DrawKeyCap(hdc, rect, paint, "E", "€", mono, caption, Dpi,
                        badge ? KeyboardTheme.BadgeSize(Dpi) : 0);
                    if (badge)
                        KeyboardTheme.DrawRankBadge(hdc, rect, KeyboardTheme.RankOf(highlight),
                            palette, caption, Dpi);
                }
            }
        });
    }

    private static void Render(string file, int width, int height, Palette palette,
        Action<IntPtr, IntPtr, IntPtr, IntPtr> body)
    {
        IntPtr hdcScreen = Win32.GetDC(IntPtr.Zero);
        IntPtr hdc = Win32.CreateCompatibleDC(hdcScreen);
        IntPtr bmp = Win32.CreateCompatibleBitmap(hdcScreen, width, height);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);
        IntPtr previous = Win32.SelectObject(hdc, bmp);

        var full = new Win32.RECT { left = 0, top = 0, right = width, bottom = height };
        Win32.FillRect(hdc, ref full, Theme.Brush(palette.Paper));

        body(hdc, Theme.Font(FontRole.Body, Dpi), Theme.Font(FontRole.Secondary, Dpi),
            Theme.Font(FontRole.Mono, Dpi));

        Win32.SelectObject(hdc, previous);

        if (Win32.GdipCreateBitmapFromHBITMAP(bmp, IntPtr.Zero, out IntPtr image) == 0
            && image != IntPtr.Zero)
        {
            var encoder = Win32.PngEncoderClsid;
            Win32.GdipSaveImageToFile(image, file, ref encoder, IntPtr.Zero);
            Win32.GdipDisposeImage(image);
        }

        Win32.DeleteObject(bmp);
        Win32.DeleteDC(hdc);
    }

    private static Win32.RECT Cell(int column, int row) => new()
    {
        left = Margin + LabelWidth + CellWidth * column,
        top = Margin + RowHeight * row,
        right = Margin + LabelWidth + CellWidth * (column + 1),
        bottom = Margin + RowHeight * (row + 1),
    };

    private static Win32.RECT RowLabel(int row) => new()
    {
        left = Margin,
        top = Margin + RowHeight * (row + 1),
        right = Margin + LabelWidth,
        bottom = Margin + RowHeight * (row + 2),
    };

    /// <summary>Une touche centrée dans sa cellule, à la taille réelle d'une touche à 96 DPI.</summary>
    private static Win32.RECT KeyRect(int column, int row)
    {
        var cell = Cell(column, row);
        int x = cell.left + (CellWidth - KeySize) / 2;
        int y = cell.top + (RowHeight - KeySize) / 2;
        return new Win32.RECT { left = x, top = y, right = x + KeySize, bottom = y + KeySize };
    }

    private static void Text(IntPtr hdc, Win32.RECT rect, string text, IntPtr font, uint color)
    {
        IntPtr previous = Win32.SelectObject(hdc, font);
        Win32.SetBkMode(hdc, Win32.TRANSPARENT);
        Win32.SetTextColor(hdc, color);
        var box = rect;
        Win32.DrawTextW(hdc, text, text.Length, ref box,
            Win32.DT_LEFT | Win32.DT_VCENTER | Win32.DT_WORDBREAK);
        Win32.SelectObject(hdc, previous);
    }
}
