using System;
using System.Linq;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit 1.3.0, A11 : l'icône de la zone de notification est le logo du produit, avec ses
/// marques d'état (grisé, Verr. Maj., suspension). Les pixels se composent sans GDI ; le
/// logo réel et l'icône finale sont rendus en mémoire puis détruits.
/// </summary>
public class TrayIconTests
{
    private const int Size = 16;
    private const uint Navy = 0xFF000C45;

    /// <summary>Logo d'essai : disque bleu nuit opaque, coins transparents.</summary>
    private static uint[] Disc()
    {
        var pixels = new uint[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                double dx = x + 0.5 - Size / 2.0, dy = y + 0.5 - Size / 2.0;
                pixels[y * Size + x] = dx * dx + dy * dy <= Size * Size / 4.0 ? Navy : 0;
            }
        return pixels;
    }

    private static uint Pixel(uint[] p, int x, int y) => p[y * Size + x];

    [Fact]
    public void Actif_LeLogoTelQuel()
    {
        var logo = Disc();
        Assert.Equal(logo, TrayIcon.Compose(logo, Size, active: true, capsLock: false, suspended: false));
    }

    [Fact]
    public void Désactivé_GriséSansToucherÀLaTransparence()
    {
        var icon = TrayIcon.Compose(Disc(), Size, active: false, capsLock: false, suspended: false);
        uint center = Pixel(icon, 8, 8);
        Assert.Equal(0xFFu, center >> 24);
        Assert.Equal((center >> 16) & 0xFF, (center >> 8) & 0xFF);
        Assert.Equal((center >> 8) & 0xFF, center & 0xFF);
        // Ramené vers le gris moyen : le bleu nuit ne devient pas noir.
        Assert.InRange(center & 0xFF, 60u, 90u);
        Assert.Equal(0u, Pixel(icon, 0, 0));
    }

    [Fact]
    public void VerrMaj_BarreOpaqueEnBas_HautIntact()
    {
        var logo = Disc();
        var icon = TrayIcon.Compose(logo, Size, active: true, capsLock: true, suspended: false);
        // GDI aurait écrit un alpha nul : la barre doit être opaque jusque dans les coins.
        Assert.Equal(TrayIcon.Argb(PaperTheme.Ink), Pixel(icon, 0, Size - 1));
        Assert.Equal(TrayIcon.Argb(PaperTheme.Step1Fill), Pixel(icon, 8, Size - 2));
        Assert.Equal(logo.Take(Size * 8), icon.Take(Size * 8));
    }

    [Fact]
    public void Suspendu_PastilleRougeEnBasÀDroite()
    {
        var icon = TrayIcon.Compose(Disc(), Size, active: false, capsLock: false, suspended: true);
        double radius = 7 / 2.0;
        int center = (int)(Size - radius);
        Assert.Equal(TrayIcon.Argb(PaperTheme.Error), Pixel(icon, center, center));
        Assert.Equal(0u, Pixel(icon, 0, 0));
        Assert.Equal(0u, Pixel(icon, Size - 1, 0));
    }

    [Fact]
    public void LogoRéel_CoinsTransparentsCentreOpaque()
    {
        var logo = TrayIcon.LogoPixels(Size);
        Assert.NotNull(logo);
        Assert.Equal(0u, logo![0] >> 24);
        Assert.Equal(0xFFu, logo[8 * Size + 8] >> 24);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(24)]
    [InlineData(32)]
    public void IcôneFinale_ÀChaqueTaille(int size)
    {
        IntPtr icon = TrayIcon.Create(active: true, capsLock: true, suspended: false, size);
        Assert.NotEqual(IntPtr.Zero, icon);
        Win32.DestroyIcon(icon);
    }
}
