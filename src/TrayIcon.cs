// Icône de la zone de notification : le logo du produit et ses marques d'état.
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Audit 1.3.0, A11 : l'icône était un carré bleu Windows #0078D4 marqué « AG » en Segoe UI,
/// dessiné à 32 px puis réduit par Windows, illisible à 16 px. Elle devient le logo du
/// produit, rendu à la taille de la zone de notification. Les états gardent leurs marques :
/// logo grisé quand l'app ne tape pas, barre jaune sous le logo pour Verr. Maj., pastille
/// rouge pour une suspension anti-triche. Les pixels se composent en mémoire : GDI écrit un
/// alpha nul sur une image 32 bits, la barre et la pastille y seraient transparentes.
/// </summary>
internal static class TrayIcon
{
    // GetSystemMetrics : taille d'une petite icône, celle de la zone de notification ; suit
    // l'échelle d'affichage (16 px à 100 %, 32 px à 200 %).
    private const int SM_CXSMICON = 49;
    // PixelFormat32bppARGB : alpha non prémultiplié, un pixel = 0xAARRGGBB.
    private const int PixelFormat32bppARGB = 0x0026200A;

    private static readonly Dictionary<int, uint[]?> LogoCache = new();

    /// <summary>
    /// L'icône de l'état donné à la taille de la zone de notification. Sans logo lisible,
    /// l'ancienne icône texte : la zone de notification n'est jamais laissée sans icône.
    /// </summary>
    internal static IntPtr Create(bool active, bool capsLock, bool suspended, int size = 0)
    {
        if (size <= 0) size = Math.Max(16, Win32.GetSystemMetrics(SM_CXSMICON));
        var logo = LogoPixels(size);
        IntPtr icon = logo == null ? IntPtr.Zero : FromPixels(Compose(logo, size, active, capsLock, suspended), size);
        return icon != IntPtr.Zero ? icon : TrayApplication.CreateTextIcon("AG", active, capsLock, suspended, size);
    }

    /// <summary>Le logo à <paramref name="size"/> px, décodé une fois par taille : l'icône se
    /// refait à chaque bascule de Verr. Maj. Null si un maillon GDI+ échoue.</summary>
    internal static uint[]? LogoPixels(int size)
    {
        lock (LogoCache)
        {
            if (LogoCache.TryGetValue(size, out var cached)) return cached;
            var pixels = RenderLogo(size);
            LogoCache[size] = pixels;
            return pixels;
        }
    }

