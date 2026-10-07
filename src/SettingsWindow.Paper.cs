using System.Runtime.InteropServices;
using System.Text;
namespace AZERTYGlobal;

/// <summary>
/// Dessin « papier » de la fenêtre Paramètres (refonte 1.3.0, maquette « onglets en tête »).
/// Les contrôles restent natifs : la bande d'onglets est peinte par sa sous-classe, les cases,
/// boutons radio, lien et boutons par NM_CUSTOMDRAW, la liste et le message par WM_DRAWITEM,
/// les deux cases de raccourci par leur sous-classe. En contraste élevé, onglets et boutons
/// reprennent le dessin du système.
/// </summary>
sealed partial class SettingsWindow
{
    // ── Interop propre à cet écran (absente du socle Win32.cs) ─────────────
    private const int NM_CUSTOMDRAW = -12;
    private const uint CDDS_PREPAINT = 0x0001;
    private const int CDRF_DODEFAULT = 0x0000;
    private const int CDRF_SKIPDEFAULT = 0x0004;
    private const uint CDIS_SELECTED = 0x0001;
    private const uint CDIS_DISABLED = 0x0004;
    private const uint CDIS_FOCUS = 0x0010;
    private const uint CDIS_HOT = 0x0040;
    private const uint WM_MEASUREITEM = 0x002C;
    private const uint WM_CTLCOLORLISTBOX = 0x0134;
    private const uint WM_QUERYUISTATE = 0x0129;
    private const int UISF_HIDEFOCUS = 0x1;
    private const uint EM_SETSEL = 0x00B1;
    private const uint TCM_SETPADDING = 0x1300 + 43;
    private const uint ODS_SELECTED = 0x0001;
    private const uint ODS_FOCUS = 0x0010;
    private const uint SPI_GETHIGHCONTRAST = 0x0042;
    private const uint HCF_HIGHCONTRASTON = 0x0001;

