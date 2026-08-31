using System;
using System.IO;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Nappe de clavier complète, rendue par <c>KeyboardRenderer.Draw</c> lui-même — pièce d'arrêt
/// visuel du chantier CH4.
///
/// La première version de cette planche dessinait ses touches elle-même, avec deux couches sur
/// quatre et une police à taille fixe : elle ne correspondait à aucun écran de l'application, et
/// ratait le seul point réellement dur — distinguer, parmi quatre caractères posés sur une
/// touche, celui que la frappe produira. L'application y employait quatre couleurs de glyphe,
/// la charte n'en offre que deux.
///
/// Celle-ci appelle le moteur du produit, dans le profil <c>Full</c> du clavier virtuel, avec les
/// cinq polices que la fenêtre Leçons lui passe (28 / 24 / 20 / 16 / 20). Aucun écart n'est donc
/// possible entre la pièce et ce que l'utilisateur verra : c'est le même code de peinture.
///
/// Trois états de modifieur sont empilés, parce que la couche active change avec eux et que c'est
/// justement ce que la palette doit rendre lisible.
///
/// PowerShell :
/// <code>
/// $env:AZERTY_PLANCHE = "D:\captures\ch4"
/// dotnet test src\AZERTYGlobal.Tests\AZERTYGlobal.Tests.csproj -c Debug --filter FullyQualifiedName~KeyboardPlank
/// </code>
/// </summary>
public class KeyboardPlank
{
    private const int Dpi = 96;
    private const int Margin = 28;
    private const int CaptionHeight = 30;

    /// <summary>Largeur visée du clavier. À cette largeur, les polices fixes de la fenêtre
    /// Leçons donnent la proportion caractère / touche qu'elle rend réellement.</summary>
    private const int KeyboardWidth = 1100;

    private static readonly (string Legende, Func<KeyboardRenderState> Etat)[] Bandes =
    {
        ("Aucun modifieur tenu — la couche de base est active",
            () => new KeyboardRenderState()),
        ("Maj tenu — la couche Maj devient active",
            () => new KeyboardRenderState { Shift = true }),
        ("AltGr tenu — la couche AltGr devient active",
            () => new KeyboardRenderState { AltGr = true }),
    };

    [Fact]
    public void RendLaNappeDeClavier()
    {
        string? outDir = Environment.GetEnvironmentVariable("AZERTY_PLANCHE");
        if (string.IsNullOrWhiteSpace(outDir))
            return;

        Directory.CreateDirectory(outDir);

        var layout = LayoutLoader.LoadFromResource();
        var input = new Win32.GdiplusStartupInput { GdiplusVersion = 1 };
        Win32.GdiplusStartup(out IntPtr token, ref input, IntPtr.Zero);
        try
        {
            foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                using (Theme.OverrideForTests(variant))
                {
                    string nom = variant == ThemeVariant.Light ? "clair" : "sombre";
                    Render(layout, Theme.Current,
                        Path.Combine(outDir, $"clavier-charte-{nom}.png"));
                }
            }
        }
        finally
        {
            if (token != IntPtr.Zero)
                Win32.GdiplusShutdown(token);
        }
    }

    private static void Render(Layout layout, Palette palette, string file)
    {
        float maxRight = 0f;
        float maxBottom = 0f;
        foreach (var k in KeyboardRenderer.VisualKeys)
        {
            maxRight = Math.Max(maxRight, k.X + k.W);
            maxBottom = Math.Max(maxBottom, k.Y + k.H);
        }

        int keyboardH = (int)(KeyboardWidth / maxRight * maxBottom);
        int bandHeight = CaptionHeight + keyboardH;
        int width = KeyboardWidth + Margin * 2;
        int height = Margin * 2 + bandHeight * Bandes.Length + Margin * (Bandes.Length - 1);

        IntPtr hdcScreen = Win32.GetDC(IntPtr.Zero);
        IntPtr hdc = Win32.CreateCompatibleDC(hdcScreen);
        IntPtr bmp = Win32.CreateCompatibleBitmap(hdcScreen, width, height);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);
        IntPtr previous = Win32.SelectObject(hdc, bmp);

        var full = new Win32.RECT { left = 0, top = 0, right = width, bottom = height };
        Win32.FillRect(hdc, ref full, Theme.Brush(palette.Paper));

        // Les cinq polices que LessonsWindow passe au moteur, aux mêmes tailles.
        IntPtr main = Win32.CreateFontW(28, 0, 0, 0, 600, 0, 0, 0, 0, 0, 0, 4, 0, "Consolas");
        IntPtr dead = Win32.CreateFontW(24, 0, 0, 0, 600, 0, 0, 0, 0, 0, 0, 4, 0, "Consolas");
        IntPtr small = Win32.CreateFontW(20, 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 4, 0, "Consolas");
        IntPtr tiny = Win32.CreateFontW(16, 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 4, 0, "Segoe UI");
        IntPtr ctx = Win32.CreateFontW(20, 0, 0, 0, 500, 0, 0, 0, 0, 0, 0, 4, 0, "Segoe UI");
        IntPtr caption = Theme.Font(FontRole.Secondary, Dpi);

        int y = Margin;
        foreach (var (legende, etat) in Bandes)
        {
            var legendeRect = new Win32.RECT
            {
                left = Margin,
                top = y,
                right = width - Margin,
                bottom = y + CaptionHeight,
            };
            Win32.SelectObject(hdc, caption);
            Win32.SetBkMode(hdc, Win32.TRANSPARENT);
            Win32.SetTextColor(hdc, palette.TextSecondary);
            Win32.DrawTextW(hdc, legende, legende.Length, ref legendeRect,
                Win32.DT_LEFT | Win32.DT_VCENTER | Win32.DT_SINGLELINE);

            var bounds = new Win32.RECT
            {
                left = Margin,
                top = y + CaptionHeight,
                right = Margin + KeyboardWidth,
                bottom = y + CaptionHeight + keyboardH,
            };
            KeyboardRenderer.Draw(hdc, bounds, layout, KeyboardRenderProfile.Full, etat(),
                main, dead, small, tiny, ctx);

            y += bandHeight + Margin;
        }

        Win32.SelectObject(hdc, previous);

        if (Win32.GdipCreateBitmapFromHBITMAP(bmp, IntPtr.Zero, out IntPtr image) == 0
            && image != IntPtr.Zero)
        {
            var encoder = Win32.PngEncoderClsid;
            Win32.GdipSaveImageToFile(image, file, ref encoder, IntPtr.Zero);
            Win32.GdipDisposeImage(image);
        }

        foreach (IntPtr font in new[] { main, dead, small, tiny, ctx })
            Win32.DeleteObject(font);
        Win32.DeleteObject(bmp);
        Win32.DeleteDC(hdc);
    }
}
