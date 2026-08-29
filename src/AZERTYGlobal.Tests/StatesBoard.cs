using System;
using System.IO;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Planche des états des contrôles owner-draw — arrêt visuel du chantier CH1.
///
/// Le banc de captures rend des fenêtres, donc il ne peut montrer que l'état au repos : aucune
/// souris ne survole un bouton dans un processus de test, et aucune touche ne lui donne le focus.
/// Or la table d'états du §6 de l'audit est marquée « candidate », et le point qu'Antoine doit
/// trancher en premier — le survol d'un bouton primaire, dont le fond est déjà l'accent — est
/// précisément celui qu'aucune capture de fenêtre ne montre.
///
/// Cette planche appelle les primitives de <see cref="ThemeControls"/> directement, état par
/// état, sur un bitmap mémoire. Elle ne prouve rien : elle donne à voir ce que le code dessine.
///
/// Fermée par AZERTY_STATES, comme le banc l'est par AZERTY_CAPTURE.
/// </summary>
public class StatesBoard
{
    private const int Dpi = 96;
    private const int Margin = 24;
    private const int RowHeight = 56;
    private const int CellWidth = 190;
    private const int LabelWidth = 150;

    [Fact]
    public void RendLaPlancheDesEtats()
    {
        string? outDir = Environment.GetEnvironmentVariable("AZERTY_STATES");
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
                    Render(Theme.Current, Path.Combine(outDir, $"etats-{nom}.png"));
                }
            }
        }
        finally
        {
            if (token != IntPtr.Zero)
                Win32.GdiplusShutdown(token);
        }
    }

    private static readonly (string Label, ControlState State)[] States =
    {
        ("repos", ControlState.None),
        ("survolé", ControlState.Hovered),
        ("enfoncé", ControlState.Pressed),
        ("focus clavier", ControlState.Focused),
        ("désactivé", ControlState.Disabled),
    };

    private static void Render(Palette palette, string file)
    {
        var rows = new[]
        {
            "Bouton primaire",
            "Bouton secondaire",
            "Case à cocher",
            "Case cochée",
            "Bouton radio",
            "Compteur — et +",
            "Lien",
        };

        int width = Margin * 2 + LabelWidth + CellWidth * States.Length;
        int height = Margin * 2 + RowHeight * (rows.Length + 1);

        IntPtr hdcScreen = Win32.GetDC(IntPtr.Zero);
        IntPtr hdc = Win32.CreateCompatibleDC(hdcScreen);
        IntPtr bmp = Win32.CreateCompatibleBitmap(hdcScreen, width, height);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);
        IntPtr previous = Win32.SelectObject(hdc, bmp);

        var full = new Win32.RECT { left = 0, top = 0, right = width, bottom = height };
        Win32.FillRect(hdc, ref full, Theme.Brush(palette.Paper));

        IntPtr body = Theme.Font(FontRole.Body, Dpi);
        IntPtr caption = Theme.Font(FontRole.Secondary, Dpi);

        // En-tête : le nom de chaque état, au-dessus de sa colonne.
        for (int c = 0; c < States.Length; c++)
        {
            var head = Cell(c, 0);
            Text(hdc, head, States[c].Label, caption, palette.TextSecondary);
        }

        for (int r = 0; r < rows.Length; r++)
        {
            var label = new Win32.RECT
            {
                left = Margin,
                top = Margin + RowHeight * (r + 1),
                right = Margin + LabelWidth,
                bottom = Margin + RowHeight * (r + 2),
            };
            Text(hdc, label, rows[r], caption, palette.TextSecondary);

            for (int c = 0; c < States.Length; c++)
            {
                var cell = Inset(Cell(c, r + 1));
                var state = States[c].State;

                switch (r)
                {
                    case 0:
                        ThemeControls.DrawButton(hdc, cell, "Mettre en pause", body,
                            ButtonKind.Primary, state, palette, Dpi);
                        break;
                    case 1:
                        ThemeControls.DrawButton(hdc, cell, "Fermer", body,
                            ButtonKind.Secondary, state, palette, Dpi);
                        break;
                    case 2:
                        ThemeControls.DrawCheckBox(hdc, cell, "Au démarrage", body,
                            state, palette, Dpi);
                        break;
                    case 3:
                        ThemeControls.DrawCheckBox(hdc, cell, "Au démarrage", body,
                            state | ControlState.Checked, palette, Dpi);
                        break;
                    case 4:
                        ThemeControls.DrawRadio(hdc, cell, "Français", body,
                            state | (c == 0 ? ControlState.Checked : ControlState.None),
                            palette, Dpi);
                        break;
                    case 5:
                        // Les deux compteurs partagent la cellule : ils se lisent en paire, et
                        // c'est leur taille l'un par rapport au champ qui est en jeu.
                        ThemeControls.DrawStepperButton(hdc, Square(cell, 0), state, palette,
                            Dpi, adding: false);
                        ThemeControls.DrawStepperButton(hdc, Square(cell, 1), state, palette,
                            Dpi, adding: true);
                        break;
                    default:
                        ThemeControls.DrawLink(hdc, cell, "Code source GitHub", body,
                            state, palette, Dpi);
                        break;
                }
            }
        }

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

    /// <summary>Un carré de la hauteur d'un champ, place <paramref name="index"/> dans la
    /// cellule — c'est la taille réelle d'un compteur à 96 DPI.</summary>
    private static Win32.RECT Square(Win32.RECT cell, int index) => new()
    {
        left = cell.left + 6 + index * 44,
        top = cell.top,
        right = cell.left + 6 + index * 44 + 28,
        bottom = cell.top + 28,
    };

    /// <summary>L'anneau de focus déborde de 4 px : la cellule lui réserve sa marge.</summary>
    private static Win32.RECT Inset(Win32.RECT rect) => new()
    {
        left = rect.left + 12,
        top = rect.top + 12,
        right = rect.right - 12,
        bottom = rect.bottom - 12,
    };

    private static void Text(IntPtr hdc, Win32.RECT rect, string text, IntPtr font, uint color)
    {
        IntPtr previous = Win32.SelectObject(hdc, font);
        Win32.SetBkMode(hdc, Win32.TRANSPARENT);
        Win32.SetTextColor(hdc, color);
        var box = rect;
        Win32.DrawTextW(hdc, text, text.Length, ref box, Win32.DT_LEFT | Win32.DT_VCENTER
            | Win32.DT_SINGLELINE);
        Win32.SelectObject(hdc, previous);
    }
}
