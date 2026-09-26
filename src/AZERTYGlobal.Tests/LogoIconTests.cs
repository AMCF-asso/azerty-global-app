using System;
using System.Runtime.InteropServices;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit du 25/09, lot 8 (F-15) — l'icône du logo, une seule fabrique pour À propos, l'accueil
/// et le tray (<see cref="GdiHelpers.CreateLogoIcon"/>). Aucune fenêtre : l'icône est rendue
/// en mémoire puis détruite.
/// </summary>
public class LogoIconTests
{
    [Fact]
    public void SansLogo_AucuneIcône()
    {
        // GDI+ démarré : sans lui, la première étape échouerait de toute façon et le témoin
        // ne verrait pas la garde. Avec lui, une image absente donnerait une icône vide.
        var input = new Win32.GdiplusStartupInput { GdiplusVersion = 1 };
        Assert.Equal(0, Win32.GdiplusStartup(out IntPtr token, ref input, IntPtr.Zero));
        try
        {
            Assert.Equal(IntPtr.Zero, GdiHelpers.CreateLogoIcon(IntPtr.Zero, 32));
        }
        finally
        {
            Win32.GdiplusShutdown(token);
        }
    }

    [Theory]
    [InlineData(32)]
    [InlineData(40)] // grande icône à 125 % : 40 n'est pas un multiple de 16, le masque s'aligne
    public void Logo_IcôneÀLaTailleDemandée(int size)
    {
        var input = new Win32.GdiplusStartupInput { GdiplusVersion = 1 };
        Assert.Equal(0, Win32.GdiplusStartup(out IntPtr token, ref input, IntPtr.Zero));
        IntPtr logo = GdiImageLoader.LoadFromEmbeddedResource(typeof(AboutWindow), ProductIdentity.LogoResourceName);
        try
        {
            Assert.NotEqual(IntPtr.Zero, logo);
            IntPtr icon = GdiHelpers.CreateLogoIcon(logo, size);
            Assert.NotEqual(IntPtr.Zero, icon);
            try
            {
                Assert.True(GetIconInfo(icon, out var info));
                try
                {
                    Assert.Equal(Marshal.SizeOf<BITMAP>(), GetObjectW(info.hbmColor, Marshal.SizeOf<BITMAP>(), out var color));
                    Assert.Equal((size, size), (color.bmWidth, color.bmHeight));
                }
                finally
                {
                    Win32.DeleteObject(info.hbmColor);
                    Win32.DeleteObject(info.hbmMask);
                }
            }
            finally
            {
                Win32.DestroyIcon(icon);
            }
        }
        finally
        {
            if (logo != IntPtr.Zero) Win32.GdipDisposeImage(logo);
            Win32.GdiplusShutdown(token);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        [MarshalAs(UnmanagedType.Bool)] public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetIconInfo(IntPtr hIcon, out IconInfo info);

    [DllImport("gdi32.dll")]
    private static extern int GetObjectW(IntPtr h, int size, out BITMAP bitmap);
}
