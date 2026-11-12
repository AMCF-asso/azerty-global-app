using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Clavier virtuel, touche morte Symboles divers : l'avion, l'étoile et les autres pictogrammes
/// (✈ ★ ☎ ⚠ ✅…) sortaient de Segoe UI Emoji ou Symbol à la hauteur du texte, donc à pleine
/// taille de leur em : plus larges que la touche, quand les lettres et les flèches voisines
/// restent à un quart de touche. Ce témoin peint chaque pictogramme de la disposition comme le
/// clavier virtuel (signe de touche morte, police titre, <see cref="PaperText.DrawLine"/>) dans
/// un bitmap en mémoire et mesure l'encre : elle tient dans la touche, à taille de signe.
/// </summary>
public class PictographFitTests
{
    private readonly ITestOutputHelper _out;
    private readonly Layout _layout = LayoutLoader.LoadFromResource();

    public PictographFitTests(ITestOutputHelper output) => _out = output;

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size; public int Width; public int Height; public ushort Planes; public ushort BitCount;
        public uint Compression; public uint SizeImage; public int XPels; public int YPels; public uint ClrUsed; public uint ClrImportant;
    }

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint lines, byte[]? bits,
        ref BitmapInfoHeader bmi, uint usage);

    /// <summary>Les sorties de toutes les touches mortes, telles que le clavier les montre.</summary>
    private IEnumerable<string> Outputs()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dead in _layout.DeadKeys.Values)
        {
            foreach (string? output in dead.Table.Values)
            {
                if (!string.IsNullOrEmpty(output) && !output.StartsWith("dk_", StringComparison.Ordinal) && seen.Add(output))
                    yield return output;
            }
        }
    }

    /// <summary>Boîte de l'encre (gauche, haut, droite, bas) de <paramref name="text"/> centré
    /// dans une touche carrée de <paramref name="key"/> pixels, en coordonnées de la touche.</summary>
    private static (int Left, int Top, int Right, int Bottom) Ink(IntPtr dc, IntPtr bitmap, IntPtr originalBitmap,
        string text, FontSpec font, int key, int margin)
    {
        int size = key + 2 * margin;
        var white = Win32.CreateSolidBrush(0x00FFFFFF);
        var all = new Win32.RECT { left = 0, top = 0, right = size, bottom = size };
        Win32.FillRect(dc, ref all, white);
        Win32.DeleteObject(white);

        var rect = new Win32.RECT { left = margin, top = margin, right = margin + key, bottom = margin + key };
        PaperText.DrawLine(dc, text, font, rect, 0, TextAlign.Center);

        var header = new BitmapInfoHeader { Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(), Width = size, Height = -size, Planes = 1, BitCount = 32 };
        var bits = new byte[size * size * 4];
        Win32.SelectObject(dc, originalBitmap); // GetDIBits exige un bitmap qui n'est dans aucun DC
        GetDIBits(dc, bitmap, 0, (uint)size, bits, ref header, 0);
        Win32.SelectObject(dc, bitmap);

        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int i = (y * size + x) * 4;
                if (bits[i] < 160 || bits[i + 1] < 160 || bits[i + 2] < 160)
                {
                    left = Math.Min(left, x); right = Math.Max(right, x);
                    top = Math.Min(top, y); bottom = Math.Max(bottom, y);
                }
            }
        }
        return right < 0 ? (0, 0, -1, -1) : (left - margin, top - margin, right - margin, bottom - margin);
    }

    [Theory]
    [InlineData(32)]
    [InlineData(50)]
    [InlineData(80)]
    public void ChaquePictogrammeTientDansSaTouche(int key)
    {
        // Signe de touche morte de DrawActiveSign : 28 px pour une touche de 50.
        int height = (int)(28 * key / 50f);
        var font = FontSpec.Of(AppFonts.Title, height, TypeRamp.Semibold, 1f);
        const int margin = 40;

        IntPtr screen = Win32.GetDC(IntPtr.Zero);
        IntPtr dc = Win32.CreateCompatibleDC(screen);
        IntPtr bitmap = Win32.CreateCompatibleBitmap(screen, key + 2 * margin, key + 2 * margin);
        Win32.ReleaseDC(IntPtr.Zero, screen);
        IntPtr oldBitmap = Win32.SelectObject(dc, bitmap);
        var failures = new List<string>();
        int measured = 0;
        try
        {
            // ␣ (U+2423, symbole « espace ») est un trait bas, petit par nature : hors mesure.
            foreach (string text in Outputs().Where(t => t.All(AppFonts.IsPictograph) && t != "␣"))
            {
                measured++;
                var ink = Ink(dc, bitmap, oldBitmap, text, font, key, margin);
                int w = ink.Right - ink.Left + 1, h = ink.Bottom - ink.Top + 1;
                string name = string.Join(" ", text.Select(c => $"U+{(int)c:X4}"));
                _out.WriteLine($"{name} : {(ink.Right < 0 ? "vide" : $"{w}×{h} à ({ink.Left},{ink.Top})")}");
                bool empty = ink.Right < 0;
                float side = empty ? 0 : Math.Max(w, h) / (float)key;
                // Dans la touche, et à taille de signe : ni plus d'une moitié de touche (l'avion
                // en prenait 1,3), ni un point perdu dans la touche (moins d'un quart).
                if (empty || side > 0.5f || side < 0.25f || ink.Left < 0 || ink.Top < 0 || ink.Right >= key || ink.Bottom >= key)
                    failures.Add($"{name} : encre {(empty ? "vide" : $"{w}×{h} à ({ink.Left},{ink.Top})")} pour une touche de {key}");
            }
        }
        finally
        {
            Win32.SelectObject(dc, oldBitmap);
            Win32.DeleteObject(bitmap);
            Win32.DeleteDC(dc);
        }

        Assert.True(measured >= 25, $"{measured} pictogrammes trouvés (31 mesurés le 2026-11-12)");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