    private static uint[]? RenderLogo(int size)
    {
        var input = new Win32.GdiplusStartupInput { GdiplusVersion = 1 };
        if (Win32.GdiplusStartup(out IntPtr token, ref input, IntPtr.Zero) != 0) return null;
        IntPtr logo = IntPtr.Zero, bitmap = IntPtr.Zero;
        var pixels = new uint[size * size];
        var pin = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            logo = GdiImageLoader.LoadFromEmbeddedResource(typeof(TrayIcon), ProductIdentity.LogoResourceName);
            if (logo == IntPtr.Zero) return null;
            if (Win32.GdipCreateBitmapFromScan0(size, size, size * 4, PixelFormat32bppARGB, pin.AddrOfPinnedObject(), out bitmap) != 0)
                return null;
            if (Win32.GdipGetImageGraphicsContext(bitmap, out IntPtr g) != 0) return null;
            Win32.GdipSetSmoothingMode(g, 4);
            Win32.GdipSetInterpolationMode(g, 7);
            int status = Win32.GdipDrawImageRectI(g, logo, 0, 0, size, size);
            Win32.GdipDeleteGraphics(g);
            return status == 0 ? pixels : null;
        }
        finally
        {
            if (bitmap != IntPtr.Zero) Win32.GdipDisposeImage(bitmap);
            if (logo != IntPtr.Zero) Win32.GdipDisposeImage(logo);
            pin.Free();
            Win32.GdiplusShutdown(token);
        }
    }

    /// <summary>
    /// Les pixels de l'icône (0xAARRGGBB, lignes de haut en bas) à partir de ceux du logo.
    /// Fonction pure : les tests l'éprouvent sans GDI.
    /// </summary>
    internal static uint[] Compose(uint[] logo, int size, bool active, bool capsLock, bool suspended)
    {
        var pixels = (uint[])logo.Clone();
        if (!active)
        {
            // Grisé et ramené vers le gris moyen : le bleu nuit du logo, devenu presque noir,
            // disparaîtrait sur la barre des tâches sombre.
            for (int i = 0; i < pixels.Length; i++)
            {
                uint p = pixels[i];
                if (p >> 24 == 0) continue;
                double luma = 0.299 * ((p >> 16) & 0xFF) + 0.587 * ((p >> 8) & 0xFF) + 0.114 * (p & 0xFF);
                uint grey = (uint)Math.Round(64 + luma * 0.6);
                pixels[i] = (p & 0xFF000000) | (grey << 16) | (grey << 8) | grey;
            }
        }

        if (capsLock)
        {
            // Barre jaune cernée d'encre : lisible sur la barre des tâches claire comme sombre.
            int height = Math.Max(4, size * 7 / 32);
            int border = Math.Max(1, size / 32);
            uint fill = Argb(PaperTheme.Step1Fill), ink = Argb(PaperTheme.Ink);
            for (int y = size - height; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool edge = y < size - height + border || y >= size - border || x < border || x >= size - border;
                    pixels[y * size + x] = edge ? ink : fill;
                }
        }

        if (suspended)
        {
            // Pastille rouge cerclée de blanc, en bas à droite, lissée sur 4 × 4 sous-pixels.
            double diameter = Math.Max(7, size * 14 / 32.0);
            double radius = diameter / 2, ring = Math.Max(1, size / 32.0);
            double cx = size - radius, cy = size - radius;
            uint red = Argb(PaperTheme.Error), white = Argb(PaperTheme.Surface);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int inner = 0, outer = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                        {
                            double dx = x + (sx + 0.5) / 4 - cx, dy = y + (sy + 0.5) / 4 - cy;
                            double d = Math.Sqrt(dx * dx + dy * dy);
                            if (d <= radius - ring) inner++;
                            else if (d <= radius) outer++;
                        }
                    if (inner + outer == 0) continue;
                    int i = y * size + x;
                    pixels[i] = Over(Over(pixels[i], white, outer / 16.0), red, inner / 16.0);
                }
        }
        return pixels;
    }

    /// <summary>COLORREF (0x00BBGGRR) en pixel opaque 0xAARRGGBB.</summary>
    internal static uint Argb(uint colorRef) =>
        0xFF000000 | ((colorRef & 0xFF) << 16) | (colorRef & 0xFF00) | ((colorRef >> 16) & 0xFF);

    /// <summary><paramref name="top"/> opaque posé sur <paramref name="under"/> avec la
    /// couverture <paramref name="coverage"/> (0 à 1), alpha non prémultiplié.</summary>
    private static uint Over(uint under, uint top, double coverage)
    {
        if (coverage <= 0) return under;
        double ua = (under >> 24) / 255.0;
        double a = coverage + ua * (1 - coverage);
        uint Channel(int shift)
        {
            double u = (under >> shift) & 0xFF, t = (top >> shift) & 0xFF;
            return (uint)Math.Round((t * coverage + u * ua * (1 - coverage)) / a);
        }
        return ((uint)Math.Round(a * 255) << 24) | (Channel(16) << 16) | (Channel(8) << 8) | Channel(0);
    }

    /// <summary>HICON 32 bits à alpha à partir des pixels ; zéro si un maillon échoue.</summary>
    private static IntPtr FromPixels(uint[] pixels, int size)
    {
        var input = new Win32.GdiplusStartupInput { GdiplusVersion = 1 };
        if (Win32.GdiplusStartup(out IntPtr token, ref input, IntPtr.Zero) != 0) return IntPtr.Zero;
        var pin = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        IntPtr bitmap = IntPtr.Zero;
        try
        {
            if (Win32.GdipCreateBitmapFromScan0(size, size, size * 4, PixelFormat32bppARGB, pin.AddrOfPinnedObject(), out bitmap) != 0)
                return IntPtr.Zero;
            if (Win32.GdipCreateHBITMAPFromBitmap(bitmap, out IntPtr color, 0) != 0) return IntPtr.Zero;
            // Masque tout opaque : l'alpha de l'image couleur décide.
            IntPtr mask = Win32.CreateBitmap(size, size, 1, 1, new byte[GdiHelpers.MonochromeMaskByteCount(size, size)]);
            var info = new Win32.ICONINFO { fIcon = true, hbmMask = mask, hbmColor = color };
            IntPtr icon = Win32.CreateIconIndirect(ref info);
            Win32.DeleteObject(mask);
            Win32.DeleteObject(color);
            return icon;
        }
        finally
        {
            if (bitmap != IntPtr.Zero) Win32.GdipDisposeImage(bitmap);
            pin.Free();
            Win32.GdiplusShutdown(token);
        }
    }
}