    [StructLayout(LayoutKind.Sequential)]
    private struct NMCUSTOMDRAW
    {
        public Win32.NMHDR hdr;
        public uint dwDrawStage;
        public IntPtr hdc;
        public Win32.RECT rc;
        public UIntPtr dwItemSpec;
        public uint uItemState;
        public IntPtr lItemlParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEASUREITEMSTRUCT
    {
        public uint CtlType;
        public uint CtlID;
        public uint itemID;
        public uint itemWidth;
        public uint itemHeight;
        public IntPtr itemData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HIGHCONTRAST
    {
        public uint cbSize;
        public uint dwFlags;
        public IntPtr lpszDefaultScheme;
    }

    [DllImport("user32.dll")]
    private static extern bool HideCaret(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    private static extern bool SystemParametersInfoHighContrast(uint action, uint param, ref HIGHCONTRAST value, uint winIni);

    [DllImport("gdiplus.dll")]
    private static extern int GdipCreateSolidFill(uint argb, out IntPtr brush);

    [DllImport("gdiplus.dll")]
    private static extern int GdipDeleteBrush(IntPtr brush);

    [DllImport("gdiplus.dll")]
    private static extern int GdipCreatePen1(uint argb, float width, int unit, out IntPtr pen);

    [DllImport("gdiplus.dll")]
    private static extern int GdipDeletePen(IntPtr pen);

    [DllImport("gdiplus.dll")]
    private static extern int GdipSetPenLineCap197819(IntPtr pen, int startCap, int endCap, int dashCap);

    [DllImport("gdiplus.dll")]
    private static extern int GdipSetPenLineJoin(IntPtr pen, int lineJoin);

    [DllImport("gdiplus.dll")]
    private static extern int GdipFillEllipse(IntPtr graphics, IntPtr brush, float x, float y, float width, float height);

    [DllImport("gdiplus.dll")]
    private static extern int GdipDrawEllipse(IntPtr graphics, IntPtr pen, float x, float y, float width, float height);

    [DllImport("gdiplus.dll")]
    private static extern int GdipDrawLine(IntPtr graphics, IntPtr pen, float x1, float y1, float x2, float y2);

    [DllImport("gdiplus.dll")]
    private static extern int GdipSetPixelOffsetMode(IntPtr graphics, int mode);

    private const int UnitPixel = 2;
    private const int LineCapRound = 2;
    private const int LineJoinRound = 2;

    /// <summary>
    /// <see cref="PaperDraw.RoundBox"/> sur un rectangle à bords exclusifs, comme tous ceux de
    /// cette fenêtre. Le socle trace jusqu'à <c>right</c> et <c>bottom</c> inclus : sans ce
    /// retrait, le bord droit et le bas tombaient un pixel hors du cadre (hors du contrôle).
    /// </summary>
    private static void RoundBox(IntPtr hdc, Win32.RECT r, uint fill, uint border, int radius, int borderWidth = 1,
        int penStyle = Win32.PS_SOLID)
    {
        r.right -= 1;
        r.bottom -= 1;
        PaperDraw.RoundBox(hdc, r, fill, border, radius, borderWidth, penStyle);
    }

    [DllImport("gdiplus.dll")]
    private static extern int GdipCreatePath(int fillMode, out IntPtr path);

    [DllImport("gdiplus.dll")]
    private static extern int GdipDeletePath(IntPtr path);

    [DllImport("gdiplus.dll")]
    private static extern int GdipAddPathArc(IntPtr path, float x, float y, float width, float height, float startAngle, float sweepAngle);

    [DllImport("gdiplus.dll")]
    private static extern int GdipClosePathFigure(IntPtr path);

    [DllImport("gdiplus.dll")]
    private static extern int GdipFillPath(IntPtr graphics, IntPtr brush, IntPtr path);

    [DllImport("gdiplus.dll")]
    private static extern int GdipDrawPath(IntPtr graphics, IntPtr pen, IntPtr path);

    /// <summary>Carré arrondi lissé (indicateur de case) : fond puis trait de
    /// <paramref name="stroke"/> px, posé à l'intérieur du cadre.</summary>
    private static void SmoothRoundBox(IntPtr gfx, float x, float y, float size, float radius, uint fill, uint border, float stroke)
    {
        if (gfx == IntPtr.Zero || GdipCreatePath(0, out IntPtr path) != 0) return;
        try
        {
            float half = stroke / 2;
            float left = x + half, top = y + half, side = size - stroke, d = radius * 2;
            GdipAddPathArc(path, left, top, d, d, 180, 90);
            GdipAddPathArc(path, left + side - d, top, d, d, 270, 90);
            GdipAddPathArc(path, left + side - d, top + side - d, d, d, 0, 90);
            GdipAddPathArc(path, left, top + side - d, d, d, 90, 90);
            GdipClosePathFigure(path);
            if (GdipCreateSolidFill(Argb(fill), out IntPtr brush) == 0)
            {
                GdipFillPath(gfx, brush, path);
                GdipDeleteBrush(brush);
            }
            if (GdipCreatePen1(Argb(border), stroke, UnitPixel, out IntPtr pen) == 0)
            {
                GdipDrawPath(gfx, pen, path);
                GdipDeletePen(pen);
            }
        }
        finally { GdipDeletePath(path); }
    }

    /// <summary>COLORREF (0x00BBGGRR) → ARGB opaque de GDI+.</summary>
    private static uint Argb(uint colorRef)
        => 0xFF000000u | ((colorRef & 0xFF) << 16) | (colorRef & 0xFF00) | ((colorRef >> 16) & 0xFF);

    /// <summary>Contraste élevé : la fenêtre rend le dessin au système (onglets, boutons).</summary>
    private static bool HighContrastOn()
    {
        var hc = new HIGHCONTRAST { cbSize = (uint)Marshal.SizeOf<HIGHCONTRAST>() };
        return SystemParametersInfoHighContrast(SPI_GETHIGHCONTRAST, hc.cbSize, ref hc, 0)
            && (hc.dwFlags & HCF_HIGHCONTRASTON) != 0;
    }

    private static string WindowText(IntPtr hWnd)
    {
        var text = new StringBuilder(256);
        Win32.GetWindowTextW(hWnd, text, text.Capacity);
        return text.ToString();
    }

    /// <summary>Le contour de focus se montre quand le clavier sert (état d'interface de
    /// Windows), comme pour les contrôles du système.</summary>
    private static bool FocusCuesShown(IntPtr hWnd)
        => ((int)Win32.SendMessageW(hWnd, WM_QUERYUISTATE, IntPtr.Zero, IntPtr.Zero) & UISF_HIDEFOCUS) == 0;

    // ── Préparation ───────────────────────────────────────────────────────

    private IntPtr[] _paperButtons = Array.Empty<IntPtr>();
    private IntPtr[] _pushButtons = Array.Empty<IntPtr>();

    /// <summary>Les boutons que dessine la fenêtre : cases, boutons radio, lien et boutons.</summary>
    private void PreparePaperButtons()
    {
        _pushButtons = new[]
        {
            _hWndCompatAdd, _hWndCompatRemove, _hWndResetVirtualKeyboardWindow, _hWndResetLessonsWindow,
        };
        _paperButtons = new[]
        {
            _hWndLinkReset,
            _hWndChkAutoStart, _hWndChkNotifications, _hWndChkOnboarding, _hWndChkTraining,
            _hWndRadioLangFr, _hWndRadioLangEn,
            _hWndRadioCompatAuto, _hWndRadioCompatForceOn, _hWndRadioCompatForceOff,
        }.Concat(_pushButtons).ToArray();
    }

    private bool IsPaperButton(IntPtr hWnd) => hWnd != IntPtr.Zero && Array.IndexOf(_paperButtons, hWnd) >= 0;

    private bool IsRadio(IntPtr hWnd)
        => hWnd == _hWndRadioLangFr || hWnd == _hWndRadioLangEn
           || hWnd == _hWndRadioCompatAuto || hWnd == _hWndRadioCompatForceOn || hWnd == _hWndRadioCompatForceOff;

    /// <summary>Marges des onglets : 11 px de chaque côté du libellé, soit les 22 px qui
    /// séparent deux libellés dans la maquette.</summary>
    private void ApplyTabStripPadding()
    {
        if (_hWndTabStrip == IntPtr.Zero) return;
        int padding = (S(11) & 0xFFFF) | ((S(6) & 0xFFFF) << 16);
        Win32.SendMessageW(_hWndTabStrip, TCM_SETPADDING, IntPtr.Zero, (IntPtr)padding);
    }

    private static string TabLabel(int index) => index switch
    {
        0 => L.Settings_TabGeneral,
        1 => L.Settings_TabApplications,
        _ => L.Settings_TabLanguageMaintenance,
    };

    private Win32.RECT TabItemRect(int index)
    {
        var r = new Win32.RECT();
        if (_hWndTabStrip == IntPtr.Zero) return r;
        IntPtr p = Marshal.AllocHGlobal(Marshal.SizeOf<Win32.RECT>());
        try
        {
            Marshal.StructureToPtr(r, p, false);
            if (Win32.SendMessageW(_hWndTabStrip, Win32.TCM_GETITEMRECT, (IntPtr)index, p) != IntPtr.Zero)
                r = Marshal.PtrToStructure<Win32.RECT>(p);
        }
        finally { Marshal.FreeHGlobal(p); }
        return r;
    }

    /// <summary>Position de la bande telle que le premier libellé commence à la marge de 20 px.</summary>
    private int MeasureTabStripLeft(IntPtr hdc)
    {
        var item = TabItemRect(0);
        int text = PaperText.Width(hdc, TabLabel(0), FontTab);
        int inset = item.right > item.left ? item.left + (item.right - item.left - text) / 2 : S(2) + S(11);
        return Math.Max(0, S(20) - inset);
    }

    // ── Fenêtre ───────────────────────────────────────────────────────────

    private void OnPaint(IntPtr hWnd)
    {
        using var paint = new PaintBuffer(hWnd);
        PaintClient(paint.Hdc, paint.Client);
    }

    /// <summary>Toute la zone cliente dans <paramref name="hdc"/>, à partir de l'origine qu'il
    /// porte déjà : (0, 0) pour WM_PAINT, celle d'un contrôle enfant pour WM_PRINTCLIENT.</summary>
    private void PaintClient(IntPtr hdc, Win32.RECT clientRect)
    {
        int cw = clientRect.right;
        LayoutInfo layout = GetLayout(S(BASE_WIN_W));
        Win32.GetViewportOrgEx(hdc, out var origin);

        PaperDraw.Fill(hdc, clientRect, CLR_BG);
        Win32.SetBkMode(hdc, 1);
        // Le contenu se dessine en coordonnées de contenu, décalées du défilement.
        Win32.SetViewportOrgEx(hdc, origin.x, origin.y - _scrollY, IntPtr.Zero);

        // Filet sous la bande d'onglets, sur toute la largeur (la bande le redessine sur sa part).
        int band = layout.TabStripRect.bottom;
        PaperDraw.Fill(hdc, 0, band - S(1), cw, band, CLR_BORDER);

        if (_activeTab == SettingsTab.General)
        {
            DrawTitle(hdc, layout.ShortcutsTitleRect, L.Settings_SectionShortcuts);
            DrawShortcutRow(hdc, layout.KeyboardRowRect, layout.KeyboardBoxRect, L.Settings_ShortcutLabelKeyboard);
            DrawShortcutRow(hdc, layout.SearchRowRect, layout.SearchBoxRect, L.Settings_ShortcutLabelSearch);
            DrawHint(hdc, layout.KeyboardHintRect, L.SettingsClarity_HintKeyboardShortcut);
            DrawHint(hdc, layout.SearchHintRect, L.SettingsClarity_HintSearchShortcut);
            DrawTitle(hdc, layout.PreferencesTitleRect, L.Settings_SectionPreferences);
            DrawHint(hdc, layout.AutoStartHintRect, L.SettingsClarity_HintAutoStart);
            DrawHint(hdc, layout.NotificationsHintRect, L.SettingsClarity_HintNotifications);
            DrawHint(hdc, layout.OnboardingHintRect, L.SettingsClarity_HintOnboarding);
        }
        else if (_activeTab == SettingsTab.Applications)
        {
            DrawTitle(hdc, layout.CompatTitleRect, L.Settings_SectionCompat);
            PaperDraw.Frame(hdc, layout.CompatListFrame, CLR_BORDER);
            string? process = SelectedCompatProcess();
            string modeLabel = process != null ? L.SettingsPaper_CompatModeFor(process) : L.SettingsPaper_CompatModeNone;
            PaperText.DrawLine(hdc, modeLabel, FontSmall, layout.CompatModeLabelRect, CLR_MUTED);
            DrawHint(hdc, layout.CompatAutoHintRect, L.SettingsPaper_CompatHintAuto);
            DrawHint(hdc, layout.CompatForceOnHintRect, L.SettingsPaper_CompatHintForceOn);
            DrawHint(hdc, layout.CompatForceOffHintRect, L.SettingsPaper_CompatHintForceOff);
        }
        else
        {
            DrawTitle(hdc, layout.LanguageTitleRect, L.Settings_SectionLanguage);
            DrawTitle(hdc, layout.WindowsTitleRect, L.Settings_SectionWindows);
            DrawHint(hdc, layout.ResetVirtualKeyboardHintRect, L.SettingsClarity_HintResetWindow);
            DrawHint(hdc, layout.ResetLessonsHintRect, L.SettingsClarity_HintResetWindow);
        }

        Win32.SetViewportOrgEx(hdc, origin.x, origin.y, IntPtr.Zero);
    }

    private void DrawHint(IntPtr hdc, Win32.RECT rect, string text)
        => PaperText.DrawParagraph(hdc, new[] { new TextRun(text) }, FontSmall, FontSmall, rect,
            CLR_MUTED, CLR_MUTED, MessageLineHeight);

    private void DrawTitle(IntPtr hdc, Win32.RECT rect, string title)
        => PaperText.DrawLine(hdc, title, FontSectionTitle, rect, CLR_TEXT);

    /// <summary>Une ligne de raccourci : le libellé, puis le geste écrit comme dans la
    /// Tablature — les modificateurs tenus en étiquettes d'encre, la touche dans sa case
    /// (peinte par la case elle-même, <see cref="PaintShortcutBox"/>).</summary>
    private void DrawShortcutRow(IntPtr hdc, Win32.RECT row, Win32.RECT box, string label)
    {
        int right = box.left - S(9);
        string[] held = { L.Settings_ShortcutModifier2, "Ctrl" };
        int tagH = S(22);
        int tagTop = row.top + (row.bottom - row.top - tagH) / 2;
        foreach (string tag in held)
        {
            int width = PaperText.Width(hdc, tag, FontHeld) + 2 * S(7);
            var r = Rect(right - width, tagTop, width, tagH);
            RoundBox(hdc, r, CLR_TEXT, CLR_TEXT, S(2));
            PaperText.DrawLine(hdc, tag, FontHeld, r, CLR_BG, TextAlign.Center);
            right = r.left - S(5);
        }
        var labelRect = row;
        labelRect.right = right - S(8);
        int save = Win32.SaveDC(hdc);
        Win32.IntersectClipRect(hdc, labelRect.left, labelRect.top, labelRect.right, labelRect.bottom);
        PaperText.DrawLine(hdc, label, FontBody, labelRect, CLR_TEXT);
        Win32.RestoreDC(hdc, save);
    }

    // ── Message du dernier geste (STATIC SS_OWNERDRAW) ────────────────────

    private void DrawValidation(Win32.DRAWITEMSTRUCT dis)
    {
        var rc = dis.rcItem;
        int w = rc.right - rc.left, h = rc.bottom - rc.top;
        if (w <= 0 || h <= 0) return;
        var (dc, bitmap, previous) = PaintBuffer.CreateSurface(w, h);
        IntPtr hdc = dc != IntPtr.Zero ? dc : dis.hDC;
        var r = dc != IntPtr.Zero ? Rect(0, 0, w, h) : rc;
        try
        {
            PaperDraw.Fill(hdc, r, CLR_BG);
            string text = _validationMessage;
            var place = CurrentMessagePlace();
            if (place == MessagePlace.Footer)
                DrawFooterMessage(hdc, r, text);
            else if (place == MessagePlace.Shortcuts)
            {
                uint color = _validationRefused ? CLR_INVALID : CLR_ACTION;
                PaperDraw.Fill(hdc, r, CLR_BG);
                DrawRightParagraph(hdc, r, text, color);
            }
            else if (place == MessagePlace.Compat)
            {
                PaperText.DrawParagraph(hdc, new[] { new TextRun(text) }, FontSmall, FontSmall, r,
                    ValidationTextColor(true), ValidationTextColor(true), MessageLineHeight);
            }
        }
        finally
        {
            if (dc != IntPtr.Zero)
            {
                Win32.BitBlt(dis.hDC, rc.left, rc.top, w, h, dc, 0, 0, Win32.SRCCOPY);
                PaintBuffer.ReleaseSurface(dc, bitmap, previous);
            }
        }
    }

    /// <summary>L'invite et un refus de raccourci, alignés à droite sous les cases ; coupés
    /// sur plusieurs lignes s'ils ne tiennent pas (anglais à 150 %).</summary>
    private void DrawRightParagraph(IntPtr hdc, Win32.RECT r, string text, uint color)
    {
        int width = PaperText.Width(hdc, text, FontSmall);
        if (width <= r.right - r.left)
        {
            var line = r;
            line.bottom = line.top + MessageLineHeight;
            PaperText.DrawLine(hdc, text, FontSmall, line, color, TextAlign.Right);
            return;
        }
        PaperText.DrawParagraph(hdc, new[] { new TextRun(text) }, FontSmall, FontSmall, r, color, color, MessageLineHeight);
    }

    /// <summary>Le pied de confirmation : filet, texte secondaire, puis le ✓ en bleu.</summary>
    private void DrawFooterMessage(IntPtr hdc, Win32.RECT r, string text)
    {
        PaperDraw.Fill(hdc, r.left, r.top, r.right, r.top + S(1), CLR_BORDER);
        string body = text.TrimEnd();
        bool tick = body.EndsWith('✓');
        if (tick) body = body[..^1].TrimEnd();
        var line = Rect(r.left + S(20), r.top + S(1), r.right - r.left - S(40), r.bottom - r.top - S(1));
        int save = Win32.SaveDC(hdc);
        Win32.IntersectClipRect(hdc, line.left, line.top, line.right, line.bottom);
        int width = PaperText.DrawLine(hdc, body, FontSmall, line, ValidationTextColor(false));
        if (tick)
        {
            var tickRect = line;
            tickRect.left += width + S(6);
            PaperText.DrawLine(hdc, "✓", FontSmallStrong, tickRect, CLR_ACTION);
        }
        Win32.RestoreDC(hdc, save);
    }

    // ── Liste des applications (LBS_OWNERDRAWFIXED) ───────────────────────

    private void DrawCompatItem(Win32.DRAWITEMSTRUCT dis)
    {
        var rc = dis.rcItem;
        int w = rc.right - rc.left, h = rc.bottom - rc.top;
        if (w <= 0 || h <= 0) return;
        var (dc, bitmap, previous) = PaintBuffer.CreateSurface(w, h);
        IntPtr hdc = dc != IntPtr.Zero ? dc : dis.hDC;
        var r = dc != IntPtr.Zero ? Rect(0, 0, w, h) : rc;
        try
        {
            int index = unchecked((int)dis.itemID);
            bool valid = index >= 0 && index < _compatProcesses.Count;
            bool selected = valid && (dis.itemState & ODS_SELECTED) != 0;
            PaperDraw.Fill(hdc, r, selected ? PaperTheme.ActionTint : CLR_SURFACE);
            if (!valid) return;

            int count = (int)Win32.SendMessageW(_hWndCompatList, LB_GETCOUNT, IntPtr.Zero, IntPtr.Zero);
            if (index < count - 1 || count < 3)
                PaperDraw.Fill(hdc, r.left, r.bottom - S(1), r.right, r.bottom, CLR_BORDER);
            if (selected)
                PaperDraw.Fill(hdc, r.left, r.top, r.left + S(3), r.bottom, CLR_ACTION);

            string mode = CompatListLabel(_compatModes[index]);
            var modeRect = Rect(r.left, r.top, w - S(12), h - S(1));
            int modeWidth = PaperText.DrawLine(hdc, mode, FontSmall, modeRect, CLR_MUTED, TextAlign.Right);

            var nameRect = Rect(r.left + S(12), r.top, Math.Max(0, w - S(12) - modeWidth - S(24)), h - S(1));
            int save = Win32.SaveDC(hdc);
            Win32.IntersectClipRect(hdc, nameRect.left, nameRect.top, nameRect.right, nameRect.bottom);
            PaperText.DrawLine(hdc, _compatProcesses[index], selected ? FontListNameChosen : FontListName, nameRect, CLR_TEXT);
            Win32.RestoreDC(hdc, save);

            if ((dis.itemState & ODS_FOCUS) != 0 && FocusCuesShown(_hWndCompatList))
                PaperDraw.Frame(hdc, Inflate(r, -S(1)), CLR_ACTION);
        }
        finally
        {
            if (dc != IntPtr.Zero)
            {
                Win32.BitBlt(dis.hDC, rc.left, rc.top, w, h, dc, 0, 0, Win32.SRCCOPY);
                PaintBuffer.ReleaseSurface(dc, bitmap, previous);
            }
        }
    }

    // ── Cases de raccourci (sous-classe de l'EDIT) ────────────────────────

    /// <summary>
    /// La case où l'on frappe la touche du raccourci. Au repos : blanche, cernée d'encre.
    /// En capture : teintée, cernée de tirets bleus, entourée du contour de focus. Refusée :
    /// cernée de rouge sur 2 px — l'épaisseur dit l'état sans dépendre de la couleur.
    /// </summary>
    private void PaintShortcutBox(IntPtr hWnd)
    {
        using var paint = new PaintBuffer(hWnd);
        var hdc = paint.Hdc;
        var client = paint.Client;
        PaperDraw.Fill(hdc, client, CLR_BG);
        var box = Inflate(client, -S(4));
        bool keyboard = hWnd == _hWndEditKeyboard;
        bool valid = keyboard ? _keyboardValid : _searchValid;
        bool capturing = _focusedShortcut == hWnd;
        string key = ConfigManager.GetShortcutDisplayName(ShownVk(keyboard));
        int radius = S(3);

        uint textColor = CLR_TEXT;
        if (!valid)
        {
            RoundBox(hdc, box, CLR_SURFACE, CLR_INVALID, radius, Math.Max(2, S(2)));
            textColor = CLR_INVALID;
        }
        else if (capturing)
        {
            RoundBox(hdc, client, CLR_BG, CLR_ACTION, radius + S(3), Math.Max(2, S(2)));
            PaperDraw.Fill(hdc, Inflate(client, -Math.Max(2, S(2))), CLR_BG);
            RoundBox(hdc, box, PaperTheme.ActionTintOnPaper, CLR_ACTION, radius, 1, Win32.PS_DASH);
            RoundBox(hdc, Inflate(box, -1), PaperTheme.ActionTintOnPaper, CLR_ACTION, Math.Max(1, radius - 1), 1, Win32.PS_DASH);
            textColor = CLR_ACTION;
        }
        else
        {
            RoundBox(hdc, box, CLR_SURFACE, CLR_TEXT, radius);
        }
        PaperText.DrawLine(hdc, key, FontKey, box, textColor, TextAlign.Center);
    }

    // ── Bande d'onglets (sous-classe du SysTabControl32) ──────────────────

    private IntPtr TabStripSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        try
        {
            switch (msg)
            {
                case Win32.WM_PAINT:
                    if (HighContrastOn()) break;
                    PaintTabStrip(hWnd);
                    return IntPtr.Zero;

                case Win32.WM_ERASEBKGND:
                    if (HighContrastOn()) break;
                    return (IntPtr)1;

                // Les onglets du contrôle n'occupent pas toute la hauteur de la bande : un clic
                // sous un libellé est ramené à la hauteur des onglets, la colonne décide.
                case Win32.WM_LBUTTONDOWN:
                {
                    var item = TabItemRect(0);
                    if (item.bottom > item.top)
                    {
                        int x = (short)(lParam.ToInt64() & 0xFFFF);
                        int y = (item.top + item.bottom) / 2;
                        lParam = (IntPtr)((x & 0xFFFF) | ((y & 0xFFFF) << 16));
                    }
                    break;
                }

                case Win32.WM_SETFOCUS:
                case Win32.WM_KILLFOCUS:
                    Win32.InvalidateRect(hWnd, IntPtr.Zero, false);
                    break;
            }
        }
        catch (Exception ex)
        {
            ConfigManager.Log("Settings TabStrip", ex);
        }
        return Win32.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    /// <summary>
    /// La bande : libellés en 15 px, l'onglet ouvert en encre et demi-gras sur le trait
    /// épais bleu (décision d'Antoine du 07/10), les autres en texte secondaire ; filet
    /// d'un pixel en bas ; la version à droite.
    /// </summary>
    private void PaintTabStrip(IntPtr hWnd)
    {
        using var paint = new PaintBuffer(hWnd);
        var hdc = paint.Hdc;
        var client = paint.Client;
        PaperDraw.Fill(hdc, client, CLR_BG);
        PaperDraw.Fill(hdc, client.left, client.bottom - S(1), client.right, client.bottom, CLR_BORDER);

        int selected = (int)Win32.SendMessageW(hWnd, Win32.TCM_GETCURSEL, IntPtr.Zero, IntPtr.Zero);
        int line = Metrics.Tab;
        int textBottom = client.bottom - S(11);
        int lastRight = 0;
        for (int i = 0; i < 3; i++)
        {
            var item = TabItemRect(i);
            if (item.right <= item.left) continue;
            bool open = i == selected;
            var font = open ? FontTab : FontTabIdle;
            string label = TabLabel(i);
            int width = PaperText.Width(hdc, label, font);
            int left = item.left + (item.right - item.left - width) / 2;
            var textRect = Rect(left, textBottom - line, width, line);
            PaperText.DrawLine(hdc, label, font, textRect, open ? CLR_TEXT : CLR_MUTED);
            if (open)
            {
                PaperDraw.Fill(hdc, left, client.bottom - S(4), left + width, client.bottom, CLR_ACTION);
                if (Win32.GetFocus() == hWnd && FocusCuesShown(hWnd))
                    PaperDraw.Frame(hdc, Inflate(textRect, S(3)), CLR_ACTION);
            }
            lastRight = Math.Max(lastRight, left + width);
        }

        // Version, alignée sur la marge droite de la fenêtre.
        string version = L.SettingsPaper_Version(Program.Version);
        int versionWidth = PaperText.Width(hdc, version, FontVersion);
        var versionRect = Rect(client.right - S(20) - versionWidth, client.bottom - S(12) - Metrics.Version,
            versionWidth, Metrics.Version);
        if (versionRect.left > lastRight + S(12))
            PaperText.DrawLine(hdc, version, FontVersion, versionRect, CLR_MUTED);
    }

    // ── Cases, boutons radio, lien et boutons (NM_CUSTOMDRAW) ─────────────

    private int OnButtonCustomDraw(IntPtr lParam)
    {
        var cd = Marshal.PtrToStructure<NMCUSTOMDRAW>(lParam);
        if (cd.dwDrawStage != CDDS_PREPAINT || HighContrastOn())
            return CDRF_DODEFAULT;

        IntPtr hWnd = cd.hdr.hwndFrom;
        var rc = cd.rc;
        int w = rc.right - rc.left, h = rc.bottom - rc.top;
        if (w <= 0 || h <= 0) return CDRF_SKIPDEFAULT;

        var (dc, bitmap, previous) = PaintBuffer.CreateSurface(w, h);
        IntPtr hdc = dc != IntPtr.Zero ? dc : cd.hdc;
        var r = dc != IntPtr.Zero ? Rect(0, 0, w, h) : rc;
        try
        {
            PaperDraw.Fill(hdc, r, CLR_BG);
            bool enabled = Win32.IsWindowEnabled(hWnd) && (cd.uItemState & CDIS_DISABLED) == 0;
            bool focused = (cd.uItemState & CDIS_FOCUS) != 0 && FocusCuesShown(hWnd);
            string text = WindowText(hWnd);

            if (hWnd == _hWndLinkReset)
                DrawLinkButton(hdc, r, text, focused);
            else if (Array.IndexOf(_pushButtons, hWnd) >= 0)
                DrawPushButton(hdc, r, text, enabled, focused,
                    (cd.uItemState & (CDIS_HOT | CDIS_SELECTED)) != 0);
            else
            {
                bool isChecked = Win32.SendMessageW(hWnd, Win32.BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == (IntPtr)Win32.BST_CHECKED;
                DrawChoice(hdc, r, text, IsRadio(hWnd), isChecked, enabled, focused);
            }
        }
        finally
        {
            if (dc != IntPtr.Zero)
            {
                Win32.BitBlt(cd.hdc, rc.left, rc.top, w, h, dc, 0, 0, Win32.SRCCOPY);
                PaintBuffer.ReleaseSurface(dc, bitmap, previous);
            }
        }
        return CDRF_SKIPDEFAULT;
    }

    /// <summary>« Réinitialiser les raccourcis », écrit comme un lien : bleu, souligné.</summary>
    private void DrawLinkButton(IntPtr hdc, Win32.RECT r, string text, bool focused)
    {
        var textRect = Inflate(r, -S(3));
        int width = PaperText.DrawLine(hdc, text, FontButton, textRect, CLR_ACTION);
        var (ascent, descent) = PaperText.Metrics(hdc, FontButton);
        int baseline = textRect.top + (textRect.bottom - textRect.top - (ascent + descent)) / 2 + ascent;
        int under = baseline + S(2);
        PaperDraw.Fill(hdc, textRect.left, under, textRect.left + width, under + Math.Max(1, S(1)), CLR_ACTION);
        if (focused)
            PaperDraw.Frame(hdc, r, CLR_ACTION);
    }

    /// <summary>Bouton secondaire : blanc, cerné d'encre, 32 px de haut.</summary>
    private void DrawPushButton(IntPtr hdc, Win32.RECT r, string text, bool enabled, bool focused, bool hot)
    {
        uint fill = enabled && hot ? PaperTheme.FunctionKey : CLR_SURFACE;
        uint border = !enabled ? CLR_BORDER : focused ? CLR_ACTION : CLR_TEXT;
        int borderWidth = enabled && focused ? Math.Max(2, S(2)) : Math.Max(1, S(1));
        RoundBox(hdc, r, fill, border, S(3), borderWidth);
        PaperText.DrawLine(hdc, text, FontButton, r, enabled ? CLR_TEXT : CLR_PALE, TextAlign.Center);
    }

    /// <summary>Case à cocher ou bouton radio : indicateur de 18 px, libellé à 10 px.</summary>
    private void DrawChoice(IntPtr hdc, Win32.RECT r, string text, bool radio, bool isChecked, bool enabled, bool focused)
    {
        int size = S(18);
        int left = r.left + S(4);
        int top = r.top + (r.bottom - r.top - size) / 2;
        var indicator = Rect(left, top, size, size);
        uint ink = enabled ? CLR_TEXT : CLR_PALE;
        uint action = enabled ? CLR_ACTION : CLR_PALE;
        float stroke = 1.5f * _dpiScale;

        IntPtr gfx = IntPtr.Zero;
        Win32.GdipCreateFromHDC(hdc, out gfx);
        try
        {
            if (gfx != IntPtr.Zero)
            {
                Win32.GdipSetSmoothingMode(gfx, 4);
                GdipSetPixelOffsetMode(gfx, 4);
            }

            if (focused)
            {
                float ring = 2f * _dpiScale;
                if (radio)
                    StrokeEllipse(gfx, indicator.left - 3f * _dpiScale + ring / 2, indicator.top - 3f * _dpiScale + ring / 2,
                        size + 6f * _dpiScale - ring, CLR_ACTION, ring);
                else
                    SmoothRoundBox(gfx, indicator.left - 3f * _dpiScale, indicator.top - 3f * _dpiScale,
                        size + 6f * _dpiScale, 5f * _dpiScale, CLR_BG, CLR_ACTION, ring);
            }

            if (radio)
            {
                float x = indicator.left + stroke / 2, y = indicator.top + stroke / 2, d = size - stroke;
                FillEllipse(gfx, indicator.left, indicator.top, size, CLR_SURFACE);
                StrokeEllipse(gfx, x, y, d, isChecked ? action : ink, stroke);
                if (isChecked)
                {
                    float inset = stroke + 3f * _dpiScale;
                    FillEllipse(gfx, indicator.left + inset, indicator.top + inset, size - 2 * inset, action);
                }
            }
            else
            {
                float radius = 3f * _dpiScale;
                if (isChecked)
                {
                    SmoothRoundBox(gfx, indicator.left, indicator.top, size, radius, action, action, stroke);
                    DrawTick(gfx, indicator);
                }
                else
                {
                    SmoothRoundBox(gfx, indicator.left, indicator.top, size, radius, CLR_SURFACE, ink, stroke);
                }
            }
        }
        finally
        {
            if (gfx != IntPtr.Zero) Win32.GdipDeleteGraphics(gfx);
        }

        var textRect = Rect(indicator.right + S(10), r.top, Math.Max(0, r.right - indicator.right - S(10)), r.bottom - r.top);
        int save = Win32.SaveDC(hdc);
        Win32.IntersectClipRect(hdc, textRect.left, textRect.top, textRect.right, textRect.bottom);
        PaperText.DrawLine(hdc, text, FontBody, textRect, enabled ? CLR_TEXT : CLR_PALE);
        Win32.RestoreDC(hdc, save);
    }

    private static void FillEllipse(IntPtr gfx, float x, float y, float d, uint color)
    {
        if (gfx == IntPtr.Zero) return;
        if (GdipCreateSolidFill(Argb(color), out IntPtr brush) != 0) return;
        GdipFillEllipse(gfx, brush, x, y, d, d);
        GdipDeleteBrush(brush);
    }

    private static void StrokeEllipse(IntPtr gfx, float x, float y, float d, uint color, float width)
    {
        if (gfx == IntPtr.Zero) return;
        if (GdipCreatePen1(Argb(color), width, UnitPixel, out IntPtr pen) != 0) return;
        GdipDrawEllipse(gfx, pen, x, y, d, d);
        GdipDeletePen(pen);
    }

    /// <summary>La coche blanche d'une case cochée, trait arrondi de 2 px.</summary>
    private void DrawTick(IntPtr gfx, Win32.RECT box)
    {
        if (gfx == IntPtr.Zero) return;
        if (GdipCreatePen1(Argb(PaperTheme.OnAction), 2f * _dpiScale, UnitPixel, out IntPtr pen) != 0) return;
        GdipSetPenLineCap197819(pen, LineCapRound, LineCapRound, 0);
        GdipSetPenLineJoin(pen, LineJoinRound);
        float u = _dpiScale;
        float x = box.left, y = box.top;
        GdipDrawLine(gfx, pen, x + 4.5f * u, y + 9.5f * u, x + 7.5f * u, y + 12.5f * u);
        GdipDrawLine(gfx, pen, x + 7.5f * u, y + 12.5f * u, x + 13.5f * u, y + 5.5f * u);
        GdipDeletePen(pen);
    }
}
