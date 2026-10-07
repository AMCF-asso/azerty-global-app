// Clavier virtuel — affiche la disposition AZERTY Global en temps réel
using System.Runtime.InteropServices;

namespace AZERTYGlobal;

/// <summary>
/// Fenêtre Win32 affichant le clavier AZERTY Global.
/// Couche active : chaque touche montre ce qu'elle tape avec les modificateurs tenus.
/// </summary>
sealed class VirtualKeyboard : IDisposable
{
    // Refonte 1.3.0 (maquette A du 07/10, « couche active ») : papier clair, un signe par
    // touche ; les couleurs viennent de PaperTheme, le clavier de PaperKeyboard.

    // ── Mapping Web API key code → scancode ─────────────────────
    internal static readonly Dictionary<string, uint> KeyCodeToScancode = new()
    {
        ["Backquote"] = 0x29,
        ["Digit1"] = 0x02, ["Digit2"] = 0x03, ["Digit3"] = 0x04, ["Digit4"] = 0x05,
        ["Digit5"] = 0x06, ["Digit6"] = 0x07, ["Digit7"] = 0x08, ["Digit8"] = 0x09,
        ["Digit9"] = 0x0A, ["Digit0"] = 0x0B, ["Minus"] = 0x0C, ["Equal"] = 0x0D,
        ["KeyQ"] = 0x10, ["KeyW"] = 0x11, ["KeyE"] = 0x12, ["KeyR"] = 0x13,
        ["KeyT"] = 0x14, ["KeyY"] = 0x15, ["KeyU"] = 0x16, ["KeyI"] = 0x17,
        ["KeyO"] = 0x18, ["KeyP"] = 0x19, ["BracketLeft"] = 0x1A, ["BracketRight"] = 0x1B,
        ["KeyA"] = 0x1E, ["KeyS"] = 0x1F, ["KeyD"] = 0x20, ["KeyF"] = 0x21,
        ["KeyG"] = 0x22, ["KeyH"] = 0x23, ["KeyJ"] = 0x24, ["KeyK"] = 0x25,
        ["KeyL"] = 0x26, ["Semicolon"] = 0x27, ["Quote"] = 0x28, ["Backslash"] = 0x2B,
        ["IntlBackslash"] = 0x56,
        ["KeyZ"] = 0x2C, ["KeyX"] = 0x2D, ["KeyC"] = 0x2E, ["KeyV"] = 0x2F,
        ["KeyB"] = 0x30, ["KeyN"] = 0x31, ["KeyM"] = 0x32,
        ["Comma"] = 0x33, ["Period"] = 0x34, ["Slash"] = 0x35,
        ["Space"] = 0x39,
    };

    // ═══════════════════════════════════════════════════════════════
    // Structures spécifiques (tooltip)
    // ═══════════════════════════════════════════════════════════════
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TOOLINFOW
    {
        public uint cbSize;
        public uint uFlags;
        public IntPtr hwnd;
        public UIntPtr uId;
        public Win32.RECT rect;
        public IntPtr hinst;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszText;
        public IntPtr lParam;
    }

    // ═══════════════════════════════════════════════════════════════
    // Données de géométrie du clavier ISO
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Représente une touche visuelle sur le clavier.</summary>
    internal const string ContextShiftLeft = "ShiftLeft";
    internal const string ContextShiftRight = "ShiftRight";

    internal record struct VisualKey(
        float X, float Y, float W, float H,   // Position/taille en unités (1u = largeur touche standard)
        uint Scancode,                          // 0 = touche contextuelle (non remappée)
        string Label,                           // Identifiant interne (repère AZERTY) ; texte dessiné : L.Keyboard_KeyCap
        bool IsContextual,                      // Tab, Shift, Ctrl, etc.
        string? ContextId = null                // Identifiant interne invisible (ex: ShiftLeft)
    );

    // Dimensions de référence (en unités de touche)
    internal const float KEY_H = 1f;
    internal const float KEY_GAP = 0.1f;
    internal const float ROW_GAP = 0.1f;

    // Noms français des touches mortes — source de vérité : tester/deadkeys.js DEAD_KEY_NAMES_FR
    // (testeur web AZERTY Global). Doit rester aligné pour cohérence cross-canal.
    internal static readonly Dictionary<string, string> _deadKeyNamesFr = new()
    {
        ["dk_circumflex"] = "Accent circonflexe",
        ["dk_diaeresis"] = "Tréma",
        ["dk_acute"] = "Accent aigu",
        ["dk_grave"] = "Accent grave",
        ["dk_tilde"] = "Tilde",
        ["dk_dot_above"] = "Point en chef",
        ["dk_dot_below"] = "Point souscrit",
        ["dk_double_acute"] = "Double accent aigu",
        ["dk_double_grave"] = "Double accent grave",
        ["dk_horn"] = "Cornu",
        ["dk_hook"] = "Crochet en chef",
        ["dk_caron"] = "Caron",
        ["dk_ogonek"] = "Ogonek",
        ["dk_breve"] = "Brève",
        ["dk_inverted_breve"] = "Brève inversée",
        ["dk_stroke"] = "Barre oblique",
        ["dk_horizontal_stroke"] = "Barre horizontale",
        ["dk_macron"] = "Macron",
        ["dk_extended_latin"] = "Latin étendu",
        ["dk_cedilla"] = "Cédille",
        ["dk_comma"] = "Virgule souscrite",
        ["dk_phonetic"] = "Alphabet phonétique",
        ["dk_ring_above"] = "Rond en chef",
        ["dk_greek"] = "Alphabet grec",
        ["dk_cyrillic"] = "Alphabet cyrillique",
        ["dk_cyrillic_ext"] = "Cyrillique étendu",
        ["dk_misc_symbols"] = "Symboles divers",
        ["dk_scientific"] = "Symboles scientifiques",
        ["dk_currencies"] = "Symboles monétaires",
        ["dk_punctuation"] = "Symboles de ponctuation",
    };

    /// <summary>Retourne le nom d'affichage d'une touche morte, dans la langue courante de l'UI.</summary>
    internal static string GetDeadKeyDisplayName(string deadKeyId)
    {
        var dict = L.IsEnglish ? L.DeadKeyNamesEn : _deadKeyNamesFr;
        return dict.TryGetValue(deadKeyId, out var name) ? name.ToUpperInvariant() : deadKeyId;
    }

    // Taille de la fenêtre par défaut : celle de la maquette A (854 de large ; bandeau de 78,
    // clavier de 266, marge basse de 24). Toutes les mesures du dessin s'y rapportent.
    private const int DEFAULT_WIDTH = 854;
    private const int DEFAULT_HEIGHT = 368;
    private const int MIN_WIDTH = 480;
    private const int MIN_HEIGHT = 207; // MIN_WIDTH / ASPECT_RATIO
    private const float ASPECT_RATIO = 854f / 368f; // ~2.32

    // Mesures de la maquette, en px à la taille par défaut.
    private const float MockMargin = 24f;        // marges gauche, droite et basse
    private const float MockBannerHeight = 78f;  // bandeau d'état, portée du geste comprise
    private const float MockStateBannerHeight = 64f; // bandeau sans geste (min-height: 64px)
    private const float MockBannerPadTop = 4f;
    private const float MockBannerPadBottom = 12f;
    private const float MockBannerGap = 28f;     // entre la portée et la phrase
    private const float MockStaffLabel = 52f;    // colonne « Maj » / « AltGr »
    private const float MockStaffLine = 150f;
    private const float MockStaffRow = 16f;
    private const float MockStaffBar = 7f;       // trait épais d'un modificateur tenu
    private const int MockTextSize = 15;         // phrase : 15 px, interligne 1,45
    private const float MockTextLine = 21.75f;
    private const float MockTextMaxWidth = 405f; // max-width: 54ch (chiffre de 7,5 px en AG Texte 15)

