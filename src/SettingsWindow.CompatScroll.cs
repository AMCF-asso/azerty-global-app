// Défilement papier de la liste des applications — audit 1.3.0, A16 (lot 3a « fenêtres »).
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// La liste Paramètres › Applications portait la barre de défilement grise du système
/// (WS_VSCROLL), seule pièce non papier de l'onglet. Elle est retirée ; la fenêtre peint à
/// sa place une piste blanche et un curseur de 4 px, comme la liste de la recherche. Ce qui
/// défilait reste : flèches et Page (la liste suit sa sélection), molette (sous-classe),
/// clic et glisser sur la piste. Les lignes restent des éléments de liste pour le Narrateur.
/// </summary>
sealed partial class SettingsWindow
{
    private const uint LB_GETTOPINDEX = 0x018E;
    private const uint LB_SETTOPINDEX = 0x0197;
    private const uint WM_CAPTURECHANGED = 0x0215;

    [DllImport("user32.dll")]
    private static extern IntPtr SetCapture(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", EntryPoint = "InvalidateRect")]
    private static extern bool InvalidateArea(IntPtr hWnd, ref Win32.RECT rect, bool erase);

    private static readonly UIntPtr CompatListSubclassId = (UIntPtr)6;
    private Win32.SUBCLASSPROC? _compatListSubclassProc;
    private int _compatTopDrawn = -1;
    private bool _compatDragging;
    // Décalage entre le pointeur et le haut du curseur au début du glisser.
    private int _compatDragOffset;

    /// <summary>Largeur de la piste, prise à droite dans le cadre de la liste.</summary>
    private int CompatTrackWidth => S(10);

    /// <summary>Sous-classe la liste : molette, et suivi du défilement pour le curseur.</summary>
    private void AttachCompatListScroll()
    {
        if (_hWndCompatList == IntPtr.Zero) return;
        _compatListSubclassProc ??= CompatListSubclassProc;
        Win32.SetWindowSubclass(_hWndCompatList, _compatListSubclassProc, CompatListSubclassId, IntPtr.Zero);
    }

    private IntPtr CompatListSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
        UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (msg == Win32.WM_MOUSEWHEEL)
        {
            var (count, visible, top) = CompatScrollState();
            if (count > visible)
            {
                // Une ligne par cran : trois lignes visibles seulement.
                int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                int lines = -Math.Sign(delta) * Math.Max(1, Math.Abs(delta) / 120);
                SetCompatTop(top + lines);
                return IntPtr.Zero;
            }
            // Liste entière visible : la molette fait défiler la fenêtre.
        }
        if (msg == 0x0082 /* WM_NCDESTROY */)
            Win32.RemoveWindowSubclass(hWnd, _compatListSubclassProc!, uIdSubclass);

        IntPtr result = Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
        int now = (int)Win32.SendMessageW(hWnd, LB_GETTOPINDEX, IntPtr.Zero, IntPtr.Zero);
        if (now != _compatTopDrawn)
            InvalidateCompatTrack();
        return result;
    }

    /// <summary>Nombre de lignes, lignes visibles et première ligne visible.</summary>
    private (int Count, int Visible, int Top) CompatScrollState()
    {
        int count = (int)Win32.SendMessageW(_hWndCompatList, LB_GETCOUNT, IntPtr.Zero, IntPtr.Zero);
        Win32.GetClientRect(_hWndCompatList, out var client);
        int visible = Math.Max(1, (client.bottom - client.top) / Math.Max(1, CompatRowHeight));
        int top = (int)Win32.SendMessageW(_hWndCompatList, LB_GETTOPINDEX, IntPtr.Zero, IntPtr.Zero);
        return (Math.Max(0, count), visible, Math.Max(0, top));
    }

    private void SetCompatTop(int top)
    {
        var (count, visible, _) = CompatScrollState();
        top = Math.Max(0, Math.Min(top, Math.Max(0, count - visible)));
        Win32.SendMessageW(_hWndCompatList, LB_SETTOPINDEX, (IntPtr)top, IntPtr.Zero);
        InvalidateCompatTrack();
    }

