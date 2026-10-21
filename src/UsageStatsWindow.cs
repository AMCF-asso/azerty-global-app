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
    // 1.3.0 : Défi masqué (DailyChallenge.Enabled) ; les compteurs restent sur disque.
    private static bool ChallengeSectionVisible
        => DailyChallenge.Enabled
           && (ConfigManager.TrainingEnabled || UsageStats.ChallengesCompletedCount > 0);
    private static int WinContentH
        => BASE_WIN_H + (ChallengeSectionVisible ? CHALLENGE_SECTION_H : 0);

    // ── Couleurs ────────────────────────────────────────────────────
    // A9 : palette papier des Leçons, ou celle du système en contraste élevé (lue à la
    // création, comme l'accueil).
    private readonly PaperPalette _pal = PaperPalette.Now();
    private uint LinkHover => _pal.HighContrast ? _pal.Action : PaperDraw.Blend(_pal.Ink, _pal.Action, 0.25f);

    private IntPtr _hWnd;
    private IntPtr _hWndBtnCopy;
    private IntPtr _hWndBtnClose;
    private IntPtr _hWndLinkFeedback;
    private IntPtr _hWndLinkDiscord;

    private readonly Win32.WNDPROC _wndProcDelegate;
    // F-10 : survol, Entrée, Échap et Tab des deux liens. Pas encore de cadre de focus (K5).
    private readonly LinkBehavior _links;
    // Fond de la classe : Windows le détruit au désenregistrement (NativeWindow).
    private IntPtr _hBgBrush;

    private bool _visible;
    private bool _showCopiedFeedback;

    private float _dpiScale;
    private int S(int val) => (int)(val * _dpiScale);

    // A9 : polices du site (AG Titre, AG Texte), partagées par AppFonts : jamais détruites.
    // Le texte peint passe par PaperText, qui complète un glyphe absent (le « ✓ » de
    // « Terminée ✓ ») ; seul le soulignement des liens STATIC demande une police à soi.
    // AG Texte n'a pas d'italique : la mention de confidentialité passe en romain.
    private FontSpec FontTitle => FontSpec.Of(AppFonts.Title, TypeRamp.Title, TypeRamp.Semibold, _dpiScale);
    private FontSpec FontText => FontSpec.Of(AppFonts.Text, TypeRamp.Body, TypeRamp.Regular, _dpiScale);
    private FontSpec FontMuted => FontSpec.Of(AppFonts.Text, TypeRamp.Caption, TypeRamp.Regular, _dpiScale);
    private FontSpec FontBold => FontSpec.Of(AppFonts.Text, TypeRamp.Body, TypeRamp.Semibold, _dpiScale);
    private IntPtr _hFontLink;

    public bool IsVisible => _visible;

    /// <summary>Langue de l'UI à la création : titre, boutons et liens sont figés au
    /// constructeur. Permet à TrayApplication de recréer la fenêtre si la langue a changé.</summary>
    public string UiLanguage { get; } = L.Language;

    public UsageStatsWindow()
    {
        _wndProcDelegate = WndProc;
        _links = new LinkBehavior(() => _hWnd, Close, focusFrame: false);

        var hdcScreen = Win32.GetDC(IntPtr.Zero);
        int dpi = Win32.GetDeviceCaps(hdcScreen, 88);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);
        _dpiScale = dpi / 96f;

        CreateFonts();
        CreateMainWindow();
        CreateControls();
        ApplyFontsToControls();

        if (NativeWindow.CorrectedScale(Win32.GetDpiForWindow(_hWnd), _dpiScale) is float windowScale)
        {
            _dpiScale = windowScale;
            // Mise en page seule : GetDpiForWindow ne lève pas (audit du 25/09, X-04).
            try
            {
                RecreateFonts();
                ResizeWindow();
                RepositionControls();
            }
            catch { }
        }
    }

    private void CreateFonts()
    {
        _hFontLink = TypeRamp.CreateIn(AppFonts.Text, TypeRamp.Body, TypeRamp.Regular, _dpiScale, underline: true);
    }

    private void DestroyFonts()
    {
        Win32.DeleteObject(_hFontLink);
    }

    private void RecreateFonts()
    {
        DestroyFonts();
        CreateFonts();
        ApplyFontsToControls();
    }

    private void ApplyFontsToControls()
    {
        Win32.SendMessageW(_hWndBtnCopy, Win32.WM_SETFONT, FontText.Handle, (IntPtr)1);
        Win32.SendMessageW(_hWndBtnClose, Win32.WM_SETFONT, FontText.Handle, (IntPtr)1);
        Win32.SendMessageW(_hWndLinkFeedback, Win32.WM_SETFONT, _hFontLink, (IntPtr)1);
        Win32.SendMessageW(_hWndLinkDiscord, Win32.WM_SETFONT, _hFontLink, (IntPtr)1);
    }

    private void CreateMainWindow()
    {
        var hInstance = Win32.GetModuleHandleW(null);
        string className = ProductIdentity.WindowClass("UsageStats");
        // Audit du 25/09, X-01 : une classe refusée ne crée pas de fenêtre.
        if (!NativeWindow.RegisterClass(className, _wndProcDelegate))
            return;

        uint dwStyle = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU;
        var (windowW, windowH) = NativeWindow.OuterSize(S(BASE_WIN_W), S(WinContentH), dwStyle);
        var (x, y) = NativeWindow.CenterIn(NativeWindow.WorkArea(), windowW, windowH);

        _hWnd = Win32.CreateWindowExW(0, className, L.Stats_WindowTitle,
            dwStyle, x, y, windowW, windowH,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        _hBgBrush = NativeWindow.ApplyClassBackground(_hWnd, _pal.Paper);

        // AG130-40 : cette fenetre veut Tab, Maj+Tab et Entree entre ses controles.
        DialogNavigation.Register(_hWnd);
        NativeWindow.ApplyFrame(_hWnd, dark: false);
    }

    private void CreateControls()
    {
        var hInstance = Win32.GetModuleHandleW(null);

        _hWndLinkFeedback = Win32.CreateWindowExW(0, "STATIC", L.Stats_LinkFeedback,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.SS_NOTIFY | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_LINK_FEEDBACK, hInstance, IntPtr.Zero);
        _links.Attach(_hWndLinkFeedback, 1);

        // Canal sobre : aucune invitation Discord, décision D3 du 2026-08-19. Le contrôle
        // est créé mais jamais affiché — il est le dernier de la ligne de liens, donc rien ne
        // se déplace, et un STATIC invisible ne reçoit ni clic ni survol. Ne pas le créer du
        // tout obligerait à dénuller les huit sites qui le manipulent, pour un résultat
        // identique à l'écran.
        uint discordStyle = Win32.WS_CHILD | Win32.SS_NOTIFY | Win32.WS_TABSTOP;
        if (PolicyManager.ExternalLinksEnabledNow) discordStyle |= Win32.WS_VISIBLE;
        _hWndLinkDiscord = Win32.CreateWindowExW(0, "STATIC", L.Stats_LinkDiscord,
            discordStyle,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_LINK_DISCORD, hInstance, IntPtr.Zero);
        _links.Attach(_hWndLinkDiscord, 2);

        _hWndBtnCopy = Win32.CreateWindowExW(0, "BUTTON", L.Stats_BtnCopy,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_COPY, hInstance, IntPtr.Zero);

        _hWndBtnClose = Win32.CreateWindowExW(0, "BUTTON", L.Common_Close,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | 0x0001 /* BS_DEFPUSHBUTTON */,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_CLOSE, hInstance, IntPtr.Zero);
        // Audit UX 1.3.0, #3 : boutons clairs de Windows 11, « Fermer » porte l'accent.
        // A9 : boutons papier ; en contraste élevé, le dessin du système.
        if (!_pal.HighContrast)
        {
            LightButton.Attach(_hWndBtnCopy, paper: true);
            LightButton.Attach(_hWndBtnClose, paper: true);
        }

        RepositionControls();
    }

    private void ResizeWindow() =>
        NativeWindow.ResizeAroundCenter(_hWnd, S(BASE_WIN_W), S(WinContentH),
            Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU);

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
                    _dpiScale = NativeWindow.DpiFromChange(_hWnd, wParam) / 96f;
                    RecreateFonts();
                    NativeWindow.MoveToSuggestedRect(_hWnd, lParam);
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
                        // Revue du 2026-09-21, R5 : Entrée et Échap arrivent d'IsDialogMessageW
                        // en WM_COMMAND. Entrée sur « Copier » copie, ailleurs elle ferme ; Échap ferme.
                        case DialogNavigation.IDOK:
                            if (DialogNavigation.ButtonToPressOnEnter(id, Win32.GetFocus(), new[] { _hWndBtnCopy }) != IntPtr.Zero)
                                OnCopyStats();
                            else
                                Close();
                            break;
                        case DialogNavigation.IDCANCEL:
                            Close();
                            break;
                        case IDC_BTN_COPY:
                            OnCopyStats();
                            break;
                        case IDC_BTN_CLOSE:
                            Close();
                            break;
                        case IDC_LINK_FEEDBACK:
                            Win32.ShellExecuteW(IntPtr.Zero, "open",
                                ProductIdentity.DiagnosticUrl(ProductIdentity.FeedbackPath, Program.Version,
                                    ProductIdentity.OsDescription(), "stats"), null, null, 1);
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
                    Win32.SetBkMode(hdcStatic, 1);
                    Win32.SetBkColor(hdcStatic, _pal.Paper);
                    if (_links.Contains(hCtrl))
                        Win32.SetTextColor(hdcStatic, _links.IsActive(hCtrl) ? LinkHover : _pal.Action);
                    // A9 : les boutons papier lisent ici la couleur de leurs coins
                    // (LightButton.Backdrop) ; sans réponse, Windows rendait son gris (#F0F0F0).
                    return _hBgBrush;
                }

                case Win32.WM_SETCURSOR:
                    if (_links.TrySetHandCursor(wParam))
                        return (IntPtr)1;
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
        using var paint = new PaintBuffer(hWnd);
        var clientRect = paint.Client;
        int cw = clientRect.right;
        int ch = clientRect.bottom;
        var hdc = paint.Hdc;

        Win32.FillRect(hdc, ref clientRect, _hBgBrush);
        Win32.SetBkMode(hdc, 1);

        int margin = S(20);
        int y = S(18);

        var titleRect = new Win32.RECT { left = margin, top = y, right = cw - margin, bottom = y + S(28) };
        PaperText.DrawLine(hdc, L.Stats_Title, FontTitle, titleRect, _pal.Ink);
        y += S(40);

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
        y = DrawWrappedLine(hdc, FontText, headline, margin, y, cw - margin * 2, S(20), _pal.Ink);

        int activeDays = UsageStats.ActiveDaysCount;
        if (activeDays > 0)
        {
            string joursLine = L.Stats_DaysLine(activeDays, UsageStats.CurrentStreak, UsageStats.BestStreak);
            y = DrawWrappedLine(hdc, FontText, joursLine, margin, y, cw - margin * 2, S(20), _pal.Ink);

            long activeMinutes = UsageStats.TotalActiveMinutes;
            if (activeMinutes > 0)
            {
                long avg = activeMinutes / activeDays;
                bool showAvg = avg >= 5 && activeDays > 1;
                string tempsLine = L.Stats_ActiveTimeLine(UsageStats.FormatActiveTime(activeMinutes),
                    showAvg ? UsageStats.FormatActiveTime(avg) : null);
                y = DrawWrappedLine(hdc, FontText, tempsLine, margin, y, cw - margin * 2, S(20), _pal.Ink);
            }
        }
        y += S(8);

        PaperDraw.Fill(hdc, margin, y, cw - margin, y + 1, _pal.Border);
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
        DrawWrappedLine(hdc, FontMuted, reassurance, margin, y, cw - margin * 2, S(16),
            _showCopiedFeedback ? _pal.Action : _pal.Muted);
    }

    /// <summary>Paragraphe coupé aux espaces dans <paramref name="width"/> ; rend le haut du
    /// suivant.</summary>
    private int DrawWrappedLine(IntPtr hdc, FontSpec font, string text, int x, int y, int width, int lineHeight, uint color)
    {
        var rect = new Win32.RECT { left = x, top = y, right = x + width, bottom = y + lineHeight };
        var runs = new[] { new TextRun(text) };
        int height = Math.Max(lineHeight, PaperText.DrawParagraph(hdc, runs, font, font, rect, color, color, lineHeight));
        return y + height + S(6);
    }

    /// <summary>Une ligne coupée à son cadre, comme DT_SINGLELINE le faisait.</summary>
    private static void DrawCell(IntPtr hdc, string text, FontSpec font, Win32.RECT rect, uint color, TextAlign align)
    {
        int save = Win32.SaveDC(hdc);
        Win32.IntersectClipRect(hdc, rect.left, rect.top, rect.right, rect.bottom);
        PaperText.DrawLine(hdc, text, font, rect, color, align);
        Win32.RestoreDC(hdc, save);
    }

    private int DrawStatLine(IntPtr hdc, string label, long count, int left, int y, int right, bool bold = false)
    {
        int h = S(20);
        var font = bold ? FontBold : FontText;
        var labelRect = new Win32.RECT { left = left, top = y, right = right - S(70), bottom = y + h };
        DrawCell(hdc, label, font, labelRect, _pal.Ink, TextAlign.Left);

        var valueRect = new Win32.RECT { left = right - S(70), top = y, right = right, bottom = y + h };
        DrawCell(hdc, count.ToString("N0", L.DisplayCulture), font, valueRect, bold ? _pal.Action : _pal.Ink, TextAlign.Right);

        return y + h;
    }

    /// <summary>
    /// Section « Défi du jour » : séances terminées, avancement de la « Prise en main »
    /// (5 changements) puis bascule vers les défis quotidiens, et date de la dernière séance.
    /// Lue depuis ConfigManager/UsageStats à chaque ouverture, pas de cache.
    /// </summary>
    private int DrawChallengeSection(IntPtr hdc, int left, int y, int right)
    {
        PaperDraw.Fill(hdc, left, y, right, y + 1, _pal.Border);
        y += S(14);

        var titleRect = new Win32.RECT { left = left, top = y, right = right, bottom = y + S(20) };
        DrawCell(hdc, L.Challenge_StatsSectionTitle, FontBold, titleRect, _pal.Ink, TextAlign.Left);
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
        var font = bold ? FontBold : FontText;
        var labelRect = new Win32.RECT { left = left, top = y, right = right - S(160), bottom = y + h };
        DrawCell(hdc, label, font, labelRect, _pal.Ink, TextAlign.Left);

        var valueRect = new Win32.RECT { left = right - S(160), top = y, right = right, bottom = y + h };
        DrawCell(hdc, value, font, valueRect, bold ? _pal.Action : _pal.Ink, TextAlign.Right);

        return y + h;
    }

    /// <summary>Parse "yyyy-MM-dd" (format de stockage ConfigManager) — même format que
    /// TrainingReminders.ParseDate, dupliqué ici car privé dans l'autre classe.</summary>
    private static DateOnly? ParseTrainingDate(string? s) =>
        s != null && DateOnly.TryParseExact(s, "yyyy-MM-dd", out var d) ? d : null;

    public void Dispose()
    {
        _links.Detach();
        if (_hWnd != IntPtr.Zero)
        {
            // AG130-40 : desinscrire AVANT de detruire — Windows recycle les HWND.
            DialogNavigation.Unregister(_hWnd);
            Win32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }

        DestroyFonts();
        NativeWindow.UnregisterClass(ProductIdentity.WindowClass("UsageStats"));
    }
}
