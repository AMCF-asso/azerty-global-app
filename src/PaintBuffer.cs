// Double tampon de WM_PAINT — audit du 25/09, lot 8 (V-16, F-01).
namespace AZERTYGlobal;

/// <summary>
/// BeginPaint, un DC mémoire de la taille de la zone cliente, puis à la fin la copie à l'écran
/// et EndPaint, dans un <c>using</c> :
/// <code>
/// using var paint = new PaintBuffer(hWnd);
/// var hdc = paint.Hdc;   // on dessine ici
/// </code>
/// Six fenêtres recopiaient ces dix lignes. Les mêmes appels, dans le même ordre : le rendu
/// ne change pas. Deux choses s'ajoutent : EndPaint passe même si le dessin lève une exception
/// (sans lui, Windows renvoie WM_PAINT sans fin), et un bitmap que GDI refuse (fenêtre
/// immense, mémoire épuisée) fait dessiner directement à l'écran au lieu de copier un DC vide.
/// </summary>
internal sealed class PaintBuffer : IDisposable
{
    private readonly IntPtr _hWnd;
    private Win32.PAINTSTRUCT _ps;
    private readonly IntPtr _target;
    private readonly IntPtr _bitmap;
    private readonly IntPtr _previous;
    private bool _disposed;

    public PaintBuffer(IntPtr hWnd)
    {
        _hWnd = hWnd;
        _target = Win32.BeginPaint(hWnd, out _ps);
        Win32.GetClientRect(hWnd, out var client);
        Client = client;

        (IntPtr dc, _bitmap, _previous) = CreateSurface(client.right, client.bottom);
        Hdc = dc != IntPtr.Zero ? dc : _target;
    }

    /// <summary>Où dessiner : le DC mémoire, ou le DC de la fenêtre quand GDI a refusé le bitmap.</summary>
    public IntPtr Hdc { get; }

    /// <summary>Zone cliente, origine en (0, 0).</summary>
    public Win32.RECT Client { get; }

    public bool IsBuffered => _bitmap != IntPtr.Zero;

    /// <summary>
    /// DC mémoire compatible avec l'écran, son bitmap sélectionné, et l'objet qu'il remplace.
    /// Tout à zéro quand GDI refuse l'un ou l'autre : rien n'est alors à libérer.
    /// </summary>
    internal static (IntPtr Dc, IntPtr Bitmap, IntPtr Previous) CreateSurface(int width, int height)
    {
        IntPtr screen = Win32.GetDC(IntPtr.Zero);
        IntPtr dc = Win32.CreateCompatibleDC(screen);
        IntPtr bitmap = dc != IntPtr.Zero ? Win32.CreateCompatibleBitmap(screen, width, height) : IntPtr.Zero;
        Win32.ReleaseDC(IntPtr.Zero, screen);

        if (bitmap == IntPtr.Zero)
        {
            if (dc != IntPtr.Zero)
                Win32.DeleteDC(dc);
            return (IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        }
        return (dc, bitmap, Win32.SelectObject(dc, bitmap));
    }

    /// <summary>Rend ce que <see cref="CreateSurface"/> a pris.</summary>
    internal static void ReleaseSurface(IntPtr dc, IntPtr bitmap, IntPtr previous)
    {
        if (bitmap == IntPtr.Zero)
            return;
        Win32.SelectObject(dc, previous);
        Win32.DeleteObject(bitmap);
        Win32.DeleteDC(dc);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (IsBuffered)
            Win32.BitBlt(_target, 0, 0, Client.right, Client.bottom, Hdc, 0, 0, Win32.SRCCOPY);
        ReleaseSurface(Hdc, _bitmap, _previous);
        Win32.EndPaint(_hWnd, ref _ps);
    }
}