    private static readonly VisualKey[] _visualKeys = BuildKeyLayout();

    internal static VisualKey[] BuildKeyLayout()
    {
        var keys = new List<VisualKey>();
        float y = 0;

        // ── Rangée 1 : Chiffres (13 touches + Retour arrière) ──
        float x = 0;
        string[] row1Labels = { "²", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", ")", "=" };
        uint[] row1Scans = { 0x29, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D };
        for (int i = 0; i < 13; i++)
        {
            keys.Add(new VisualKey(x, y, 1f, KEY_H, row1Scans[i], row1Labels[i], false));
            x += 1f + KEY_GAP;
        }
        keys.Add(new VisualKey(x, y, 2f, KEY_H, 0x0E, "⌫", true)); // Backspace
        y += KEY_H + ROW_GAP;

        // ── Rangée 2 : AZER (Tab + 12 touches + Enter partiel) ──
        x = 0;
        keys.Add(new VisualKey(x, y, 1.5f, KEY_H, 0x0F, "Tab", true));
        x = 1.5f + KEY_GAP;
        string[] row2Labels = { "A", "Z", "E", "R", "T", "Y", "U", "I", "O", "P", "^", "$" };
        uint[] row2Scans = { 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B };
        for (int i = 0; i < 12; i++)
        {
            keys.Add(new VisualKey(x, y, 1f, KEY_H, row2Scans[i], row2Labels[i], false));
            x += 1f + KEY_GAP;
        }
        // Entrée ISO (partie haute) — touche verticale sur 2 rangées
        keys.Add(new VisualKey(x, y, 1.5f, KEY_H * 2 + ROW_GAP, 0x1C, "Entrée", true));
        y += KEY_H + ROW_GAP;

        // ── Rangée 3 : QSDF (Verr.Maj + 12 touches) ──
        x = 0;
        keys.Add(new VisualKey(x, y, 1.75f, KEY_H, 0x3A, "Verr. Maj.", true));
        x = 1.75f + KEY_GAP;
        string[] row3Labels = { "Q", "S", "D", "F", "G", "H", "J", "K", "L", "M", "ù", "*" };
        uint[] row3Scans = { 0x1E, 0x1F, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x2B };
        for (int i = 0; i < 12; i++)
        {
            keys.Add(new VisualKey(x, y, 1f, KEY_H, row3Scans[i], row3Labels[i], false));
            x += 1f + KEY_GAP;
        }
        // L'Entrée ISO est déjà dessinée (s'étend sur cette rangée)
        y += KEY_H + ROW_GAP;

        // ── Rangée 4 : WXCV (Shift G + B00 + 10 touches + Shift D) ──
        x = 0;
        keys.Add(new VisualKey(x, y, 1.25f, KEY_H, 0, "Maj ⇧", true, ContextShiftLeft)); // Left Shift
        x = 1.25f + KEY_GAP;
        string[] row4Labels = { "<", "W", "X", "C", "V", "B", "N", ",", ".", ":", "!" };
        uint[] row4Scans = { 0x56, 0x2C, 0x2D, 0x2E, 0x2F, 0x30, 0x31, 0x32, 0x33, 0x34, 0x35 };
        for (int i = 0; i < 11; i++)
        {
            keys.Add(new VisualKey(x, y, 1f, KEY_H, row4Scans[i], row4Labels[i], false));
            x += 1f + KEY_GAP;
        }
        keys.Add(new VisualKey(x, y, 2.85f, KEY_H, 0, "Maj ⇧", true, ContextShiftRight)); // Right Shift
        y += KEY_H + ROW_GAP;

        // ── Rangée 5 : Espace (Ctrl, Win, Alt, Espace, AltGr, Menu, Ctrl) ──
        x = 0;
        keys.Add(new VisualKey(x, y, 1.25f, KEY_H, 0x1D, "Ctrl", true));   // LCtrl
        x += 1.25f + KEY_GAP;
        keys.Add(new VisualKey(x, y, 1.25f, KEY_H, 0x5B, "Win", true));   // LWin (scancode 0x5B)
        x += 1.25f + KEY_GAP;
        keys.Add(new VisualKey(x, y, 1.25f, KEY_H, 0, "Alt", true));      // LAlt (état maintenu via _altDown)
        x += 1.25f + KEY_GAP;
        keys.Add(new VisualKey(x, y, 6.25f, KEY_H, 0x39, "Espace", false)); // Space
        x += 6.25f + KEY_GAP;
        keys.Add(new VisualKey(x, y, 1.25f, KEY_H, 0, "AltGr", true));     // RAlt (état maintenu via _altGrDown)
        x += 1.25f + KEY_GAP;
        keys.Add(new VisualKey(x, y, 1.25f, KEY_H, 0x5C, "Win", true));  // RWin (scancode 0x5C)
        x += 1.25f + KEY_GAP;
        keys.Add(new VisualKey(x, y, 1.25f, KEY_H, 0x5D, "Menu", true));  // Menu (scancode 0x5D)
        x += 1.25f + KEY_GAP;
        keys.Add(new VisualKey(x, y, 1.85f, KEY_H, 0x1D, "Ctrl", true));  // RCtrl (aligné bord droit)

        return keys.ToArray();
    }

    // ═══════════════════════════════════════════════════════════════
    // Champs d'instance
    // ═══════════════════════════════════════════════════════════════
    private IntPtr _hWnd;
    private readonly Win32.WNDPROC _wndProcDelegate;
    private readonly Layout _layout;

    private bool _visible;
    private IntPtr _preferredMonitorWindow;
    private bool _trackingMouse;
    private int _hoveredKeyIndex = -1;

    // État des modificateurs (mis à jour par TrayApplication depuis KeyMapper)
    private bool _shiftDown;
    private bool _altGrDown;
    private bool _ctrlDown;
    private bool _altDown;
    private bool _capsLockActive;
    private string? _activeDeadKey;

    // Animation d'appui sur touche
    private static readonly UIntPtr TIMER_KEYPRESS = (UIntPtr)1;
    private const uint KEYPRESS_DURATION_MS = 120;
    private uint _pressedScancode; // 0 = aucune touche pressée

    /// <summary>Geste venu de la recherche : ses temps (pastilles 1 et 2 sur le clavier,
    /// portée dans le bandeau), le caractère et son nom, le signe de la touche morte qu'il
    /// arme (« Frappez ù (´) »). Montré jusqu'au prochain choix de la recherche.</summary>
    private sealed record ShownGesture(IReadOnlyList<GestureStep> Steps, string? Character, string? Name,
        int ArmStep, string? DeadKeySign);
    private ShownGesture? _gesture;

    /// <summary>La disposition sans aucun signe : Ctrl ou Alt tenu, les touches ne tapent rien.</summary>
    private Layout? _blankLayout;

    // Tooltip
    private IntPtr _hTooltip;

    public bool IsVisible => _visible;

    // ── Ancienne géométrie du clavier (encore utilisée par le tutoriel) ──

    internal const float TOTAL_KEY_W = 16.3f; // Largeur max d'une rangée en unités
    internal const int KB_MARGIN = 10;

