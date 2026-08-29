// Fenêtre « Mes statistiques » — statistiques d'usage 100 % locales (v1.1).
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Mini-fenêtre custom GDI affichant les statistiques d'usage locales (jours d'utilisation,
/// caractères spéciaux produits grâce au remapping) avec un bouton d'export presse-papiers.
/// Calquée sur AboutWindow pour le style (fond clair, double buffering, DPI-aware).
/// Aucune de ces données ne quitte jamais la machine (cf. UsageStats).
/// </summary>
sealed class UsageStatsWindow : IDisposable
{
    private const uint SS_NOTIFY = 0x0100;

    private const int IDC_BTN_COPY = 4201;
    private const int IDC_BTN_CLOSE = 4202;
    private const int IDC_LINK_FEEDBACK = 4203;
    private const int IDC_LINK_DISCORD = 4204;
    private const uint TIMER_COPY_FEEDBACK = 4210;

    private const int BASE_WIN_W = 460;
    private const int BASE_WIN_H = 460;
    // Section « Défi du jour » ajoutée en v1.2.0 : la fenêtre ne réserve sa hauteur
    // que lorsque la section se dessine (même prédicat que WM_PAINT, cf. plus bas).
    private const int CHALLENGE_SECTION_H = 130;
    private static bool ChallengeSectionVisible
        => ConfigManager.TrainingEnabled || UsageStats.ChallengesCompletedCount > 0;
    private static int WinContentH
        => BASE_WIN_H + (ChallengeSectionVisible ? CHALLENGE_SECTION_H : 0);

    // ── Couleurs : les jetons de la charte, relus à chaque bascule de thème ──────
    // L'accent et la couleur de lien de cette fenêtre étaient l'orange 0x00D47800, qui est le
    // bleu 0x000078D4 à octets inversés — le piège COLORREF dont l'orange fantôme du parc est
    // né. Les deux disparaissent au profit du seul accent de la charte.
    private static uint CLR_BG => Theme.Current.Paper;
    private static uint CLR_TITLE => Theme.Current.Ink;
    private static uint CLR_TEXT => Theme.Current.Ink;
    private static uint CLR_MUTED => Theme.Current.TextSecondary;
    private static uint CLR_ACCENT => Theme.Current.Action;
    private static uint CLR_SEPARATOR => Theme.Current.Border;
    private static uint CLR_LINK => Theme.Current.Action;

    private IntPtr _hWnd;
    private IntPtr _hWndBtnCopy;
    private IntPtr _hWndBtnClose;
    private IntPtr _hWndLinkFeedback;
    private IntPtr _hWndLinkDiscord;

    private readonly Win32.WNDPROC _wndProcDelegate;
    private readonly Win32.SUBCLASSPROC _linkSubclassProc;
    private IntPtr _hoveredLink;

    private bool _visible;
    private bool _showCopiedFeedback;
    private Action? _themeChanged;

    private float _dpiScale;
    private int S(int val) => (int)(val * _dpiScale);

    /// <summary>L'échelle en points par pouce, dont Theme a besoin pour ses polices.
    /// _dpiScale reste la mesure de travail de cette fenêtre, qui multiplie des dizaines de
    /// coordonnées : les deux disent la même chose.</summary>
    private int _dpi => (int)Math.Round(96 * _dpiScale);

    private IntPtr _hFontTitle => Theme.Font(FontRole.WindowTitle, _dpi);
    private IntPtr _hFontText => Theme.Font(FontRole.Body, _dpi);
    private IntPtr _hFontMuted => Theme.Font(FontRole.Secondary, _dpi);
    private IntPtr _hFontBold => Theme.Font(FontRole.BodyStrong, _dpi);
    private IntPtr _hFontButton => Theme.Font(FontRole.Body, _dpi);
    private IntPtr _hFontLink => Theme.Font(FontRole.Body, _dpi);
    private IntPtr _hFontLinkHovered => Theme.Font(FontRole.Body, _dpi, underlined: true);

    public bool IsVisible => _visible;

    /// <summary>Pour le banc de captures : la fenêtre est rendue, elle n'est pas pilotée.</summary>
    internal IntPtr Handle => _hWnd;

