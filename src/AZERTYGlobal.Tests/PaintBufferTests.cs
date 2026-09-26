using System;
using System.Runtime.InteropServices;
using AZERTYGlobal;
using Xunit;

namespace AZERTYGlobal.Tests;

/// <summary>
/// Audit du 25/09, lot 8 (V-16) — le double tampon commun, <see cref="PaintBuffer"/>. Le rendu
/// des six fenêtres qui l'emploient se vérifie au banc de captures ; ici, la surface
/// mémoire et le repli quand GDI la refuse. Aucune fenêtre ne s'affiche : le DC est celui de
/// l'écran, ou d'une fenêtre « message-only ».
/// </summary>
public class PaintBufferTests
{
    private static readonly IntPtr HWND_MESSAGE = new(-3);
    private const int OBJ_MEMDC = 10;

    [Fact]
    public void Surface_DcMémoireAvecSonBitmap()
    {
        var (dc, bitmap, previous) = PaintBuffer.CreateSurface(40, 20);
        try
        {
            Assert.Equal(OBJ_MEMDC, GetObjectType(dc));
            Assert.NotEqual(IntPtr.Zero, bitmap);
            Assert.NotEqual(IntPtr.Zero, previous);
            Assert.Equal(bitmap, GetCurrentObject(dc, 7)); // OBJ_BITMAP
        }
        finally
        {
            PaintBuffer.ReleaseSurface(dc, bitmap, previous);
        }
    }

    [Fact]
    public void Surface_RefuséeParGdi_RienÀLibérer()
    {
        // 100 000 × 100 000 en 32 bits : 40 Go, GDI refuse le bitmap. Le DC mémoire déjà créé
        // est rendu, et l'appelant dessine directement dans le DC de la fenêtre.
        var surface = PaintBuffer.CreateSurface(100_000, 100_000);
        Assert.Equal((IntPtr.Zero, IntPtr.Zero, IntPtr.Zero), surface);
    }

    [Fact]
    public void PeintureDUneFenêtre_DansLeTamponPuisÀLÉcran()
    {
        string name = "AZERTYGlobal.Tests.Peinture." + Guid.NewGuid().ToString("N");
        Win32.WNDPROC procédure = (h, m, w, l) => Win32.DefWindowProcW(h, m, w, l);
        Assert.True(NativeWindow.RegisterClass(name, procédure));
        IntPtr hwnd = Win32.CreateWindowExW(0, name, string.Empty, 0x80000000 /* WS_POPUP */,
            0, 0, 64, 32, HWND_MESSAGE, IntPtr.Zero, Win32.GetModuleHandleW(null), IntPtr.Zero);
        try
        {
            Assert.NotEqual(IntPtr.Zero, hwnd);
            var paint = new PaintBuffer(hwnd);
            Assert.True(paint.IsBuffered);
            Assert.Equal((64, 32), (paint.Client.right, paint.Client.bottom));
            Assert.Equal(OBJ_MEMDC, GetObjectType(paint.Hdc));
            // Un second Dispose ne rend rien deux fois. (Que le DC mémoire soit bien rendu,
            // GetObjectType ne le dit pas : GDI recycle aussitôt le handle, mesuré le 26/09.)
            paint.Dispose();
            paint.Dispose();
        }
        finally
        {
            Win32.DestroyWindow(hwnd);
            NativeWindow.UnregisterClass(name);
            GC.KeepAlive(procédure);
        }
    }

    [DllImport("gdi32.dll")]
    private static extern int GetObjectType(IntPtr h);

    [DllImport("gdi32.dll")]
    private static extern IntPtr GetCurrentObject(IntPtr hdc, uint type);
}