    internal record struct KeyboardGeometry(
        float Scale, float KbWidth, float KbHeight,
        int OffsetX, int OffsetY, int BottomReserve);

    internal static KeyboardGeometry GetKeyboardGeometry(int cw, int ch)
    {
        float totalKeyH = 5 * KEY_H + 4 * ROW_GAP;
        float scaleX = (cw - 2 * KB_MARGIN) / TOTAL_KEY_W;
        float scaleY = (ch - 2 * KB_MARGIN) / totalKeyH;
        float scale = Math.Min(scaleX, scaleY);
        int bottomReserve = Math.Max(20, (int)(scale * 0.55f));
        float kbWidth = TOTAL_KEY_W * scale;
        float kbHeight = totalKeyH * scale;
        int offsetX = KB_MARGIN + (int)((cw - 2 * KB_MARGIN - kbWidth) / 2);
        int offsetY = KB_MARGIN + (int)((ch - 2 * KB_MARGIN - bottomReserve - kbHeight) / 2);
        return new KeyboardGeometry(scale, kbWidth, kbHeight, offsetX, offsetY, bottomReserve);
    }

    /// <summary>Échelle du dessin : la taille client rapportée à celle de la maquette. La
    /// fenêtre garde le ratio de la maquette ; un écran à 150 % l'agrandit (WM_DPICHANGED), et
    /// tout grandit avec elle.</summary>
    private static float PaintScale(int cw, int ch) => Math.Max(0.1f, Math.Min(cw / (float)DEFAULT_WIDTH, ch / (float)DEFAULT_HEIGHT));

    private static int Px(float value, float scale) => (int)Math.Round(value * scale);

    /// <summary>Le cadre du clavier, sous le bandeau ; c'est aussi celui des zones de survol
    /// (<see cref="KeyboardRenderer.BuildHitTestRects(KeyboardPlacement)"/>).</summary>
    private static KeyboardPlacement KeyboardPlacementFor(int cw, int ch)
    {
        float s = PaintScale(cw, ch);
        var bounds = new Win32.RECT
        {
            left = Px(MockMargin, s),
            top = Px(MockBannerHeight, s),
            right = cw - Px(MockMargin, s),
            bottom = ch - Px(MockMargin, s),
        };
        return PaperKeyboard.Place(bounds);
    }

    /// <summary>« AZERTY Global, Clavier virtuel », comme sur la maquette.</summary>
    private static string WindowTitle => $"{ProductIdentity.DisplayName}, {L.VirtualKeyboardPaper_Title}";

    private const uint DWMWA_CAPTION_COLOR = 35;
    private const uint DWMWA_TEXT_COLOR = 36;

