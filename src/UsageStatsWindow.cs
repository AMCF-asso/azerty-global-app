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
    private const int IDC_BTN_RATE = 4205;
    private const uint TIMER_COPY_FEEDBACK = 4210;

    // 1.3.0, A1 : WS_VSCROLL dès la création (la barre existe, FitWindow la masque quand tout
    // tient), comme les Paramètres et l'Accueil. Un seul style pour la création, la mesure du
    // cadre et le redimensionnement : sans lui, la zone cliente se calcule sans la barre.
    private const uint Style = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU | Win32.WS_VSCROLL;
    private readonly WindowScroll _scroll = new();

    private const int BASE_WIN_W = 460;
    private const int BASE_WIN_H = 460;
    // C1 (1.3.0) : « Noter l'application » tient sur sa propre rangée, au-dessus de « Copier ».
    // Réservée seulement quand le bouton est visible (canal Store, liens externes permis).
    private const int RATE_ROW_H = 40;
    // Section « Défi du jour » ajoutée en v1.2.0 : la fenêtre ne réserve sa hauteur
    // que lorsque la section se dessine (même prédicat que WM_PAINT, cf. plus bas).
    private const int CHALLENGE_SECTION_H = 130;
    // 1.3.0 : Défi masqué par son interrupteur ou par la stratégie LessonsEnabled ; les
    // compteurs restent sur disque.
    private static bool ChallengeSectionVisible
        => PolicyManager.ChallengeEnabledNow
           && (ConfigManager.TrainingEnabled || UsageStats.ChallengesCompletedCount > 0);
    // S1-3 : deux lignes discrètes sous la phrase de confidentialité, réservées comme le Défi.
    private const int POLICY_HINT_H = 40;
    private static int WinContentH
        => BASE_WIN_H + (ChallengeSectionVisible ? CHALLENGE_SECTION_H : 0)
           + (PolicyManager.CollectionPolicyHintNow ? POLICY_HINT_H : 0)
           + (RateVisibleNow ? RATE_ROW_H : 0);

    /// <summary>C1 : le bouton « Noter l'application » n'existe que si une fiche Store est à noter
    /// (canal Store, pas AMCF ni hors paquet) et que la politique permet d'ouvrir des liens externes.
    /// Pure : testable sans fenêtre.</summary>
    internal static bool RateButtonVisible(DistributionChannel channel, bool externalLinks)
        => StoreReview.RatesOnStore(channel) && externalLinks;

    private static bool RateVisibleNow
        => RateButtonVisible(AppChannel.Current, PolicyManager.ExternalLinksEnabledNow);

    /// <summary>Ordonnées (zone de contenu, origine en haut, sans défilement) des rangées posées sous
    /// le texte : liens, bouton « Noter », boutons « Copier » et « Fermer ».</summary>
    internal readonly record struct Rows(int LinkY, int RateY, int ButtonY, int ContentHeight);

    /// <summary>
    /// A1 : les rangées du bas suivent le texte mesuré au lieu de l'estimer. Une hauteur fixe
    /// laissait, à 200 % ou 300 %, les paragraphes qui reviennent à la ligne passer sous les
    /// boutons ancrés en bas. La hauteur de référence reste un plancher : à 100 % la fenêtre
    /// garde sa taille, boutons au bas du cadre.
    /// </summary>
    /// <param name="textBottom">Bas du texte peint (y suivant le dernier paragraphe).</param>
    /// <param name="minContentH">Hauteur de référence, à l'échelle de l'écran.</param>
    /// <param name="s">Mise à l'échelle d'une valeur de référence.</param>
    internal static Rows ComputeRows(int textBottom, int minContentH, bool rateVisible, Func<int, int> s)
    {
        int margin = s(20), btnH = s(32), linkH = s(22);
        int flowLinkY = textBottom + s(6);
        int flowRateY = flowLinkY + linkH + s(8);
        int flowBtnY = rateVisible ? flowRateY + btnH + s(8) : flowLinkY + linkH + s(10);
        int contentH = Math.Max(minContentH, flowBtnY + btnH + margin);

        int btnY = contentH - margin - btnH;
        int rateY = rateVisible ? btnY - s(8) - btnH : 0;
        int linkY = (rateVisible ? rateY : btnY) - s(rateVisible ? 8 : 10) - linkH;
        return new Rows(linkY, rateY, btnY, contentH);
    }

    // ── Couleurs ────────────────────────────────────────────────────
    // A9 : palette papier des Leçons, ou celle du système en contraste élevé (lue à la
    // création, comme l'accueil).
    private PaperPalette _pal = PaperPalette.Now(); // T5 : relue sur WM_SETTINGCHANGE
    private uint LinkHover => _pal.HighContrast ? _pal.Action : PaperDraw.Blend(_pal.Ink, _pal.Action, 0.25f);

    private IntPtr _hWnd;
    private IntPtr _hWndBtnCopy;
    private IntPtr _hWndBtnRate;
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
    // Audit 09/11, W11 : la copie a échoué (presse-papiers tenu par une autre application).
    private bool _copyFailed;

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
    public string UiLanguage { get; private set; } = L.Language;

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
        ConfigManager.AppLanguageChanged += OnLanguageChanged;
        ConfigManager.TrainingEnabledChanged += OnTrainingEnabledChanged;
        CreateControls();
        ApplyFontsToControls();

        if (NativeWindow.CorrectedScale(Win32.GetDpiForWindow(_hWnd), _dpiScale) is float windowScale)
        {
            _dpiScale = windowScale;
            // Mise en page seule : GetDpiForWindow ne lève pas (audit du 25/09, X-04).
            try
            {
                RecreateFonts();
                FitWindow();
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
        Win32.SendMessageW(_hWndBtnRate, Win32.WM_SETFONT, FontText.Handle, (IntPtr)1);
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

        uint dwStyle = Style;
        var (windowW, windowH) = NativeWindow.OuterSize(S(BASE_WIN_W), S(WinContentH), dwStyle);
        var (x, y) = NativeWindow.CenterIn(NativeWindow.WorkArea(), windowW, windowH);

        _hWnd = Win32.CreateWindowExW(0, className, L.Stats_WindowTitle,
            dwStyle, x, y, windowW, windowH,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        _hBgBrush = NativeWindow.ApplyClassBackground(_hWnd, _pal.Paper);

        // AG130-40 : cette fenetre veut Tab, Maj+Tab et Entree entre ses controles.
        // A1 : Tab vers un contrôle sous le pli le fait défiler jusqu'à lui ; Page haut et bas,
        // Début et Fin défilent quand le focus est sur un bouton. Le défilement repeint le texte.
        _scroll.Scrolled = () => Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
        DialogNavigation.Register(_hWnd, control => _scroll.EnsureVisible(_hWnd, control, S(8)),
            (in Win32.MSG key) => _scroll.OnControlKey(_hWnd, key, S(24)) || TryCopyKey(key.wParam));
        NativeWindow.ApplyFrame(_hWnd, dark: false);
    }

    private void CreateControls()
    {
        var hInstance = Win32.GetModuleHandleW(null);

        // Stratégie OutboundLinksEnabled à 0 (1.3.0) : « Donner mon avis » aussi est masqué, par
        // le même procédé que Discord ci-dessous — créé, jamais affiché.
        uint feedbackStyle = Win32.WS_CHILD | Win32.SS_NOTIFY | Win32.WS_TABSTOP;
        if (PolicyManager.OutboundLinksEnabledNow) feedbackStyle |= Win32.WS_VISIBLE;
        _hWndLinkFeedback = Win32.CreateWindowExW(0, "STATIC", L.Stats_LinkFeedback,
            feedbackStyle,
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

        // C1 : « Noter l'application », créé après « Copier » (ordre de tabulation) et masqué
        // quand il n'y a pas de fiche à noter ; FitWindow règle sa visibilité à chaque mise en page.
        _hWndBtnRate = Win32.CreateWindowExW(0, "BUTTON", L.Stats_BtnRate,
            Win32.WS_CHILD | Win32.WS_TABSTOP,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_RATE, hInstance, IntPtr.Zero);

        _hWndBtnClose = Win32.CreateWindowExW(0, "BUTTON", L.Common_Close,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | 0x0001 /* BS_DEFPUSHBUTTON */,
            0, 0, 0, 0,
            _hWnd, (IntPtr)IDC_BTN_CLOSE, hInstance, IntPtr.Zero);
        // Audit UX 1.3.0, #3 : boutons clairs de Windows 11, « Fermer » porte l'accent.
        // A9 : boutons papier ; en contraste élevé, le dessin du système (T5 : décidé au dessin).
        LightButton.Attach(_hWndBtnCopy, paper: true);
        LightButton.Attach(_hWndBtnRate, paper: true);
        LightButton.Attach(_hWndBtnClose, paper: true);

        FitWindow();
    }

    /// <summary>
    /// A1 : la fenêtre à sa largeur de référence plafonnée à la zone de travail de son écran, et à
    /// la hauteur de son contenu mesuré, plafonnée de même ; au-delà, le contenu défile (barre,
    /// molette, clavier). Même mécanique que l'Accueil et les Paramètres.
    /// </summary>
    private void FitWindow()
    {
        var work = NativeWindow.WorkArea(_hWnd);
        var (frameW, frameH) = NativeWindow.NonClientSize(_hWnd, Style);
        // La dernière mise en page de FitScrollable est celle de fit.LayoutWidth : les contrôles
        // sont déjà à leur place.
        var fit = WindowSizing.FitScrollable(S(BASE_WIN_W), PlaceControls, frameW, frameH,
            work.right - work.left, work.bottom - work.top, NativeWindow.ScrollBarWidth(_hWnd));

        NativeWindow.ResizeAroundCenter(_hWnd, fit.ClientWidth, fit.ClientHeight, Style);
        NativeWindow.KeepInWorkArea(_hWnd);
        _scroll.Reset(_hWnd, fit.ContentHeight, fit.ClientHeight);
    }

    /// <summary>
    /// Les contrôles à la largeur de mise en page donnée, posés à l'origine (sans défilement) ;
    /// rend la hauteur du contenu. Le texte est mesuré à cette largeur, avec la plus longue des
    /// deux phrases du pied (« copié » ou confidentialité) : l'afficher ou l'effacer ne déplace
    /// donc jamais les boutons.
    /// </summary>
    private int PlaceControls(int layoutW)
    {
        bool rate = RateVisibleNow;
        var rows = ComputeRows(MeasureTextBottom(layoutW), S(WinContentH), rate, S);
        int margin = S(20), btnH = S(32);

        int btnCloseW = S(90);
        int btnCloseX = layoutW - margin - btnCloseW;
        Win32.MoveWindow(_hWndBtnClose, btnCloseX, rows.ButtonY, btnCloseW, btnH, true);

        int btnCopyW = S(210);
        int btnCopyX = btnCloseX - S(12) - btnCopyW;
        Win32.MoveWindow(_hWndBtnCopy, btnCopyX, rows.ButtonY, btnCopyW, btnH, true);

        Win32.ShowWindow(_hWndBtnRate, rate ? 5 : 0);
        if (rate)
            Win32.MoveWindow(_hWndBtnRate, btnCopyX, rows.RateY, btnCopyW, btnH, true);

        // Liens retours/communauté sur une ligne au-dessus des boutons
        int linkH = S(22);
        int wFeedback = S(130);
        int wDiscord = S(240);
        int linkGap = S(24);
        int linksX = (layoutW - wFeedback - wDiscord - linkGap) / 2;
        Win32.MoveWindow(_hWndLinkFeedback, linksX, rows.LinkY, wFeedback, linkH, true);
        Win32.MoveWindow(_hWndLinkDiscord, linksX + wFeedback + linkGap, rows.LinkY, wDiscord, linkH, true);
        return rows.ContentHeight;
    }

    /// <summary>Bas du texte peint à la largeur donnée : la peinture elle-même, sur un bitmap d'un
    /// pixel (tout est rogné, seul le y final compte).</summary>
    private int MeasureTextBottom(int width)
    {
        var (dc, bitmap, previous) = PaintBuffer.CreateSurface(Math.Max(1, width), 1);
        bool ownDc = dc == IntPtr.Zero;
        if (ownDc) dc = Win32.GetDC(_hWnd);
        try
        {
            var area = new Win32.RECT { left = 0, top = 0, right = Math.Max(1, width), bottom = 1 };
            return Math.Max(Math.Max(DrawContents(dc, area, copiedFeedback: false),
                                     DrawContents(dc, area, copiedFeedback: true)),
                            DrawContents(dc, area, copiedFeedback: false, copyFailed: true));
        }
        finally
        {
            if (ownDc) Win32.ReleaseDC(_hWnd, dc);
            else PaintBuffer.ReleaseSurface(dc, bitmap, previous);
        }
    }

    /// <summary>Affiche la fenêtre. Les compteurs sont relus à chaque ouverture (pas de cache).</summary>
    public void Show()
    {
        RefreshPalette(); // T5 : le réglage a pu changer fenêtre cachée
        _showCopiedFeedback = false;
        _copyFailed = false;
        // CE-T2 : un usage-stats.json resté illisible au démarrage est relu ici, pour que la
        // fenêtre montre le cumul et pas les seuls compteurs de la session.
        UsageStats.Preload();
        // La visibilité de la section « Défi du jour » peut changer entre deux ouvertures :
        // la hauteur se recalcule à chaque affichage ; la fenêtre se rouvre en haut.
        FitWindow();
        NativeWindow.KeepInWorkArea(_hWnd); // T6 (EW-18) : l'écran de la dernière ouverture a pu partir
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
        Win32.ShowWindow(_hWnd, 1);
        Win32.SetForegroundWindow(_hWnd);
        _visible = true;
    }

    /// <summary>
    /// C1 (1.3.0) : demandé par le bouton « Noter l'application » seulement. « Copier » ne
    /// sollicite plus d'avis : la demande part d'un geste explicite, jamais d'un partage.
    /// Reçoit la fenêtre propriétaire de la boîte de notation (cette fenêtre).
    /// </summary>
    public Action<IntPtr>? RateRequested;

    public void Close()
    {
        // M5.6 I2 (1.3.0) : la minuterie du retour « Copié » meurt avec la fermeture, au lieu
        // de survivre jusqu'à son propre WM_TIMER ; le retour s'efface avec elle, pour qu'une
        // réouverture ne montre pas un « Copié » d'une copie passée.
        Win32.KillTimer(_hWnd, (UIntPtr)TIMER_COPY_FEEDBACK);
        _showCopiedFeedback = false;
        _copyFailed = false;
        Win32.ShowWindow(_hWnd, 0);
        _visible = false;
    }

    /// <summary>
    /// T5 (EW-15) : relit la palette (contraste élevé activé ou coupé fenêtre ouverte) ;
    /// fond, pinceaux, barre de titre et contrôles suivent. Rien quand elle n'a pas changé.
    /// </summary>
    private void RefreshPalette()
    {
        if (_hWnd == IntPtr.Zero || !NativeWindow.RefreshPalette(_hWnd, ref _pal))
            return;
        _hBgBrush = NativeWindow.ApplyClassBackground(_hWnd, _pal.Paper);
        NativeWindow.RepaintAll(_hWnd);
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            // T5 (EW-15) : contraste élevé ou couleurs du système changés, fenêtre ouverte.
            if (NativeWindow.IsThemeChange(msg))
                RefreshPalette();
            // T6 (EW-18) : écran débranché, définition ou zone de travail changée : la fenêtre,
            // affichée ou cachée, revient dans la zone de travail de son écran.
            if (NativeWindow.IsDisplayChange(msg, wParam))
                NativeWindow.KeepInWorkArea(hWnd);
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
                    FitWindow();
                    Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                    return IntPtr.Zero;
                }

                case Win32.WM_VSCROLL:
                case Win32.WM_MOUSEWHEEL:
                    if (_scroll.TryHandle(_hWnd, msg, wParam, S(24)))
                        return IntPtr.Zero;
                    break;

                case Win32.WM_COMMAND:
                {
                    int id = wParam.ToInt32() & 0xFFFF;
                    int code = (wParam.ToInt32() >> 16) & 0xFFFF;
                    if (code != 0) break;
                    switch (id)
                    {
                        // Revue du 2026-09-21, R5 : Entrée et Échap arrivent d'IsDialogMessageW
                        // en WM_COMMAND. Entrée sur « Copier » copie, sur « Noter » note, ailleurs elle
                        // ferme ; Échap ferme.
                        case DialogNavigation.IDOK:
                        {
                            IntPtr pressed = DialogNavigation.ButtonToPressOnEnter(id, Win32.GetFocus(),
                                new[] { _hWndBtnCopy, _hWndBtnRate });
                            if (pressed == _hWndBtnCopy && pressed != IntPtr.Zero)
                                OnCopyStats();
                            else if (pressed == _hWndBtnRate && pressed != IntPtr.Zero)
                                OnRate();
                            else
                                Close();
                            break;
                        }
                        case DialogNavigation.IDCANCEL:
                            Close();
                            break;
                        case IDC_BTN_COPY:
                            OnCopyStats();
                            break;
                        case IDC_BTN_RATE:
                            OnRate();
                            break;
                        case IDC_BTN_CLOSE:
                            Close();
                            break;
                        case IDC_LINK_FEEDBACK:
                            OutboundLinks.Open(
                                ProductIdentity.DiagnosticUrl(ProductIdentity.FeedbackPath, Program.Version,
                                    ProductIdentity.OsDescription(), "stats"));
                            break;
                        case IDC_LINK_DISCORD:
                            OutboundLinks.Open(ProductIdentity.DiscordInviteUrl);
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
                        _copyFailed = false;
                        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                    }
                    return IntPtr.Zero;

                case Win32.WM_KEYDOWN:
                    if (_scroll.TryHandle(_hWnd, msg, wParam, S(24)))
                        return IntPtr.Zero;
                    // Audit 09/11, E29 : Ctrl+C copie les statistiques, comme « Copier mes statistiques ».
                    if (TryCopyKey(wParam))
                        return IntPtr.Zero;
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
        // W11 : un échec se dit (le presse-papiers peut être tenu par une autre application), avec
        // la même durée que « Copié ».
        bool copied = CopyToClipboard(UsageStats.BuildShareText());
        _showCopiedFeedback = copied;
        _copyFailed = !copied;
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
        Win32.SetTimer(_hWnd, (UIntPtr)TIMER_COPY_FEEDBACK, 1500, IntPtr.Zero);
    }

    /// <summary>Point d'appui du presse-papiers : les tests le remplacent pour simuler un échec.</summary>
    internal Func<IntPtr, string, bool> ClipboardWriter = ClipboardText.TrySet;

    private bool CopyToClipboard(string text) => ClipboardWriter(_hWnd, text);

    /// <summary>Ctrl+C : le focus est presque toujours sur un bouton, dont la touche passe par le filtre de
    /// DialogNavigation avant d'atteindre la fenêtre.</summary>
    private bool TryCopyKey(IntPtr wParam)
    {
        if (!IsCopyChord((int)wParam, CtrlHeld(), AltHeld())) return false;
        OnCopyStats();
        return true;
    }

    /// <summary>Ctrl+C, sans Alt : Alt Gr (Ctrl + Alt) n'est pas un Ctrl.</summary>
    internal static bool IsCopyChord(int vk, bool ctrl, bool alt) => vk == 0x43 && ctrl && !alt;

    private static bool CtrlHeld() => (TypingEngine.Windows.Win32.GetKeyState(0x11) & unchecked((short)0x8000)) != 0;
    private static bool AltHeld() => (TypingEngine.Windows.Win32.GetKeyState(0x12) & unchecked((short)0x8000)) != 0;

    /// <summary>Audit 09/11, W8 : la langue change pendant que la fenêtre est ouverte. Les textes
    /// peints suivent d'eux-mêmes ; le titre, les liens et les boutons portaient celui de la création.</summary>
    internal void OnLanguageChanged(string language)
    {
        if (_hWnd == IntPtr.Zero) return;
        UiLanguage = L.Language;
        Win32.SetWindowTextW(_hWnd, L.Stats_WindowTitle);
        Win32.SetWindowTextW(_hWndLinkFeedback, L.Stats_LinkFeedback);
        Win32.SetWindowTextW(_hWndLinkDiscord, L.Stats_LinkDiscord);
        Win32.SetWindowTextW(_hWndBtnCopy, L.Stats_BtnCopy);
        Win32.SetWindowTextW(_hWndBtnRate, L.Stats_BtnRate);
        Win32.SetWindowTextW(_hWndBtnClose, L.Common_Close);
        FitWindow(); // les textes n'ont plus la même longueur : mise en page et hauteur refaites
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    /// <summary>ME-07, C5.5 : l'opt-in du Défi change pendant que la fenêtre est ouverte (les
    /// Paramètres ne sont pas modaux). La peinture relit le prédicat de la section ; la hauteur et
    /// les boutons, eux, ne se calculaient qu'à l'ouverture : on refait la mise en page.</summary>
    internal void OnTrainingEnabledChanged(bool enabled)
    {
        if (_hWnd == IntPtr.Zero) return;
        FitWindow();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }

    /// <summary>C1 : « Noter l'application » — la demande d'avis vient de ce clic, et d'aucun
    /// autre geste de la fenêtre. TrayApplication ouvre la boîte de notation du Store.</summary>
    private void OnRate()
    {
        try { RateRequested?.Invoke(_hWnd); }
        catch (Exception ex) { ConfigManager.Log("UsageStatsWindow.RateRequested", ex); }
    }

    // La copie elle-même vit dans ClipboardText.TrySet depuis la v1.2.0, avec le même
    // filet de restauration : mémoriser l'ancien contenu pour le remettre si
    // SetClipboardData échoue après EmptyClipboard.

    private void OnPaint(IntPtr hWnd)
    {
        if (_scroll.Needed)
        {
            OnPaintScrolled(hWnd);
            return;
        }
        using var paint = new PaintBuffer(hWnd);
        DrawContents(paint.Hdc, paint.Client, _showCopiedFeedback, _copyFailed);
    }

    /// <summary>
    /// A1 : fenêtre qui défile — tout le texte est dessiné dans un bitmap de la hauteur du contenu,
    /// et la partie visible en est copiée (même méthode que l'Accueil et les Leçons). Les boutons et
    /// les liens, eux, sont décalés par <see cref="WindowScroll"/>.
    /// </summary>
    private void OnPaintScrolled(IntPtr hWnd)
    {
        IntPtr target = Win32.BeginPaint(hWnd, out var ps);
        try
        {
            Win32.GetClientRect(hWnd, out var client);
            var canvas = new Win32.RECT { left = 0, top = 0, right = client.right,
                bottom = Math.Max(_scroll.ContentHeight, client.bottom) };
            var (dc, bitmap, previous) = PaintBuffer.CreateSurface(canvas.right, canvas.bottom);
            if (dc == IntPtr.Zero)
            {
                // Mémoire refusée : directement à l'écran, origine décalée du défilement.
                Win32.SetViewportOrgEx(target, 0, -_scroll.Position, IntPtr.Zero);
                DrawContents(target, canvas, _showCopiedFeedback, _copyFailed);
                Win32.SetViewportOrgEx(target, 0, 0, IntPtr.Zero);
                return;
            }
            try
            {
                DrawContents(dc, canvas, _showCopiedFeedback, _copyFailed);
                Win32.BitBlt(target, 0, 0, client.right, client.bottom, dc, 0, _scroll.Position, Win32.SRCCOPY);
            }
            finally { PaintBuffer.ReleaseSurface(dc, bitmap, previous); }
        }
        finally { Win32.EndPaint(hWnd, ref ps); }
    }

    /// <summary>
    /// Le texte de la fenêtre dans <paramref name="clientRect"/> (origine en haut à gauche) ; rend
    /// le bas du dernier paragraphe, que <see cref="PlaceControls"/> mesure pour poser les liens et
    /// les boutons dessous. <paramref name="copiedFeedback"/> choisit la phrase du pied.
    /// </summary>
    private int DrawContents(IntPtr hdc, Win32.RECT clientRect, bool copiedFeedback, bool copyFailed = false)
    {
        int cw = clientRect.right;

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

        string reassurance = copiedFeedback
            ? L.Stats_CopiedFeedback
            : copyFailed
            ? L.Stats_CopyFailed
            : collectionOff
                ? L.Stats_CollectionOffPrivacy
                : L.Stats_PrivacyReassurance;
        y = DrawWrappedLine(hdc, FontMuted, reassurance, margin, y, cw - margin * 2, S(16),
            copiedFeedback ? _pal.Action : copyFailed ? _pal.Error : _pal.Muted);

        if (PolicyManager.CollectionPolicyHintNow)
            y = DrawWrappedLine(hdc, FontMuted, L.Stats_CollectionOffPolicyHint, margin, y, cw - margin * 2, S(16), _pal.Muted);
        return y;
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

        var lastSession = StoredDate.Parse(ConfigManager.TrainingLastSessionDate);
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

    public void Dispose()
    {
        ConfigManager.AppLanguageChanged -= OnLanguageChanged;
        ConfigManager.TrainingEnabledChanged -= OnTrainingEnabledChanged;
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