    /// <summary>Langue de l'UI à la création : titre, boutons et liens sont figés au
    /// constructeur. Permet à TrayApplication de recréer la fenêtre si la langue a changé.</summary>
    public string UiLanguage { get; } = L.Language;

    public UsageStatsWindow()
    {
        _wndProcDelegate = WndProc;
        _linkSubclassProc = LinkSubclassProc;

        var hdcScreen = Win32.GetDC(IntPtr.Zero);
        int dpi = Win32.GetDeviceCaps(hdcScreen, 88);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);
        _dpiScale = dpi / 96f;

        CreateMainWindow();
        CreateControls();
        ApplyFontsToControls();

        try
        {
            int realDpi = Win32.GetDpiForWindow(_hWnd);
            if (realDpi > 0 && Math.Abs(realDpi / 96f - _dpiScale) > 0.01f)
            {
                _dpiScale = realDpi / 96f;
                ApplyFontsToControls();
                ResizeWindow();
                RepositionControls();
            }
        }
        catch { /* GetDpiForWindow non disponible (Windows 8.1-) */ }
    }

    private void ApplyFontsToControls()
    {
        Win32.SendMessageW(_hWndBtnCopy, Win32.WM_SETFONT, _hFontButton, (IntPtr)1);
        Win32.SendMessageW(_hWndBtnClose, Win32.WM_SETFONT, _hFontButton, (IntPtr)1);
        Win32.SendMessageW(_hWndLinkFeedback, Win32.WM_SETFONT, _hFontLink, (IntPtr)1);
        Win32.SendMessageW(_hWndLinkDiscord, Win32.WM_SETFONT, _hFontLink, (IntPtr)1);
    }

    private void CreateMainWindow()
    {
        var hInstance = Win32.GetModuleHandleW(null);
        string className = ProductIdentity.WindowClass("UsageStats");

        var wc = new Win32.WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEXW>(),
            lpfnWndProc = _wndProcDelegate,
            hInstance = hInstance,
            hCursor = Win32.LoadCursorW(IntPtr.Zero, (IntPtr)32512),
            // hbrBackground = IntPtr.Zero : une brosse inscrite ici appartient au système,
            // qui la détruit au désenregistrement de la classe. ApplyClassBackground en
            // pose une dédiée, que ce helper est seul à libérer.
            hbrBackground = IntPtr.Zero,
            lpszClassName = className
        };
        Win32.RegisterClassExW(ref wc);

        int winW = S(BASE_WIN_W);
        int winH = S(WinContentH);
        uint dwStyle = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU;
        var adjustRect = new Win32.RECT { left = 0, top = 0, right = winW, bottom = winH };
        Win32.AdjustWindowRectEx(ref adjustRect, dwStyle, false, 0);
        int windowW = adjustRect.right - adjustRect.left;
        int windowH = adjustRect.bottom - adjustRect.top;

        Win32.GetCursorPos(out var cursorPt);
        var hMonitor = Win32.MonitorFromPoint(cursorPt, 0x00000001);
        var monInfo = new Win32.MONITORINFO { cbSize = Marshal.SizeOf<Win32.MONITORINFO>() };
        Win32.GetMonitorInfo(hMonitor, ref monInfo);
        int screenX = monInfo.rcWork.left;
        int screenY = monInfo.rcWork.top;
        int screenW = monInfo.rcWork.right - monInfo.rcWork.left;
        int screenH = monInfo.rcWork.bottom - monInfo.rcWork.top;

        _hWnd = Win32.CreateWindowExW(0, className, L.Stats_WindowTitle,
            dwStyle, screenX + (screenW - windowW) / 2, screenY + (screenH - windowH) / 2, windowW, windowH,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        ThemeWindow.ApplyChrome(_hWnd);
        ThemeWindow.ApplyProductIcon(_hWnd);
        ThemeWindow.ApplyClassBackground(_hWnd, CLR_BG);

        _themeChanged = () =>
        {
            if (_hWnd == IntPtr.Zero)
                return;
            ThemeWindow.ApplyClassBackground(_hWnd, CLR_BG);
            ThemeWindow.ApplyChrome(_hWnd);
        };
        Theme.Changed += _themeChanged;
    }

    private void CreateControls()
    {
        var hInstance = Win32.GetModuleHandleW(null);

        _hWndLinkFeedback = Win32.CreateWindowExW(0, "STATIC", L.Stats_LinkFeedback,
            Win32.WS_CHILD | Win32.WS_VISIBLE | SS_NOTIFY | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_LINK_FEEDBACK, hInstance, IntPtr.Zero);
        Win32.SetWindowSubclass(_hWndLinkFeedback, _linkSubclassProc, (UIntPtr)1, IntPtr.Zero);

        // Canal sobre : aucune invitation Discord, décision D3 du 2026-08-19. Le contrôle
        // est créé mais jamais affiché — il est le dernier de la ligne de liens, donc rien ne
        // se déplace, et un STATIC invisible ne reçoit ni clic ni survol. Ne pas le créer du
        // tout obligerait à dénuller les huit sites qui le manipulent, pour un résultat
        // identique à l'écran.
        uint discordStyle = Win32.WS_CHILD | SS_NOTIFY | Win32.WS_TABSTOP;
        if (PolicyManager.ExternalLinksEnabledNow) discordStyle |= Win32.WS_VISIBLE;
        _hWndLinkDiscord = Win32.CreateWindowExW(0, "STATIC", L.Stats_LinkDiscord,
            discordStyle,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_LINK_DISCORD, hInstance, IntPtr.Zero);
        Win32.SetWindowSubclass(_hWndLinkDiscord, _linkSubclassProc, (UIntPtr)2, IntPtr.Zero);

        // BS_OWNERDRAW remplace le bouton à relief du système : c'est le grand écart entre
        // ces deux générations de contrôles que la refonte supprime.
        _hWndBtnCopy = Win32.CreateWindowExW(0, "BUTTON", L.Stats_BtnCopy,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | Win32.BS_OWNERDRAW,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_COPY, hInstance, IntPtr.Zero);
        Win32.SetWindowSubclass(_hWndBtnCopy, _linkSubclassProc, (UIntPtr)3, IntPtr.Zero);

        _hWndBtnClose = Win32.CreateWindowExW(0, "BUTTON", L.Common_Close,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | Win32.BS_OWNERDRAW,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_CLOSE, hInstance, IntPtr.Zero);
        Win32.SetWindowSubclass(_hWndBtnClose, _linkSubclassProc, (UIntPtr)4, IntPtr.Zero);

        RepositionControls();
    }

    private void ResizeWindow()
    {
        int winW = S(BASE_WIN_W);
        int winH = S(WinContentH);
        uint dwStyle = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU;
        var adjustRect = new Win32.RECT { left = 0, top = 0, right = winW, bottom = winH };
        Win32.AdjustWindowRectEx(ref adjustRect, dwStyle, false, 0);
        int windowW = adjustRect.right - adjustRect.left;
        int windowH = adjustRect.bottom - adjustRect.top;
        Win32.GetWindowRect(_hWnd, out var currentRect);
        int cx = (currentRect.left + currentRect.right) / 2;
        int cy = (currentRect.top + currentRect.bottom) / 2;
        Win32.MoveWindow(_hWnd, cx - windowW / 2, cy - windowH / 2, windowW, windowH, true);
    }

    private void RepositionControls()
    {
        int winW = S(BASE_WIN_W);
        int winH = S(WinContentH);
        int margin = S(20);

        int btnH = S(32);
        int btnY = winH - margin - btnH;

        int btnCloseW = S(90);
        int btnCloseX = winW - margin - btnCloseW;
        Win32.MoveWindow(_hWndBtnClose, btnCloseX, btnY, btnCloseW, btnH, true);

        int btnCopyW = S(210);
        int btnCopyX = btnCloseX - S(12) - btnCopyW;
        Win32.MoveWindow(_hWndBtnCopy, btnCopyX, btnY, btnCopyW, btnH, true);

        // Liens retours/communauté sur une ligne au-dessus des boutons
        int linkH = S(22);
        int linkY = btnY - S(10) - linkH;
        int wFeedback = S(130);
        int wDiscord = S(240);
        int linkGap = S(24);
        int linksX = (winW - wFeedback - wDiscord - linkGap) / 2;
        Win32.MoveWindow(_hWndLinkFeedback, linksX, linkY, wFeedback, linkH, true);
        Win32.MoveWindow(_hWndLinkDiscord, linksX + wFeedback + linkGap, linkY, wDiscord, linkH, true);
    }

    /// <summary>Affiche la fenêtre. Les compteurs sont relus à chaque ouverture (pas de cache).</summary>
    public void Show()
    {
        _showCopiedFeedback = false;
        // La visibilité de la section « Défi du jour » peut changer entre deux ouvertures :
        // la hauteur se recalcule à chaque affichage.
        ResizeWindow();
        RepositionControls();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
        Win32.ShowWindow(_hWnd, 1);
        Win32.SetForegroundWindow(_hWnd);
        _visible = true;
    }

    /// <summary>
    /// Émis à la fermeture de la fenêtre lorsque l'utilisateur a copié son résumé de
    /// statistiques pendant la visite. Même report que <see cref="LessonsWindow"/> et son
    /// <c>ChallengeShared</c> : solliciter à l'instant du clic couperait le geste en deux,
    /// alors que l'utilisateur part précisément coller son résumé ailleurs.
    /// </summary>
    public Action? StatsShared;

    private bool _statsSharedThisSession;

    public void Close()
    {
        Win32.ShowWindow(_hWnd, 0);
        _visible = false;

        // Les trois chemins de fermeture — bouton, Échap, WM_CLOSE — passent tous ici.
        if (_statsSharedThisSession)
        {
            _statsSharedThisSession = false;
            try { StatsShared?.Invoke(); }
            catch (Exception ex) { ConfigManager.Log("UsageStatsWindow.StatsShared", ex); }
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            switch (msg)
            {
                case Win32.WM_PAINT:
                    OnPaint(hWnd);
                    return IntPtr.Zero;

                case Win32.WM_ERASEBKGND:
                    return (IntPtr)1;

                case Win32.WM_DPICHANGED:
                {
                    int newDpi = (wParam.ToInt32() >> 16) & 0xFFFF;
                    if (newDpi > 0)
                        _dpiScale = newDpi / 96f;
                    ApplyFontsToControls();
                    var suggested = Marshal.PtrToStructure<Win32.RECT>(lParam);
                    Win32.MoveWindow(_hWnd, suggested.left, suggested.top,
                        suggested.right - suggested.left, suggested.bottom - suggested.top, true);
                    RepositionControls();
                    Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                    return IntPtr.Zero;
                }

                case Win32.WM_COMMAND:
                {
                    int id = wParam.ToInt32() & 0xFFFF;
                    int code = (wParam.ToInt32() >> 16) & 0xFFFF;
                    if (code != 0) break;
                    switch (id)
                    {
                        case IDC_BTN_COPY:
                            OnCopyStats();
                            break;
                        case IDC_BTN_CLOSE:
                            Close();
                            break;
                        case IDC_LINK_FEEDBACK:
                            Win32.ShellExecuteW(IntPtr.Zero, "open", ProductIdentity.Url("/feedback"), null, null, 1);
                            break;
                        case IDC_LINK_DISCORD:
                            Win32.ShellExecuteW(IntPtr.Zero, "open", ProductIdentity.DiscordInviteUrl, null, null, 1);
                            break;
                    }
                    return IntPtr.Zero;
                }

                case Win32.WM_CTLCOLORSTATIC:
                {
                    IntPtr hdcStatic = wParam;
                    IntPtr hCtrl = lParam;
                    if (hCtrl == _hWndLinkFeedback || hCtrl == _hWndLinkDiscord)
                    {
                        Win32.SetBkMode(hdcStatic, 1);
                        // La même couleur dans tous les états : c'est la police qui souligne.
                        // Deux teintes de lien demanderaient une seconde nuance d'accent, que la
                        // charte n'a pas — et c'est de ce survol qu'était né l'orange fantôme.
                        Win32.SetTextColor(hdcStatic, CLR_LINK);
                        return Theme.Brush(CLR_BG);
                    }
                    break;
                }

                case Win32.WM_CTLCOLORBTN:
                    // Windows efface le fond d'un bouton owner-draw avec la brosse rendue ici,
                    // avant d'envoyer WM_DRAWITEM.
                    Win32.SetBkMode(wParam, Win32.TRANSPARENT);
                    Win32.SetTextColor(wParam, CLR_TEXT);
                    return Theme.Brush(CLR_BG);

                case Win32.WM_DRAWITEM:
                    if (TryDrawItem(lParam))
                        return (IntPtr)1;
                    break;

                case Win32.WM_SETCURSOR:
                    if (wParam == _hWndLinkFeedback || wParam == _hWndLinkDiscord)
                    {
                        Win32.SetCursor(Win32.LoadCursorW(IntPtr.Zero, (IntPtr)32649));
                        return (IntPtr)1;
                    }
                    break;

                case Win32.WM_TIMER:
                    if (wParam.ToInt64() == TIMER_COPY_FEEDBACK)
                    {
                        Win32.KillTimer(_hWnd, (UIntPtr)TIMER_COPY_FEEDBACK);
                        _showCopiedFeedback = false;
                        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                    }
                    return IntPtr.Zero;

                case Win32.WM_KEYDOWN:
                    if (wParam == (IntPtr)0x1B) // VK_ESCAPE
                    {
                        Close();
                        return IntPtr.Zero;
                    }
                    break;

                case Win32.WM_CLOSE:
                    Close();
                    return IntPtr.Zero;
            }
        }
        catch (Exception ex)
        {
            ConfigManager.Log("UsageStatsWindow WndProc", ex);
        }

        return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    /// <summary>Un contrôle STATIC est peint par le système : on ne peut pas lui dessiner de
    /// filet, seule sa police peut le souligner. Les boutons, eux, ne sont pas des liens et
    /// gardent la leur.</summary>
    private void ApplyLinkFont(IntPtr hWnd, bool underlined)
    {
        if (hWnd != _hWndLinkFeedback && hWnd != _hWndLinkDiscord)
            return;
        Win32.SendMessageW(hWnd, Win32.WM_SETFONT,
            underlined ? _hFontLinkHovered : _hFontLink, (IntPtr)1);
    }

    /// <summary>
    /// Les deux boutons de la fenêtre. « Copier » est l'action qu'elle propose, donc le seul
    /// primaire de l'écran ; « Fermer » est un congé, comme dans À propos.
    /// </summary>
    private bool TryDrawItem(IntPtr lParam)
    {
        var dis = Marshal.PtrToStructure<Win32.DRAWITEMSTRUCT>(lParam);
        if (dis.hwndItem != _hWndBtnCopy && dis.hwndItem != _hWndBtnClose)
            return false;

        var state = ControlState.None;
        if ((dis.itemState & Win32.ODS_DISABLED) != 0) state |= ControlState.Disabled;
        if ((dis.itemState & Win32.ODS_SELECTED) != 0) state |= ControlState.Pressed;
        if ((dis.itemState & Win32.ODS_FOCUS) != 0) state |= ControlState.Focused;
        if (_hoveredLink == dis.hwndItem) state |= ControlState.Hovered;

        var full = dis.rcItem;
        Win32.FillRect(dis.hDC, ref full, Theme.Brush(CLR_BG));

        bool copy = dis.hwndItem == _hWndBtnCopy;
        ThemeControls.DrawButton(dis.hDC, dis.rcItem, copy ? L.Stats_BtnCopy : L.Common_Close,
            _hFontButton, copy ? ButtonKind.Primary : ButtonKind.Secondary,
            state, Theme.Current, _dpi);
        return true;
    }

    private IntPtr LinkSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        switch (msg)
        {
            case Win32.WM_MOUSEMOVE:
                if (_hoveredLink != hWnd)
                {
                    _hoveredLink = hWnd;
                    ApplyLinkFont(hWnd, underlined: true);
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
                if (_hoveredLink == hWnd)
                {
                    _hoveredLink = IntPtr.Zero;
                    ApplyLinkFont(hWnd, underlined: false);
                    Win32.InvalidateRect(hWnd, IntPtr.Zero, true);
                }
                break;
            case 0x0087: // WM_GETDLGCODE
                return (IntPtr)0x0004; // DLGC_WANTALLKEYS
            case Win32.WM_KEYDOWN:
                if (wParam == (IntPtr)0x0D) // VK_RETURN
                {
                    int ctrlId = Win32.GetDlgCtrlID(hWnd);
                    Win32.SendMessageW(_hWnd, Win32.WM_COMMAND, (IntPtr)ctrlId, hWnd);
                    return IntPtr.Zero;
                }
                break;
        }
        return Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    /// <summary>
    /// Copie le résumé lisible dans le presse-papiers. Usage prévu : témoignages, retours
    /// pilotes, mails au support — copie uniquement, aucun envoi automatique.
    /// </summary>
    private void OnCopyStats()
    {
        if (ClipboardText.TrySet(_hWnd, UsageStats.BuildShareText()))
        {
            _statsSharedThisSession = true;
            _showCopiedFeedback = true;
            Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
            Win32.SetTimer(_hWnd, (UIntPtr)TIMER_COPY_FEEDBACK, 1500, IntPtr.Zero);
        }
    }

    // La copie elle-même vit dans ClipboardText.TrySet depuis la v1.2.0, avec le même
    // filet de restauration : mémoriser l'ancien contenu pour le remettre si
    // SetClipboardData échoue après EmptyClipboard.

    private void OnPaint(IntPtr hWnd)
    {
        var hdcPaint = Win32.BeginPaint(hWnd, out var ps);
        Win32.GetClientRect(hWnd, out var clientRect);
        int cw = clientRect.right;
        int ch = clientRect.bottom;

        var hdcScreen = Win32.GetDC(IntPtr.Zero);
        var hdc = Win32.CreateCompatibleDC(hdcScreen);
        var hBmp = Win32.CreateCompatibleBitmap(hdcScreen, cw, ch);
        var hBmpOld = Win32.SelectObject(hdc, hBmp);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);

        Win32.FillRect(hdc, ref clientRect, Theme.Brush(CLR_BG));
        Win32.SetBkMode(hdc, 1);

        int margin = S(20);
        int y = S(18);

        Win32.SelectObject(hdc, _hFontTitle);
        Win32.SetTextColor(hdc, CLR_TITLE);
        var titleRect = new Win32.RECT { left = margin, top = y, right = cw - margin, bottom = y + S(28) };
        Win32.DrawTextW(hdc, L.Stats_Title, -1, ref titleRect, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
        y += S(40);

        Win32.SetTextColor(hdc, CLR_TEXT);

        // Collecte éteinte : le dire, plutôt que d'afficher « vous n'avez pas encore tapé de
        // caractère spécial » à chaque ouverture. Rien n'étant relu, cette phrase serait vraie
        // au sens du fichier et fausse pour l'utilisateur, et un zéro ressemble à un bug.
        bool collectionOff = !UsageStats.CollectionEnabled;

        var first = UsageStats.FirstRemapDate;
        string headline = collectionOff
            ? L.Stats_CollectionOffHeadline
            : first.HasValue
                ? L.Stats_HeadlineWithDate(L.FormatDate(first.Value))
                : L.Stats_HeadlineNoData;
        y = DrawWrappedLine(hdc, _hFontText, headline, margin, y, cw - margin * 2, S(20));

        int activeDays = UsageStats.ActiveDaysCount;
        if (activeDays > 0)
        {
            string joursLine = L.Stats_DaysLine(activeDays, UsageStats.CurrentStreak, UsageStats.BestStreak);
            y = DrawWrappedLine(hdc, _hFontText, joursLine, margin, y, cw - margin * 2, S(20));

            long activeMinutes = UsageStats.TotalActiveMinutes;
            if (activeMinutes > 0)
            {
                long avg = activeMinutes / activeDays;
                bool showAvg = avg >= 5 && activeDays > 1;
                string tempsLine = L.Stats_ActiveTimeLine(UsageStats.FormatActiveTime(activeMinutes),
                    showAvg ? UsageStats.FormatActiveTime(avg) : null);
                y = DrawWrappedLine(hdc, _hFontText, tempsLine, margin, y, cw - margin * 2, S(20));
            }
        }
        y += S(8);

        var sepBrush = Win32.CreateSolidBrush(CLR_SEPARATOR);
        var sepRect = new Win32.RECT { left = margin, top = y, right = cw - margin, bottom = y + 1 };
        Win32.FillRect(hdc, ref sepRect, sepBrush);
        Win32.DeleteObject(sepBrush);
        y += S(14);

        y = DrawStatLine(hdc, L.Stats_LabelAccented, UsageStats.AccentedUppercaseCount, margin, y, cw - margin);
        y = DrawStatLine(hdc, L.Stats_LabelTypography, UsageStats.FrenchTypographyCount, margin, y, cw - margin);
        y = DrawStatLine(hdc, L.Stats_LabelInternational, UsageStats.InternationalCount, margin, y, cw - margin);
        y = DrawStatLine(hdc, L.Stats_LabelSymbols, UsageStats.SymbolsCount, margin, y, cw - margin);
        y += S(4);
        y = DrawStatLine(hdc, L.Stats_LabelTotal, UsageStats.TotalSpecialCharsCount, margin, y, cw - margin, bold: true);
        y += S(12);

        // Section « Défi du jour » : visible dès qu'il y a quelque chose à montrer — rappels
        // actifs, ou historique conservé même après désactivation des rappels (v1.2.0).
        if (ChallengeSectionVisible)
            y = DrawChallengeSection(hdc, margin, y, cw - margin);

        string reassurance = _showCopiedFeedback
            ? L.Stats_CopiedFeedback
            : collectionOff
                ? L.Stats_CollectionOffPrivacy
                : L.Stats_PrivacyReassurance;
        Win32.SetTextColor(hdc, _showCopiedFeedback ? CLR_ACCENT : CLR_MUTED);
        DrawWrappedLine(hdc, _hFontMuted, reassurance, margin, y, cw - margin * 2, S(16));

        Win32.BitBlt(hdcPaint, 0, 0, cw, ch, hdc, 0, 0, Win32.SRCCOPY);
        Win32.SelectObject(hdc, hBmpOld);
        Win32.DeleteObject(hBmp);
        Win32.DeleteDC(hdc);
        Win32.EndPaint(hWnd, ref ps);
    }

    private int DrawWrappedLine(IntPtr hdc, IntPtr font, string text, int x, int y, int width, int lineHeight)
    {
        Win32.SelectObject(hdc, font);
        var measureRect = new Win32.RECT { left = x, top = 0, right = x + width, bottom = lineHeight * 4 };
        Win32.DrawTextW(hdc, text, -1, ref measureRect, Win32.DT_LEFT | Win32.DT_WORDBREAK | Win32.DT_NOPREFIX | Win32.DT_CALCRECT);
        int height = Math.Max(lineHeight, measureRect.bottom - measureRect.top);

        var paintRect = new Win32.RECT { left = x, top = y, right = x + width, bottom = y + height };
        Win32.DrawTextW(hdc, text, -1, ref paintRect, Win32.DT_LEFT | Win32.DT_WORDBREAK | Win32.DT_NOPREFIX);
        return y + height + S(6);
    }

    private int DrawStatLine(IntPtr hdc, string label, long count, int left, int y, int right, bool bold = false)
    {
        int h = S(20);
        Win32.SelectObject(hdc, bold ? _hFontBold : _hFontText);
        Win32.SetTextColor(hdc, CLR_TEXT);
        var labelRect = new Win32.RECT { left = left, top = y, right = right - S(70), bottom = y + h };
        Win32.DrawTextW(hdc, label, -1, ref labelRect, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);

        Win32.SetTextColor(hdc, bold ? CLR_ACCENT : CLR_TEXT);
        var valueRect = new Win32.RECT { left = right - S(70), top = y, right = right, bottom = y + h };
        Win32.DrawTextW(hdc, count.ToString("N0", L.DisplayCulture), -1, ref valueRect,
            Win32.DT_RIGHT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);

        return y + h;
    }

    /// <summary>
    /// Section « Défi du jour » : séances terminées, avancement de la « Prise en main »
    /// (5 changements) puis bascule vers les défis quotidiens, et date de la dernière séance.
    /// Lue depuis ConfigManager/UsageStats à chaque ouverture, pas de cache.
    /// </summary>
    private int DrawChallengeSection(IntPtr hdc, int left, int y, int right)
    {
        var sepBrush = Win32.CreateSolidBrush(CLR_SEPARATOR);
        var sepRect = new Win32.RECT { left = left, top = y, right = right, bottom = y + 1 };
        Win32.FillRect(hdc, ref sepRect, sepBrush);
        Win32.DeleteObject(sepBrush);
        y += S(14);

        Win32.SelectObject(hdc, _hFontBold);
        Win32.SetTextColor(hdc, CLR_TEXT);
        var titleRect = new Win32.RECT { left = left, top = y, right = right, bottom = y + S(20) };
        Win32.DrawTextW(hdc, L.Challenge_StatsSectionTitle, -1, ref titleRect, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
        y += S(26);

        y = DrawStatLine(hdc, L.Challenge_StatsSessionsLabel, UsageStats.ChallengesCompletedCount, left, y, right);

        uint seq = ConfigManager.TrainingSequenceIndex;
        string onboardingValue = seq >= DailyChallenge.SequenceLength
            ? L.Challenge_StatsOnboardingDone
            : $"{Math.Min(seq, (uint)DailyChallenge.SequenceLength)}/{DailyChallenge.SequenceLength}";
        y = DrawStatLineText(hdc, L.Challenge_StatsOnboardingLabel, onboardingValue, left, y, right);

        var lastSession = ParseTrainingDate(ConfigManager.TrainingLastSessionDate);
        string lastSessionValue = lastSession.HasValue ? L.FormatDate(lastSession.Value) : L.Challenge_StatsNoSessionYet;
        y = DrawStatLineText(hdc, L.Challenge_StatsLastSessionLabel, lastSessionValue, left, y, right);

        return y;
    }

    /// <summary>Variante de DrawStatLine pour une valeur textuelle (date, étiquette) plutôt
    /// qu'un compteur numérique — colonne de valeur plus large pour les dates longues.</summary>
    private int DrawStatLineText(IntPtr hdc, string label, string value, int left, int y, int right, bool bold = false)
    {
        int h = S(20);
        Win32.SelectObject(hdc, bold ? _hFontBold : _hFontText);
        Win32.SetTextColor(hdc, CLR_TEXT);
        var labelRect = new Win32.RECT { left = left, top = y, right = right - S(160), bottom = y + h };
        Win32.DrawTextW(hdc, label, -1, ref labelRect, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);

        Win32.SetTextColor(hdc, bold ? CLR_ACCENT : CLR_TEXT);
        var valueRect = new Win32.RECT { left = right - S(160), top = y, right = right, bottom = y + h };
        Win32.DrawTextW(hdc, value, -1, ref valueRect, Win32.DT_RIGHT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);

        return y + h;
    }

    /// <summary>Parse "yyyy-MM-dd" (format de stockage ConfigManager) — même format que
    /// TrainingReminders.ParseDate, dupliqué ici car privé dans l'autre classe.</summary>
    private static DateOnly? ParseTrainingDate(string? s) =>
        s != null && DateOnly.TryParseExact(s, "yyyy-MM-dd", out var d) ? d : null;

    public void Dispose()
    {
        if (_hWndLinkFeedback != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndLinkFeedback, _linkSubclassProc, (UIntPtr)1);
        if (_hWndLinkDiscord != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndLinkDiscord, _linkSubclassProc, (UIntPtr)2);
        if (_hWndBtnCopy != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndBtnCopy, _linkSubclassProc, (UIntPtr)3);
        if (_hWndBtnClose != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndBtnClose, _linkSubclassProc, (UIntPtr)4);

        if (_themeChanged != null)
        {
            Theme.Changed -= _themeChanged;
            _themeChanged = null;
        }

        if (_hWnd != IntPtr.Zero)
        {
            ThemeWindow.ForgetClassBackground(_hWnd);
            Win32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }

        // Ni police ni brosse à libérer : les unes appartiennent au cache de Theme, l'autre au
        // système, qui la détruit avec la classe.

        // UnregisterClassW pour permettre une 2e instance avec un delegate WndProc frais.
        Win32.UnregisterClassW(ProductIdentity.WindowClass("UsageStats"), Win32.GetModuleHandleW(null));
    }
}