    /// <summary>Barre de titre claire, couleur du papier (Windows 11 ; ignoré avant).</summary>
    private static void ApplyPaperFrame(IntPtr hWnd)
    {
        NativeWindow.ApplyFrame(hWnd, dark: false);
        if (hWnd == IntPtr.Zero) return;
        int caption = (int)PaperTheme.Paper;
        int text = (int)PaperTheme.TextSecondary;
        Win32.DwmSetWindowAttribute(hWnd, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
        Win32.DwmSetWindowAttribute(hWnd, DWMWA_TEXT_COLOR, ref text, sizeof(int));
    }

    public VirtualKeyboard(Layout layout)
    {
        _layout = layout;
        _wndProcDelegate = WndProc;

        var hInstance = Win32.GetModuleHandleW(null);
        var className = ProductIdentity.WindowClass("VK");
        // Audit du 25/09, X-01 : une classe refusée ne crée pas de fenêtre.
        if (!NativeWindow.RegisterClass(className, _wndProcDelegate, 0x0020 /* CS_OWNDC */))
            return;

        // Calculer la taille de fenêtre pour obtenir la zone client souhaitée
        uint dwStyle = Win32.WS_POPUP | Win32.WS_THICKFRAME | Win32.WS_CAPTION | Win32.WS_SYSMENU;
        uint dwExStyle = Win32.WS_EX_TOPMOST | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE;
        var adjustRect = new Win32.RECT { left = 0, top = 0, right = DEFAULT_WIDTH, bottom = DEFAULT_HEIGHT };
        Win32.AdjustWindowRectEx(ref adjustRect, dwStyle, false, dwExStyle);
        int windowW = adjustRect.right - adjustRect.left;
        int windowH = adjustRect.bottom - adjustRect.top;

        var workArea = GetActiveMonitorWorkArea();
        int posX = workArea.left + Math.Max(0, (workArea.right - workArea.left - windowW) / 2);
        int posY = workArea.top + Math.Max(0, (workArea.bottom - workArea.top - windowH) / 2);

        _hWnd = Win32.CreateWindowExW(
            dwExStyle,
            className,
            WindowTitle,
            dwStyle,
            posX, posY, windowW, windowH,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        ApplyPaperFrame(_hWnd);

        CreateTooltip();
        ConfigManager.WindowBoundsCleared += OnWindowBoundsCleared;
        ConfigManager.AppLanguageChanged += OnAppLanguageChanged;
    }

    // ═══════════════════════════════════════════════════════════════
    // Tooltip Win32
    // ═══════════════════════════════════════════════════════════════
    private const uint TTS_ALWAYSTIP = 0x01;
    private const uint TTS_NOPREFIX = 0x02;
    private const uint TTF_SUBCLASS = 0x0010;
    private const uint TTF_TRANSPARENT = 0x0100;
    private const uint TTM_ADDTOOLW = 0x0432;
    private const uint TTM_UPDATETIPTEXTW = 0x0439;
    private const uint TTM_NEWTOOLRECTW = 0x0434;
    private const uint TTM_SETMAXTIPWIDTH = 0x0418;
    private const uint TTM_SETDELAYTIME = 0x0403;
    private const uint TTDT_INITIAL = 3;    // Délai avant apparition
    private const uint TTDT_RESHOW = 1;     // Délai pour réapparition (passage d'une touche à l'autre)

    private void CreateTooltip()
    {
        _hTooltip = Win32.CreateWindowExW(
            Win32.WS_EX_TOPMOST,
            "tooltips_class32",
            "",
            Win32.WS_POPUP | TTS_ALWAYSTIP | TTS_NOPREFIX,
            0, 0, 0, 0,
            _hWnd, IntPtr.Zero, Win32.GetModuleHandleW(null), IntPtr.Zero);

        // Permettre les tooltips multi-lignes
        Win32.SendMessageW(_hTooltip, TTM_SETMAXTIPWIDTH, IntPtr.Zero, (IntPtr)400);

        // Réduire les délais pour un affichage quasi-instantané
        Win32.SendMessageW(_hTooltip, TTM_SETDELAYTIME, (IntPtr)TTDT_INITIAL, (IntPtr)100);  // 100ms avant apparition
        Win32.SendMessageW(_hTooltip, TTM_SETDELAYTIME, (IntPtr)TTDT_RESHOW, (IntPtr)0);     // Immédiat entre touches

        // Ajouter un outil unique couvrant toute la fenêtre (on le repositionne dynamiquement)
        var ti = new TOOLINFOW
        {
            cbSize = (uint)Marshal.SizeOf<TOOLINFOW>(),
            uFlags = TTF_SUBCLASS | TTF_TRANSPARENT,
            hwnd = _hWnd,
            uId = (UIntPtr)1,
            rect = new Win32.RECT { left = 0, top = 0, right = DEFAULT_WIDTH, bottom = DEFAULT_HEIGHT },
            lpszText = ""
        };
        // Audit sécu 2026-05 SEV-A2-01 : try/finally pour éviter memory leak si exception.
        var ptr = Marshal.AllocHGlobal(Marshal.SizeOf<TOOLINFOW>());
        try
        {
            Marshal.StructureToPtr(ti, ptr, false);
            Win32.SendMessageW(_hTooltip, TTM_ADDTOOLW, IntPtr.Zero, ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private void UpdateTooltipRect()
    {
        if (_hTooltip == IntPtr.Zero || _hWnd == IntPtr.Zero) return;
        Win32.GetClientRect(_hWnd, out var clientRect);
        var ti = new TOOLINFOW
        {
            cbSize = (uint)Marshal.SizeOf<TOOLINFOW>(),
            hwnd = _hWnd,
            uId = (UIntPtr)1,
            rect = clientRect
        };
        // Audit sécu 2026-05 SEV-A2-01 : try/finally pour éviter memory leak si exception.
        var ptr = Marshal.AllocHGlobal(Marshal.SizeOf<TOOLINFOW>());
        try
        {
            Marshal.StructureToPtr(ti, ptr, false);
            Win32.SendMessageW(_hTooltip, TTM_NEWTOOLRECTW, IntPtr.Zero, ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private void UpdateTooltipText(string text)
    {
        var ti = new TOOLINFOW
        {
            cbSize = (uint)Marshal.SizeOf<TOOLINFOW>(),
            hwnd = _hWnd,
            uId = (UIntPtr)1,
            lpszText = text
        };
        // Audit sécu 2026-05 SEV-A2-01 : try/finally pour éviter memory leak si exception.
        var ptr = Marshal.AllocHGlobal(Marshal.SizeOf<TOOLINFOW>());
        try
        {
            Marshal.StructureToPtr(ti, ptr, false);
            Win32.SendMessageW(_hTooltip, TTM_UPDATETIPTEXTW, IntPtr.Zero, ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Affichage / masquage
    // ═══════════════════════════════════════════════════════════════
    public void UsePreferredMonitorWindow(IntPtr hWnd)
    {
        if (hWnd != IntPtr.Zero && hWnd != _hWnd)
            _preferredMonitorWindow = hWnd;
    }

    public void Toggle()
    {
        if (_visible) Hide();
        else Show();
    }

    public void Show()
    {
        // Signal Défi du jour (v1.2.0) : compteur GLOBAL d'ouvertures uniquement —
        // jamais les caractères consultés ou insérés (décision 2026-07-22).
        UsageStats.RecordVirtualKeyboardOpened();

        if (!TryRestoreSavedBounds())
            CenterOnActiveMonitor();
        else
            _preferredMonitorWindow = IntPtr.Zero;
        Win32.ShowWindow(_hWnd, 8); // SW_SHOWNA (ne prend pas le focus)
        _visible = true;
        Invalidate();
    }

    private void CenterOnActiveMonitor()
    {
        if (_hWnd == IntPtr.Zero || !Win32.GetWindowRect(_hWnd, out var winRect))
            return;

        int windowW = Math.Max(1, winRect.right - winRect.left);
        int windowH = Math.Max(1, winRect.bottom - winRect.top);
        var workArea = GetActiveMonitorWorkArea(_preferredMonitorWindow, _hWnd);
        _preferredMonitorWindow = IntPtr.Zero;
        int posX = workArea.left + Math.Max(0, (workArea.right - workArea.left - windowW) / 2);
        int posY = workArea.top + Math.Max(0, (workArea.bottom - workArea.top - windowH) / 2);
        Win32.MoveWindow(_hWnd, posX, posY, windowW, windowH, false);
    }

    private bool TryRestoreSavedBounds()
    {
        if (_hWnd == IntPtr.Zero) return false;
        if (!ConfigManager.TryGetWindowBounds(ConfigManager.VirtualKeyboardBoundsKey, out var rect))
            return false;
        if (!IsRectVisibleOnScreen(rect))
            return false;

        Win32.MoveWindow(_hWnd, rect.left, rect.top,
            rect.right - rect.left, rect.bottom - rect.top, false);
        return true;
    }

    private static bool IsRectVisibleOnScreen(Win32.RECT rect)
    {
        int width = rect.right - rect.left;
        int height = rect.bottom - rect.top;
        if (width < 100 || height < 80) return false;

        var center = new Win32.POINT
        {
            x = rect.left + width / 2,
            y = rect.top + height / 2
        };
        var monitor = Win32.MonitorFromPoint(center, 0);
        if (monitor == IntPtr.Zero) return false;

        var info = new Win32.MONITORINFO { cbSize = Marshal.SizeOf<Win32.MONITORINFO>() };
        if (!Win32.GetMonitorInfo(monitor, ref info)) return false;

        var work = info.rcWork;
        return rect.right > work.left + 40 &&
               rect.left < work.right - 40 &&
               rect.bottom > work.top + 40 &&
               rect.top < work.bottom - 40;
    }

    private void SaveWindowBounds()
    {
        if (_hWnd == IntPtr.Zero) return;
        if (Win32.GetWindowRect(_hWnd, out var rect) && IsRectVisibleOnScreen(rect))
            ConfigManager.SetWindowBounds(ConfigManager.VirtualKeyboardBoundsKey, rect);
    }

    // Les libellés dessinés (Entrée, Verr. Maj., Maj, Espace) suivent la langue de
    // l'interface : redessiner dès qu'elle change, et réécrire l'infobulle au prochain survol.
    private void OnAppLanguageChanged(string _)
    {
        _hoveredKeyIndex = -1;
        if (_hWnd != IntPtr.Zero)
            Win32.SetWindowTextW(_hWnd, WindowTitle);
        if (_visible && _hWnd != IntPtr.Zero)
            Invalidate();
    }

    private void OnWindowBoundsCleared(string key)
    {
        if (key != ConfigManager.VirtualKeyboardBoundsKey || _hWnd == IntPtr.Zero)
            return;
        CenterOnActiveMonitor();
        if (_visible)
            Invalidate();
    }

    internal static IntPtr ChooseMonitorReferenceWindow(IntPtr preferredWindow, IntPtr foregroundWindow, IntPtr ignoredWindow)
    {
        if (preferredWindow != IntPtr.Zero && preferredWindow != ignoredWindow)
            return preferredWindow;
        if (foregroundWindow != IntPtr.Zero && foregroundWindow != ignoredWindow)
            return foregroundWindow;
        return IntPtr.Zero;
    }

    private static Win32.RECT GetActiveMonitorWorkArea(IntPtr preferredWindow = default, IntPtr ignoredWindow = default)
    {
        IntPtr foreground = Win32.GetForegroundWindow();
        IntPtr referenceWindow = ChooseMonitorReferenceWindow(preferredWindow, foreground, ignoredWindow);
        IntPtr monitor = referenceWindow != IntPtr.Zero
            ? Win32.MonitorFromWindow(referenceWindow, Win32.MONITOR_DEFAULTTONEAREST)
            : IntPtr.Zero;

        if (monitor == IntPtr.Zero && Win32.GetCursorPos(out var cursorPt))
            monitor = Win32.MonitorFromPoint(cursorPt, Win32.MONITOR_DEFAULTTONEAREST);

        var mi = new Win32.MONITORINFO { cbSize = Marshal.SizeOf<Win32.MONITORINFO>() };
        if (monitor != IntPtr.Zero && Win32.GetMonitorInfo(monitor, ref mi))
            return mi.rcWork;

        return new Win32.RECT { left = 0, top = 0, right = 1024, bottom = 768 };
    }

    public void Hide()
    {
        SaveWindowBounds();
        Win32.ShowWindow(_hWnd, 0); // SW_HIDE
        _visible = false;
    }

    /// <summary>Met à jour l'état et redessine si changement.</summary>
    public void UpdateState(bool shift, bool altGr, bool ctrl, bool alt, bool capsLock, string? deadKey)
    {
        if (_shiftDown == shift && _altGrDown == altGr && _ctrlDown == ctrl &&
            _altDown == alt && _capsLockActive == capsLock && _activeDeadKey == deadKey)
            return;

        _shiftDown = shift;
        _altGrDown = altGr;
        _ctrlDown = ctrl;
        _altDown = alt;
        _capsLockActive = capsLock;
        _activeDeadKey = deadKey;

        if (_visible)
            Invalidate();
    }

    /// <summary>Montre le geste d'un caractère choisi dans la recherche : pastilles 1 et 2 sur
    /// les touches à frapper, portée et phrase dans le bandeau. Il reste affiché jusqu'au choix
    /// suivant ; <c>null</c> l'efface.</summary>
    public void HighlightMethod(MethodData? method)
    {
        bool had = _gesture != null;
        _gesture = null;

        var steps = method == null || !_visible || string.IsNullOrEmpty(method.Key)
            ? Array.Empty<GestureStep>()
            : Gesture.Of(method);
        if (method == null || steps.Count == 0)
        {
            if (had && _visible) Invalidate();
            return;
        }

        // La touche qui arme une touche morte : « Frappez ù (´) » (décision du 07/10).
        int armStep = -1;
        string? deadKey = null;
        if (method.IsDeadKey)
        {
            deadKey = method.DeadKey;
            for (int i = steps.Count - 2; i >= 0 && armStep < 0; i--)
                if (steps[i].KeyCode != Gesture.CapsLockCode) armStep = i;
        }
        else if (string.Equals(method.Type, "deadkey_activation", StringComparison.OrdinalIgnoreCase))
        {
            armStep = steps.Count - 1;
            deadKey = method.DeadKey;
            if (string.IsNullOrEmpty(deadKey) && KeyCodeToScancode.TryGetValue(steps[armStep].KeyCode, out uint sc)
                && _layout.Keys.TryGetValue(sc, out var def))
            {
                string? output = def.GetOutput(steps[armStep].Shift, steps[armStep].AltGr, false);
                if (output != null && output.StartsWith("dk_", StringComparison.Ordinal)) deadKey = output;
            }
        }
        string? sign = string.IsNullOrEmpty(deadKey) ? null : DeadKeySign(deadKey);
        if (armStep < 0 || sign == null || sign == steps[armStep].KeyLabel)
            armStep = -1;

        // Le caractère et son nom : l'entrée de l'index qui porte cette méthode ; une touche
        // morte seule se nomme par son signe et son nom.
        string? character = null, name = null;
        foreach (var entry in CharacterIndex.Shared.Entries)
        {
            if (!entry.Methods.Contains(method)) continue;
            character = entry.Character;
            name = L.IsEnglish
                ? (entry.NameEn.Length > 0 ? entry.NameEn : entry.NameFr)
                : (entry.NameFr.Length > 0 ? entry.NameFr : entry.NameEn);
            break;
        }
        if (character == null && !method.IsDeadKey && !string.IsNullOrEmpty(deadKey))
        {
            character = DeadKeySign(deadKey);
            name = DeadKeyName(deadKey);
        }

        _gesture = new ShownGesture(steps, character, name, armStep, sign);
        Invalidate();
    }

    /// <summary>Le signe d'une touche morte, tel qu'Espace l'écrit seul (´, ^, µ…).</summary>
    private string DeadKeySign(string deadKey)
    {
        if (_layout.DeadKeys.TryGetValue(deadKey, out var dk) && dk.GetIsolated() is { } isolated
            && isolated.Trim().Length > 0)
            return DisplayGlyph.ForStandaloneMark(isolated);
        return TrayApplication.GetDeadKeySymbol(deadKey);
    }

    /// <summary>Le nom d'une touche morte en minuscules initiales (« accent aigu »).</summary>
    private static string DeadKeyName(string deadKey)
    {
        var dict = L.IsEnglish ? L.DeadKeyNamesEn : _deadKeyNamesFr;
        return dict.TryGetValue(deadKey, out var name) ? LowerFirst(name) : deadKey;
    }

    private static string LowerFirst(string text)
        => text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text[1..];

    /// <summary>Un nom Unicode dans la phrase : « LETTRE Z MAJUSCULE AVEC ACCENT AIGU » devient
    /// « lettre Z majuscule avec accent aigu » ; la lettre nommée seule garde sa casse.</summary>
    internal static string InSentence(string name)
    {
        if (name.Any(char.IsLower)) return LowerFirst(name);
        var words = name.Split(' ');
        for (int i = 0; i < words.Length; i++)
            if (words[i].Length > 1) words[i] = words[i].ToLowerInvariant();
        return string.Join(' ', words);
    }

    /// <summary>Ctrl ou Alt tenu sans AltGr : les touches servent de raccourcis.</summary>
    private bool ShortcutHeld => (_ctrlDown || _altDown) && !_altGrDown;

    /// <summary>Notifie qu'une touche a été pressée (pour animation visuelle).</summary>
    public void NotifyKeyPress(uint scancode)
    {
        if (!_visible) return;
        _pressedScancode = scancode;
        Invalidate();
        // Timer pour effacer le highlight après KEYPRESS_DURATION_MS
        Win32.SetTimer(_hWnd, TIMER_KEYPRESS, KEYPRESS_DURATION_MS, IntPtr.Zero);
    }

    private void Invalidate()
    {
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, false);
    }

    // ═══════════════════════════════════════════════════════════════
    // WndProc
    // ═══════════════════════════════════════════════════════════════
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
                return (IntPtr)1; // Pas d'effacement — on peint tout dans WM_PAINT

            case Win32.WM_SIZE:
                UpdateTooltipRect();
                if (_visible)
                    SaveWindowBounds();
                Invalidate();
                return IntPtr.Zero;

            case Win32.WM_EXITSIZEMOVE:
                SaveWindowBounds();
                return IntPtr.Zero;

            case Win32.WM_DPICHANGED:
            {
                // Windows envoie ce message quand la fenetre est deplacee vers un moniteur
                // de DPI different. lParam pointe vers une RECT* contenant la taille suggeree
                // pour s'adapter au nouveau DPI. Sans handler explicite, la fenetre garderait
                // sa taille → rendu flou ou disproportionne sur l'autre moniteur jusqu'au resize
                // manuel. Le dessin suit la taille client (PaintScale) : tout se remet a
                // l'echelle au prochain WM_PAINT.
                if (lParam != IntPtr.Zero)
                {
                    var suggested = Marshal.PtrToStructure<Win32.RECT>(lParam);
                    Win32.MoveWindow(_hWnd, suggested.left, suggested.top,
                        suggested.right - suggested.left, suggested.bottom - suggested.top, true);
                }
                if (_visible)
                    SaveWindowBounds();
                Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                return IntPtr.Zero;
            }

            case Win32.WM_MOUSEMOVE:
                OnMouseMove(lParam);
                return IntPtr.Zero;

            case Win32.WM_MOUSELEAVE:
                _trackingMouse = false;
                if (_hoveredKeyIndex != -1)
                {
                    _hoveredKeyIndex = -1;
                    UpdateTooltipText("");
                }
                return IntPtr.Zero;

            case Win32.WM_SIZING:
                OnSizing(wParam, lParam);
                return (IntPtr)1;

            case Win32.WM_TIMER:
                var timerId = (UIntPtr)(ulong)wParam.ToInt64();
                if (timerId == TIMER_KEYPRESS)
                {
                    Win32.KillTimer(hWnd, TIMER_KEYPRESS);
                    _pressedScancode = 0;
                    Invalidate();
                }
                return IntPtr.Zero;

            case Win32.WM_CLOSE:
                Hide();
                return IntPtr.Zero; // Ne pas détruire, juste masquer

            case Win32.WM_GETMINMAXINFO:
                if (lParam != IntPtr.Zero)
                {
                    // Convertir les tailles client min en tailles fenêtre
                    var minRect = new Win32.RECT { left = 0, top = 0, right = MIN_WIDTH, bottom = MIN_HEIGHT };
                    Win32.AdjustWindowRectEx(ref minRect, Win32.WS_POPUP | Win32.WS_THICKFRAME | Win32.WS_CAPTION | Win32.WS_SYSMENU,
                        false, Win32.WS_EX_TOPMOST | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE);
                    var mmi = Marshal.PtrToStructure<Win32.MINMAXINFO>(lParam);
                    mmi.ptMinTrackSize = new Win32.POINT { x = minRect.right - minRect.left, y = minRect.bottom - minRect.top };
                    Marshal.StructureToPtr(mmi, lParam, false);
                }
                return IntPtr.Zero;
        }
        }
        catch (Exception ex)
        {
            ConfigManager.Log("VirtualKeyboard WndProc", ex);
        }

        return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    /// <summary>Contraint le resize pour garder le ratio d'aspect.</summary>
    private void OnSizing(IntPtr wParam, IntPtr lParam)
    {
        // wParam indique quel bord est déplacé (1=left, 2=right, 3=top, 6=bottom, etc.)
        var rc = Marshal.PtrToStructure<Win32.RECT>(lParam);
        int w = rc.right - rc.left;
        int h = rc.bottom - rc.top;
        int edge = wParam.ToInt32();

        // Calculer la bordure de la fenêtre (différence entre window rect et client rect)
        Win32.GetWindowRect(_hWnd, out var winRect);
        Win32.GetClientRect(_hWnd, out var cliRect);
        int borderW = (winRect.right - winRect.left) - (cliRect.right - cliRect.left);
        int borderH = (winRect.bottom - winRect.top) - (cliRect.bottom - cliRect.top);

        int clientW = w - borderW;
        int clientH = h - borderH;

        // WMSZ : 1=Left, 2=Right, 3=Top, 4=TopLeft, 5=TopRight, 6=Bottom, 7=BottomLeft, 8=BottomRight
        // Coins et bords horizontaux → largeur prioritaire (ajuster hauteur)
        // Bords purement verticaux (3=Top, 6=Bottom) → hauteur prioritaire (ajuster largeur)
        if (edge == 3 || edge == 6)
        {
            // Hauteur change → ajuster largeur
            clientW = (int)(clientH * ASPECT_RATIO);
        }
        else
        {
            // Largeur change → ajuster hauteur
            clientH = (int)(clientW / ASPECT_RATIO);
        }

        int newW = clientW + borderW;
        int newH = clientH + borderH;

        // Appliquer les contraintes selon les bords déplacés
        // Horizontal : bords gauches (1=Left, 4=TopLeft, 7=BottomLeft) ancrent à droite
        if (edge == 1 || edge == 4 || edge == 7)
            rc.left = rc.right - newW;
        else
            rc.right = rc.left + newW;

        // Vertical : bords hauts (3=Top, 4=TopLeft, 5=TopRight) ancrent en bas
        if (edge == 3 || edge == 4 || edge == 5)
            rc.top = rc.bottom - newH;
        else
            rc.bottom = rc.top + newH;

        Marshal.StructureToPtr(rc, lParam, false);
    }

    // ═══════════════════════════════════════════════════════════════
    // Rendu GDI (double buffering)
    // ═══════════════════════════════════════════════════════════════
    private void OnPaint(IntPtr hWnd)
    {
        var ps = new Win32.PAINTSTRUCT();
        var hdcPaint = Win32.BeginPaint(hWnd, out ps);
        Win32.GetClientRect(hWnd, out var clientRect);

        int cw = clientRect.right - clientRect.left;
        int ch = clientRect.bottom - clientRect.top;
        if (cw <= 0 || ch <= 0) { Win32.EndPaint(hWnd, ref ps); return; }

        // Double buffering
        var hdcScreen = Win32.GetDC(IntPtr.Zero);
        var hdc = Win32.CreateCompatibleDC(hdcScreen);
        var hBmp = Win32.CreateCompatibleBitmap(hdcScreen, cw, ch);
        var hBmpOld = Win32.SelectObject(hdc, hBmp);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);

        try
        {
            PaintContent(hdc, ref clientRect, cw, ch);
        }
        catch (Exception ex)
        {
            // Ne pas crasher — log et afficher un fond noir
            ConfigManager.Log("OnPaint", ex);
        }

        // Copier le buffer sur l'écran
        Win32.BitBlt(hdcPaint, 0, 0, cw, ch, hdc, 0, 0, Win32.SRCCOPY);

        // Nettoyage double buffering
        Win32.SelectObject(hdc, hBmpOld);
        Win32.DeleteObject(hBmp);
        Win32.DeleteDC(hdc);

        Win32.EndPaint(hWnd, ref ps);
    }

    /// <summary>Dessine le bandeau d'état et le clavier en couche active dans le DC mémoire.</summary>
    private void PaintContent(IntPtr hdc, ref Win32.RECT clientRect, int cw, int ch)
    {
        float s = PaintScale(cw, ch);
        PaperDraw.Fill(hdc, clientRect, PaperTheme.Paper);
        Win32.SetBkMode(hdc, Win32.TRANSPARENT);

        var layout = ShortcutHeld ? BlankLayout : _layout;
        PaperKeyboard.Draw(hdc, KeyboardPlacementFor(cw, ch), layout, PaperKeyboardMode.ActiveLayer, BuildKeyboardState());

        DrawBanner(hdc, cw, s);
    }

    /// <summary>La disposition sans aucun signe, pour effacer toutes les touches.</summary>
    private Layout BlankLayout
    {
        get
        {
            if (_blankLayout != null) return _blankLayout;
            var blank = new Layout();
            foreach (var (sc, def) in _layout.Keys)
                blank.Keys[sc] = new KeyDefinition { Position = def.Position, Scancode = def.Scancode };
            return _blankLayout = blank;
        }
    }

    /// <summary>L'état que montre le clavier : modificateurs tenus (en bleu), touche morte en
    /// attente, appui en cours, et le geste de la recherche (pastilles 1 et 2).</summary>
    private PaperKeyboardState BuildKeyboardState()
    {
        bool shortcut = ShortcutHeld;
        var state = new PaperKeyboardState
        {
            Shift = _shiftDown,
            AltGr = _altGrDown,
            CapsLock = _capsLockActive,
            Ctrl = _ctrlDown && !_altGrDown, // le Ctrl fantôme d'AltGr n'est pas tenu
            Alt = _altDown && !_altGrDown,
            ActiveDeadKey = shortcut ? null : _activeDeadKey,
            PressedScancode = _pressedScancode,
        };
        if (_gesture is { } g)
            AddGesture(state, g.Steps, namesOnKeys: !shortcut);
        return state;
    }

    /// <summary>Le geste de la recherche sur le clavier : touches cernées, pastilles 1 et 2, et
    /// sur chaque touche à pastille le nom que lui donne la consigne (<see cref="Gesture.Sentence"/>
    /// écrit le même <see cref="GestureStep.KeyLabel"/>). Ctrl ou Alt tenu efface les signes :
    /// les noms aussi.</summary>
    internal static void AddGesture(PaperKeyboardState state, IReadOnlyList<GestureStep> steps, bool namesOnKeys)
    {
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            uint sc = step.KeyCode == Gesture.CapsLockCode ? 0x3Au
                : KeyCodeToScancode.TryGetValue(step.KeyCode, out var found) ? found : 0u;
            if (sc != 0)
            {
                if (steps.Count > 1)
                {
                    if (state.StepBadges.TryAdd(sc, i + 1) && namesOnKeys)
                        state.StepKeyNames[sc] = step.KeyLabel;
                }
                else state.TargetScancodes.Add(sc);
            }
            if (step.Shift) state.TargetContextLabels.Add(ContextShiftLeft);
            if (step.AltGr) state.TargetContextLabels.Add("AltGr");
            if (steps.Count > 1)
            {
                // Maj et AltGr portent aussi le numéro de leur temps.
                if (step.Shift) state.ContextStepBadges.TryAdd(ContextShiftLeft, i + 1);
                if (step.AltGr) state.ContextStepBadges.TryAdd("AltGr", i + 1);
            }
        }
    }

    /// <summary>Le bandeau : à gauche la portée (Maj et AltGr tenus, ou le geste de la
    /// recherche), à droite la phrase qui dit ce que montre le clavier. La maquette le fait
    /// haut de 64 px, 78 avec la portée d'un geste ; la fenêtre garde 78 pour que le clavier ne
    /// saute pas, et l'état seul se pose en bas, contre le clavier, comme sur la maquette.</summary>
    private void DrawBanner(IntPtr hdc, int cw, float s)
    {
        int bottom = Px(MockBannerHeight - MockBannerPadBottom, s);
        int fullTop = Px(MockBannerPadTop, s);
        int stateTop = Px(MockBannerHeight - MockStateBannerHeight + MockBannerPadTop, s);
        int left = Px(MockMargin, s);
        int right = cw - Px(MockMargin, s);

        int staffRight;
        int top;
        if (_gesture is { } g)
        {
            top = fullTop;
            var style = TablatureStyle.Banner(s);
            var columns = TabColumn.FromGesture(g.Steps);
            var widths = TablatureRenderer.MeasureColumns(hdc, columns, style);
            int width = TablatureRenderer.TotalWidth(widths, style);
            int staffTop = top + (bottom - top - style.Height) / 2;
            TablatureRenderer.Draw(hdc, columns, widths, style,
                new Win32.RECT { left = left, top = staffTop, right = left + width, bottom = staffTop + style.Height });
            staffRight = left + width;
        }
        else
        {
            top = stateTop;
            staffRight = DrawHeldStaff(hdc, left, top + (bottom - top - 2 * Px(MockStaffRow, s)) / 2, s);
        }

        // La phrase : 15 px sur 54ch au plus ; si elle ne tient pas, tout le bandeau, puis 13 px.
        int x = staffRight + Px(MockBannerGap, s);
        int maxWidth = Math.Min(Px(MockTextMaxWidth, s), right - x);
        if (maxWidth <= 0) return;
        var runs = BannerSentence();
        foreach (var (size, line) in new[] { (MockTextSize, MockTextLine), (13, 19f) })
        {
            var regular = FontSpec.Of(AppFonts.Text, size, TypeRamp.Regular, s);
            var strong = FontSpec.Of(AppFonts.Text, size, TypeRamp.Semibold, s);
            var rect = new Win32.RECT { left = x, top = fullTop, right = x + maxWidth, bottom = bottom };
            int lineHeight = Px(line, s);
            int textHeight = PaperText.DrawParagraph(hdc, runs, regular, strong, rect, PaperTheme.TextSecondary,
                PaperTheme.Ink, lineHeight, draw: false);
            if (textHeight > bottom - fullTop && size != 13) continue;
            int bandTop = textHeight <= bottom - top ? top : fullTop;
            rect.top = bandTop + Math.Max(0, (bottom - bandTop - textHeight) / 2);
            rect.bottom = rect.top + textHeight;
            PaperText.DrawParagraph(hdc, runs, regular, strong, rect, PaperTheme.TextSecondary, PaperTheme.Ink, lineHeight);
            return;
        }
    }

    /// <summary>La portée d'état : lignes Maj et AltGr, le trait épais quand on tient. Rend
    /// son bord droit.</summary>
    private int DrawHeldStaff(IntPtr hdc, int left, int top, float s)
    {
        var labelFont = FontSpec.Of(AppFonts.Text, 12, TypeRamp.Regular, s);
        int labelWidth = Px(MockStaffLabel, s);
        int lineLeft = left + labelWidth;
        int lineRight = lineLeft + Px(MockStaffLine, s);
        int row = Px(MockStaffRow, s);
        int hair = Math.Max(1, Px(1, s));
        int bar = Math.Max(2, Px(MockStaffBar, s));
        (string Label, bool Held)[] rows = { (L.Gesture_LineShift, _shiftDown), (L.Gesture_LineAltGr, _altGrDown) };
        for (int r = 0; r < rows.Length; r++)
        {
            int rowTop = top + r * row;
            PaperText.DrawLine(hdc, rows[r].Label, labelFont,
                new Win32.RECT { left = left, top = rowTop, right = lineLeft, bottom = rowTop + row }, PaperTheme.TextSecondary);
            int middle = rowTop + row / 2;
            PaperDraw.Fill(hdc, lineLeft, middle - hair / 2, lineRight, middle - hair / 2 + hair, PaperTheme.Border);
            if (rows[r].Held)
                PaperDraw.Fill(hdc, lineLeft, middle - bar / 2, lineRight, middle - bar / 2 + bar, PaperTheme.Action);
        }
        return lineRight;
    }

    /// <summary>Ce que dit le bandeau, du plus précis au plus général : geste de la recherche,
    /// touche morte en attente, raccourci, modificateurs tenus, Verr. Maj., repos.</summary>
    private List<TextRun> BannerSentence()
    {
        var runs = new List<TextRun>();
        if (_gesture is { } g)
        {
            if (!string.IsNullOrEmpty(g.Character))
            {
                string shown = KeyboardRenderer.DisplayInvisible(g.Character) is { } invisible && invisible != g.Character
                    ? invisible
                    : DisplayGlyph.ForStandaloneMark(g.Character);
                runs.Add(new TextRun(shown, true));
                runs.Add(new TextRun(string.IsNullOrEmpty(g.Name) ? ". " : ", " + InSentence(g.Name) + ". "));
            }
            runs.AddRange(GestureSentence(g));
            return runs;
        }

        string shiftName = L.Settings_ShortcutModifier2;
        if (ShortcutHeld)
        {
            runs.Add(new TextRun(_ctrlDown ? L.Keyboard_KeyCap("Ctrl") : L.Keyboard_KeyCap("Alt"), true));
            runs.Add(new TextRun(L.VirtualKeyboardPaper_ShortcutHeld));
            return runs;
        }
        if (_activeDeadKey != null)
        {
            runs.Add(new TextRun(L.VirtualKeyboardPaper_DeadKeyLead));
            runs.Add(new TextRun(DeadKeySign(_activeDeadKey), true));
            string text = L.VirtualKeyboardPaper_DeadKeyWaits(DeadKeyName(_activeDeadKey));
            if (_layout.DeadKeys.TryGetValue(_activeDeadKey, out var dk) && dk.GetIsolated() is { } alone
                && alone.Trim().Length > 0)
                text += L.VirtualKeyboardPaper_DeadKeySpace(DisplayGlyph.ForStandaloneMark(alone));
            runs.Add(new TextRun(text));
            return runs;
        }
        var held = new List<string>(2);
        if (_shiftDown) held.Add(shiftName);
        if (_altGrDown) held.Add("AltGr");
        if (held.Count > 0)
        {
            for (int i = 0; i < held.Count; i++)
            {
                if (i > 0) runs.Add(new TextRun(L.Gesture_And));
                runs.Add(new TextRun(held[i], true));
            }
            runs.Add(new TextRun((held.Count > 1 ? L.VirtualKeyboardPaper_HeldMany : L.VirtualKeyboardPaper_HeldOne)
                + string.Join(L.Gesture_And, held) + "."));
            return runs;
        }
        if (_capsLockActive)
        {
            runs.Add(new TextRun(L.Keyboard_KeyCap("Verr. Maj."), true));
            runs.Add(new TextRun(L.VirtualKeyboardPaper_CapsOn));
            return runs;
        }
        runs.Add(new TextRun(L.VirtualKeyboardPaper_IdleLead));
        runs.Add(new TextRun(shiftName, true));
        runs.Add(new TextRun(L.VirtualKeyboardPaper_Or));
        runs.Add(new TextRun("AltGr", true));
        runs.Add(new TextRun(L.VirtualKeyboardPaper_IdleTail));
        return runs;
    }

    /// <summary>La phrase du geste (socle), avec le signe de la touche morte après la touche
    /// qui l'arme : « Frappez ù (´) ».</summary>
    private static List<TextRun> GestureSentence(ShownGesture g)
    {
        var runs = Gesture.Sentence(g.Steps);
        if (g.ArmStep < 0 || g.DeadKeySign == null) return runs;
        // Gesture.Sentence met en gras, pour chaque temps, les modificateurs tenus puis la touche.
        int ordinal = 0;
        for (int i = 0; i < g.ArmStep; i++)
            ordinal += (g.Steps[i].Shift ? 1 : 0) + (g.Steps[i].AltGr ? 1 : 0) + 1;
        ordinal += (g.Steps[g.ArmStep].Shift ? 1 : 0) + (g.Steps[g.ArmStep].AltGr ? 1 : 0);
        int seen = 0;
        for (int r = 0; r < runs.Count; r++)
        {
            if (!runs[r].Strong) continue;
            if (seen++ != ordinal) continue;
            runs.Insert(r + 1, new TextRun(" (" + g.DeadKeySign + ")"));
            break;
        }
        return runs;
    }

    /// <summary>
    /// Retourne le caractère à afficher pour un scancode selon l'état actuel.
    /// Quand une touche morte est active : retourne le résultat transformé, ou null si pas de correspondance.
    /// </summary>
    private string? GetDisplayChar(uint scancode)
    {
        if (!_layout.Keys.TryGetValue(scancode, out var keyDef))
            return null;

        if ((_ctrlDown && !_altGrDown) || (_altDown && !_altGrDown))
            return null;

        string? output = keyDef.GetOutput(_shiftDown, _altGrDown, _capsLockActive);

        // Si une touche morte est active
        if (_activeDeadKey != null && output != null)
        {
            // Autre touche morte → masquer, sauf la touche morte active :
            // double-pression = GetIsolated() → caractère A, puis Apply(A) → caractère B
            if (output.StartsWith("dk_"))
            {
                if (output == _activeDeadKey && _layout.DeadKeys.TryGetValue(_activeDeadKey, out var selfDk))
                {
                    var isolated = selfDk.GetIsolated();
                    if (isolated != null)
                    {
                        var doubled = selfDk.Apply(isolated);
                        if (doubled != null)
                            return doubled;
                    }
                    return isolated; // fallback au caractère isolé
                }
                return null;
            }

            if (_layout.DeadKeys.TryGetValue(_activeDeadKey, out var dk))
            {
                var transformed = dk.Apply(output);
                if (transformed != null)
                    return transformed;
            }
            return null; // Pas de correspondance → pas de caractère (légende jaune affichée séparément)
        }

        return output;
    }

    // ═══════════════════════════════════════════════════════════════
    // Hit testing + tooltip
    // ═══════════════════════════════════════════════════════════════
    private void OnMouseMove(IntPtr lParam)
    {
        if (!_trackingMouse)
        {
            var tme = new Win32.TRACKMOUSEEVENT
            {
                cbSize = (uint)Marshal.SizeOf<Win32.TRACKMOUSEEVENT>(),
                dwFlags = Win32.TME_LEAVE,
                hwndTrack = _hWnd,
                dwHoverTime = 0
            };
            Win32.TrackMouseEvent(ref tme);
            _trackingMouse = true;
        }

        int mx = (short)(lParam.ToInt64() & 0xFFFF);
        int my = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

        Win32.GetClientRect(_hWnd, out var clientRect);
        int cw = clientRect.right;
        int ch = clientRect.bottom;

        // Mêmes cadres que le dessin (PaperKeyboard suit la géométrie de KeyboardRenderer).
        int hitIndex = -1, index = 0;
        foreach (var hit in KeyboardRenderer.BuildHitTestRects(KeyboardPlacementFor(cw, ch)))
        {
            var r = hit.Rect;
            if (mx >= r.left && mx < r.right && my >= r.top && my < r.bottom)
            {
                hitIndex = index;
                break;
            }
            index++;
        }

        if (hitIndex != _hoveredKeyIndex)
        {
            _hoveredKeyIndex = hitIndex;
            if (hitIndex >= 0 && !_visualKeys[hitIndex].IsContextual)
            {
                string? ch2 = GetDisplayChar(_visualKeys[hitIndex].Scancode);
                if (ch2 != null && !ch2.StartsWith("dk_") && CharacterIndex.Shared.Names.TryGetValue(ch2, out var name))
                    UpdateTooltipText(L.IsEnglish
                        ? (name.En.Length > 0 ? name.En : name.Fr)
                        : (name.Fr.Length > 0 ? name.Fr : name.En));
                else if (ch2 != null && ch2.StartsWith("dk_"))
                {
                    UpdateTooltipText(GetDeadKeyDisplayName(ch2));
                }
                else
                    UpdateTooltipText("");
            }
            else
            {
                UpdateTooltipText("");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Dispose
    // ═══════════════════════════════════════════════════════════════
    public void Dispose()
    {
        ConfigManager.WindowBoundsCleared -= OnWindowBoundsCleared;
        ConfigManager.AppLanguageChanged -= OnAppLanguageChanged;
        if (_hTooltip != IntPtr.Zero)
        {
            Win32.DestroyWindow(_hTooltip);
            _hTooltip = IntPtr.Zero;
        }
        if (_hWnd != IntPtr.Zero)
        {
            Win32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }

        NativeWindow.UnregisterClass(ProductIdentity.WindowClass("VK"));
    }
}