    /// <summary>La piste, en coordonnées de contenu : à droite de la liste, dans son cadre.</summary>
    private static Win32.RECT CompatTrackRect(LayoutInfo layout) => new()
    {
        left = layout.CompatListRect.right,
        top = layout.CompatListRect.top,
        right = layout.CompatListFrame.right - 1,
        bottom = layout.CompatListRect.bottom,
    };

    /// <summary>Le curseur dans la piste, ou null quand toute la liste est visible.</summary>
    private Win32.RECT? CompatThumbRect(Win32.RECT track)
    {
        var (count, visible, top) = CompatScrollState();
        if (count <= visible) return null;
        int trackH = track.bottom - track.top - 2 * S(2);
        int thumbH = Math.Max(S(16), trackH * visible / count);
        int range = Math.Max(1, count - visible);
        int y = track.top + S(2) + (trackH - thumbH) * Math.Min(top, range) / range;
        int x = track.left + (track.right - track.left - S(4)) / 2;
        return new Win32.RECT { left = x, top = y, right = x + S(4), bottom = y + thumbH };
    }

    /// <summary>Piste et curseur, dessinés avec le reste de l'onglet (coordonnées de contenu).</summary>
    private void DrawCompatScroll(IntPtr hdc, LayoutInfo layout)
    {
        var track = CompatTrackRect(layout);
        PaperDraw.Fill(hdc, track, CLR_SURFACE);
        if (CompatThumbRect(track) is { } thumb)
            PaperDraw.RoundBox(hdc, new Win32.RECT { left = thumb.left, top = thumb.top, right = thumb.right - 1, bottom = thumb.bottom - 1 },
                CLR_PALE, CLR_PALE, S(2));
        _compatTopDrawn = CompatScrollState().Top;
    }

    private void InvalidateCompatTrack()
    {
        if (_hWnd == IntPtr.Zero || _activeTab != SettingsTab.Applications) return;
        var track = CompatTrackRect(GetLayout(S(BASE_WIN_W)));
        var client = new Win32.RECT { left = track.left, top = track.top - _scrollY, right = track.right, bottom = track.bottom - _scrollY };
        InvalidateArea(_hWnd, ref client, false);
    }

    /// <summary>Clic et glisser sur la piste. Rend vrai si le message est consommé.</summary>
    private bool OnCompatTrackMouse(uint msg, IntPtr lParam)
    {
        int x = (short)(lParam.ToInt64() & 0xFFFF);
        int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF) + _scrollY;
        switch (msg)
        {
            case Win32.WM_LBUTTONDOWN:
            {
                if (_activeTab != SettingsTab.Applications) return false;
                var track = CompatTrackRect(GetLayout(S(BASE_WIN_W)));
                if (x < track.left || x >= track.right || y < track.top || y >= track.bottom) return false;
                if (CompatThumbRect(track) is not { } thumb) return true;
                // Hors du curseur, il saute sous le pointeur, puis suit le glisser.
                _compatDragOffset = y >= thumb.top && y < thumb.bottom ? y - thumb.top : (thumb.bottom - thumb.top) / 2;
                _compatDragging = true;
                SetCapture(_hWnd);
                DragCompatThumb(track, y);
                return true;
            }
            case Win32.WM_MOUSEMOVE:
                if (!_compatDragging) return false;
                DragCompatThumb(CompatTrackRect(GetLayout(S(BASE_WIN_W))), y);
                return true;
            case Win32.WM_LBUTTONUP:
                if (!_compatDragging) return false;
                _compatDragging = false;
                ReleaseCapture();
                return true;
            case WM_CAPTURECHANGED:
                _compatDragging = false;
                return false;
        }
        return false;
    }

    private void DragCompatThumb(Win32.RECT track, int y)
    {
        if (CompatThumbRect(track) is not { } thumb) return;
        var (count, visible, _) = CompatScrollState();
        int thumbH = thumb.bottom - thumb.top;
        int span = Math.Max(1, track.bottom - track.top - 2 * S(2) - thumbH);
        int offset = Math.Max(0, Math.Min(span, y - _compatDragOffset - track.top - S(2)));
        SetCompatTop((int)Math.Round(offset * (double)(count - visible) / span));
    }
}
