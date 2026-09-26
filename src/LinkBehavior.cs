// Liens STATIC cliquables — audit du 25/09, lot 8 (F-10).
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Sous-classe commune des liens STATIC (SS_NOTIFY) d'À propos, des Statistiques et de
/// l'accueil : survol, Entrée qui active le lien, Échap qui ferme la fenêtre, et Tab rendu
/// à la navigation de dialogue. Les trois copies avaient divergé ; les écarts qui se voient
/// ou s'entendent restent des réglages explicites, pour que rien ne change :
/// <list type="bullet">
/// <item><c>focusFrame</c> : repeint au focus et cadre de focus (K5). À propos et l'accueil
/// l'ont, les Statistiques pas encore.</item>
/// <item><c>staticDialogCode</c> : WM_GETDLGCODE part de la réponse du STATIC (À propos,
/// Statistiques) ou de zéro (accueil).</item>
/// <item><c>swallow</c> : messages d'entrée ignorés, pendant que l'accueil a mis sa saisie en
/// pause.</item>
/// </list>
/// La couleur du lien reste décidée par la fenêtre, dans WM_CTLCOLORSTATIC, par
/// <see cref="IsActive"/>.
/// </summary>
internal sealed class LinkBehavior
{
    private const int VK_RETURN = 0x0D;
    private const int VK_ESCAPE = 0x1B;
    private const int IDC_HAND = 32649;

    // Gardé vivant tant que la sous-classe est posée : Windows n'en tient qu'un pointeur.
    private readonly Win32.SUBCLASSPROC _proc;
    private readonly List<(IntPtr Link, UIntPtr Id)> _links = new();
    private readonly Func<IntPtr> _owner;
    private readonly Action _onEscape;
    private readonly bool _focusFrame;
    private readonly bool _staticDialogCode;
    private readonly Func<uint, bool>? _swallow;

    /// <param name="owner">Fenêtre qui reçoit le WM_COMMAND d'un lien activé à Entrée.</param>
    /// <param name="onEscape">Échap sur un lien : le lien garde la touche (WANTALLKEYS),
    /// IsDialogMessageW n'en fait donc pas un IDCANCEL.</param>
    public LinkBehavior(Func<IntPtr> owner, Action onEscape, bool focusFrame,
        bool staticDialogCode = true, Func<uint, bool>? swallow = null)
    {
        _proc = SubclassProc;
        _owner = owner;
        _onEscape = onEscape;
        _focusFrame = focusFrame;
        _staticDialogCode = staticDialogCode;
        _swallow = swallow;
    }

    /// <summary>Lien sous le pointeur, IntPtr.Zero sinon.</summary>
    public IntPtr Hovered { get; private set; }

    /// <summary>Survolé ou focalisé : la couleur de survol, dans WM_CTLCOLORSTATIC.</summary>
    public bool IsActive(IntPtr link) => link != IntPtr.Zero && (Hovered == link || Win32.GetFocus() == link);

    public bool Contains(IntPtr hwnd) => hwnd != IntPtr.Zero && _links.Exists(l => l.Link == hwnd);

    /// <summary>WM_SETCURSOR : la main au-dessus d'un lien. Rend vrai quand le curseur est posé.</summary>
    public bool TrySetHandCursor(IntPtr hwnd)
    {
        if (!Contains(hwnd))
            return false;
        Win32.SetCursor(Win32.LoadCursorW(IntPtr.Zero, (IntPtr)IDC_HAND));
        return true;
    }

    /// <summary>Pose la sous-classe sur un lien, sous l'identifiant de sous-classe donné.</summary>
    public void Attach(IntPtr link, uint subclassId)
    {
        if (link == IntPtr.Zero)
            return;
        Win32.SetWindowSubclass(link, _proc, (UIntPtr)subclassId, IntPtr.Zero);
        _links.Add((link, (UIntPtr)subclassId));
    }

    /// <summary>Retire les sous-classes, avant la destruction de la fenêtre.</summary>
    public void Detach()
    {
        foreach (var (link, id) in _links)
            Win32.RemoveWindowSubclass(link, _proc, id);
        _links.Clear();
        Hovered = IntPtr.Zero;
    }

    private IntPtr SubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        if (_swallow != null && _swallow(msg))
            return IntPtr.Zero;

        switch (msg)
        {
            case Win32.WM_MOUSEMOVE:
                if (Hovered != hWnd)
                {
                    Hovered = hWnd;
                    Win32.InvalidateRect(hWnd, IntPtr.Zero, true);
                    var tme = new Win32.TRACKMOUSEEVENT
                    {
                        cbSize = (uint)Marshal.SizeOf<Win32.TRACKMOUSEEVENT>(),
                        dwFlags = Win32.TME_LEAVE,
                        hwndTrack = hWnd
                    };
                    Win32.TrackMouseEvent(ref tme);
                }
                break;
            case Win32.WM_MOUSELEAVE:
                if (Hovered == hWnd)
                {
                    Hovered = IntPtr.Zero;
                    Win32.InvalidateRect(hWnd, IntPtr.Zero, true);
                }
                break;
            case Win32.WM_GETDLGCODE:
            {
                // Revue du 2026-09-21, R2 : DLGC_WANTALLKEYS inconditionnel gardait Tab aussi,
                // et le focus ne sortait plus jamais d'un lien. Tab est rendu à IsDialogMessageW.
                long baseCode = _staticDialogCode ? Win32.DefSubclassProc(hWnd, msg, wParam, lParam).ToInt64() : 0;
                return (IntPtr)DialogNavigation.DialogCodeKeepingTab(baseCode, lParam);
            }
            case Win32.WM_KEYDOWN:
                if (wParam == (IntPtr)VK_RETURN)
                {
                    int ctrlId = Win32.GetDlgCtrlID(hWnd);
                    Win32.SendMessageW(_owner(), Win32.WM_COMMAND, (IntPtr)ctrlId, hWnd);
                    return IntPtr.Zero;
                }
                if (wParam == (IntPtr)VK_ESCAPE)
                {
                    _onEscape();
                    return IntPtr.Zero;
                }
                break;
            // K5 (accessibilité 1.3.0) : la couleur de focus se décide dans WM_CTLCOLORSTATIC,
            // qui ne passe qu'au repeint : sans repeint au changement de focus, le lien focalisé
            // ne se distinguait pas au clavier. Repeint, et cadre de focus.
            case Win32.WM_SETFOCUS:
            case Win32.WM_KILLFOCUS:
                if (_focusFrame)
                    Win32.InvalidateRect(hWnd, IntPtr.Zero, true);
                break;
            case Win32.WM_PAINT:
                if (_focusFrame)
                    return GdiHelpers.PaintLinkWithFocusRect(hWnd, msg, wParam, lParam);
                break;
        }
        return Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
    }
}
