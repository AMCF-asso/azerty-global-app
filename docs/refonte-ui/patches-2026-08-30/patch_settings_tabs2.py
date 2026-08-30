"""CH3 passe 2, lot A3 (2/2) — les onglets se créent, se peignent, se montrent et se mesurent.

Suite de patch_settings_tabs.py, qui n'avait réécrit que la mise en page. SettingsWindow.cs est
en LF pur sans BOM.
"""

import pathlib
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = pathlib.Path(__file__).resolve().parents[3]
SETTINGS = ROOT / "src" / "SettingsWindow.cs"

data = SETTINGS.read_bytes()
if data[:3] == b"\xef\xbb\xbf" or data.count(b"\r\n"):
    sys.exit("SettingsWindow.cs n'est plus en LF pur sans BOM")


def replace(name, old, new, expected=1):
    global data
    old_b = old.encode("utf-8")
    found = data.count(old_b)
    if found != expected:
        sys.exit(f"{name} : ancre attendue {expected} fois, trouvée {found}\n---\n{old}\n---")
    data = data.replace(old_b, new.encode("utf-8"))
    print(f"  {name:36s} {expected}×")


def replace_between(name, start, end, new_middle):
    global data
    s_b, e_b = start.encode("utf-8"), end.encode("utf-8")
    if data.count(s_b) != 1 or data.count(e_b) != 1:
        sys.exit(f"{name} : bornes non uniques ({data.count(s_b)}, {data.count(e_b)})")
    i, j = data.index(s_b), data.index(e_b)
    if j < i:
        sys.exit(f"{name} : bornes inversées")
    data = data[:i] + new_middle.encode("utf-8") + data[j:]
    print(f"  {name:36s} {j - i} octets remplacés")


# ══════════════════════════════════════════════════════════════════════════
# 1. Création des trois boutons d'onglet
# ══════════════════════════════════════════════════════════════════════════

replace(
    "création des onglets",
    """    private void CreateControls()
    {
        var hInstance = Win32.GetModuleHandleW(null);
""",
    """    private void CreateControls()
    {
        var hInstance = Win32.GetModuleHandleW(null);

        // La barre d'onglets, créée avant tout le reste : Windows construit l'ordre de
        // tabulation dans l'ordre de création des enfants, et les onglets doivent venir en
        // premier — c'est par eux qu'on choisit ce qu'on va régler.
        //
        // Des BUTTON BS_OWNERDRAW plutôt qu'un SysTabControl32 : le contrôle système ne connaît
        // aucun jeton de la charte, et un bouton rend gratuitement le focus, l'espace, l'entrée
        // et WM_COMMAND.
        int[] tabIds = { IDC_TAB_SHORTCUTS, IDC_TAB_PREFERENCES, IDC_TAB_COMPAT };
        for (int i = 0; i < TabCount; i++)
        {
            _hWndTabs[i] = Win32.CreateWindowExW(0, "BUTTON", TabLabel(i),
                Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_OWNERDRAW | Win32.WS_TABSTOP,
                0, 0, 0, 0,
                _hWnd, (IntPtr)tabIds[i], hInstance, IntPtr.Zero);
        }
""",
)

# ══════════════════════════════════════════════════════════════════════════
# 2. Placement et visibilité par onglet
# ══════════════════════════════════════════════════════════════════════════

replace(
    "placement des onglets et visibilité",
    """        Win32.MoveWindow(_hWndRadioCompatForceOff,
            layout.CompatForceOffRect.left, layout.CompatForceOffRect.top,
            layout.CompatForceOffRect.right - layout.CompatForceOffRect.left,
            layout.CompatForceOffRect.bottom - layout.CompatForceOffRect.top, true);
    }""",
    """        Win32.MoveWindow(_hWndRadioCompatForceOff,
            layout.CompatForceOffRect.left, layout.CompatForceOffRect.top,
            layout.CompatForceOffRect.right - layout.CompatForceOffRect.left,
            layout.CompatForceOffRect.bottom - layout.CompatForceOffRect.top, true);

        for (int i = 0; i < TabCount; i++)
        {
            var r = layout.TabRects[i];
            Win32.MoveWindow(_hWndTabs[i], r.left, r.top, r.right - r.left, r.bottom - r.top, true);
        }

        ApplyTabVisibility(layout.Tab);
    }

    /// <summary>
    /// Montre les contrôles de l'onglet visible et cache les autres. Cacher suffit : Windows
    /// retire un contrôle invisible du parcours de tabulation, ce qui évite d'avoir à toucher
    /// aux styles WS_TABSTOP à chaque changement d'onglet.
    /// </summary>
    private void ApplyTabVisibility(int tab)
    {
        Show(_hWndEditKeyboard, tab == TabShortcuts);
        Show(_hWndEditSearch, tab == TabShortcuts);
        Show(_hWndLinkReset, tab == TabShortcuts);
        Show(_hWndValidation, tab == TabShortcuts);

        Show(_hWndChkAutoStart, tab == TabPreferences);
        Show(_hWndChkNotifications, tab == TabPreferences);
        Show(_hWndChkOnboarding, tab == TabPreferences);
        Show(_hWndChkTraining, tab == TabPreferences);
        Show(_hWndRadioLangFr, tab == TabPreferences);
        Show(_hWndRadioLangEn, tab == TabPreferences);
        Show(_hWndResetVirtualKeyboardWindow, tab == TabPreferences);
        Show(_hWndResetLessonsWindow, tab == TabPreferences);
        Show(_hWndManagedNotifications, tab == TabPreferences && _managedNotifications);
        Show(_hWndManagedOnboarding, tab == TabPreferences && _managedOnboarding);
        Show(_hWndManagedLanguage, tab == TabPreferences && _managedLanguage);

        Show(_hWndCompatList, tab == TabCompat);
        Show(_hWndCompatAdd, tab == TabCompat);
        Show(_hWndCompatRemove, tab == TabCompat);
        Show(_hWndRadioCompatAuto, tab == TabCompat);
        Show(_hWndRadioCompatForceOn, tab == TabCompat);
        Show(_hWndRadioCompatForceOff, tab == TabCompat);
    }

    private static void Show(IntPtr control, bool visible)
    {
        if (control != IntPtr.Zero)
            Win32.ShowWindow(control, visible ? 5 : 0); // SW_SHOW / SW_HIDE
    }

    /// <summary>
    /// Change d'onglet. La fenêtre ne change pas de taille : MeasureRequiredClientSize prend
    /// déjà le plus grand des trois, pour qu'un changement d'onglet ne fasse pas sauter la
    /// fenêtre sous le curseur.
    /// </summary>
    private void SetActiveTab(int tab)
    {
        if (tab < 0 || tab >= TabCount || tab == _activeTab)
            return;

        _activeTab = tab;
        RepositionControls();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }""",
)

# ══════════════════════════════════════════════════════════════════════════
# 3. Activation au clic
# ══════════════════════════════════════════════════════════════════════════

replace(
    "WM_COMMAND : onglets",
    """                    case IDC_CHK_TRAINING:
                        if (code == 0)""",
    """                    case IDC_TAB_SHORTCUTS:
                        if (code == 0) SetActiveTab(TabShortcuts);
                        break;
                    case IDC_TAB_PREFERENCES:
                        if (code == 0) SetActiveTab(TabPreferences);
                        break;
                    case IDC_TAB_COMPAT:
                        if (code == 0) SetActiveTab(TabCompat);
                        break;
                    case IDC_CHK_TRAINING:
                        if (code == 0)""",
)

# ══════════════════════════════════════════════════════════════════════════
# 4. Peinture : un panneau, trois contenus
# ══════════════════════════════════════════════════════════════════════════

replace(
    "OnPaint : dispatch par onglet",
    """        DrawHeader(hdc, gfx, layout, cw);
        GdiHelpers.DrawPanel(hdc, layout.ShortcutsPanel, CLR_PANEL_BG, CLR_PANEL_BORDER, 0, 0);
        PaintShortcutPanel(hdc, layout);
        PaintPreferencesPanel(hdc, layout);
        PaintLanguagePanel(hdc, layout);
        PaintWindowsPanel(hdc, layout);
        PaintCompatPanel(hdc, layout);""",
    """        DrawHeader(hdc, gfx, layout, cw);
        // Le fond de la barre d'onglets appartient au papier : les trois boutons se peignent
        // eux-mêmes et WS_CLIPCHILDREN les découpe de ce tampon.
        GdiHelpers.DrawPanel(hdc, layout.ContentPanel, CLR_PANEL_BG, CLR_PANEL_BORDER, 0, 0);
        switch (layout.Tab)
        {
            case TabShortcuts:
                PaintShortcutTab(hdc, layout);
                break;
            case TabCompat:
                PaintCompatTab(hdc, layout);
                break;
            default:
                PaintPreferencesTab(hdc, layout);
                break;
        }""",
)

NEW_PAINTERS = '''    /// <summary>
    /// Titre d'un bloc à l'intérieur d'un onglet, précédé de son filet. Le **premier** bloc d'un
    /// onglet n'en reçoit pas : l'onglet le nomme déjà, et répéter « Raccourcis » sous l'onglet
    /// « Raccourcis » ne dit rien de plus. Seuls Langue et Fenêtres en portent un, parce qu'ils
    /// partagent l'onglet Préférences.
    /// </summary>
    private void PaintSectionTitle(IntPtr hdc, LayoutInfo layout, int top, string title)
    {
        int left = layout.ContentPanel.left + S(12);
        int right = layout.ContentPanel.right - S(12);
        GdiHelpers.FillSolidRect(hdc, Rect(left, top - S(8), right - left, 1), CLR_SEPARATOR);

        Win32.SelectObject(hdc, _hFontPanelTitle);
        Win32.SetTextColor(hdc, CLR_LINK);
        var titleRect = new Win32.RECT
        {
            left = left,
            top = top,
            right = right,
            bottom = top + MeasureSingleLineHeight(hdc, _hFontPanelTitle)
        };
        Win32.DrawTextW(hdc, title, -1, ref titleRect,
            Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
    }

    private void PaintShortcutTab(IntPtr hdc, LayoutInfo layout)
    {
        DrawShortcutRow(hdc, layout.ShortcutsLabelX, layout.ShortcutsLabelWidth, layout.KeyboardRowY,
            L.Settings_ShortcutLabelKeyboard, GetShortcutPrefixRuns(), layout.ShortcutsShortcutX, layout.ShortcutsShortcutWidth);
        DrawKeyBox(hdc, layout.KeyboardBoxRect, _keyboardValid, _focusedShortcut == _hWndEditKeyboard);

        DrawShortcutRow(hdc, layout.ShortcutsLabelX, layout.ShortcutsLabelWidth, layout.SearchRowY,
            L.Settings_ShortcutLabelSearch, GetShortcutPrefixRuns(), layout.ShortcutsShortcutX, layout.ShortcutsShortcutWidth);
        DrawKeyBox(hdc, layout.SearchBoxRect, _searchValid, _focusedShortcut == _hWndEditSearch);
    }

    private void PaintPreferencesTab(IntPtr hdc, LayoutInfo layout)
    {
        PaintSectionTitle(hdc, layout, layout.LanguageTitleTop, L.Settings_SectionLanguage);
        PaintSectionTitle(hdc, layout, layout.WindowsTitleTop, L.Settings_SectionWindows);
    }

    private void PaintCompatTab(IntPtr hdc, LayoutInfo layout)
    {
        // Le cadre de la liste, que WS_BORDER dessinait aux couleurs du système. Même primitive
        // que les champs de raccourci : une liste est un champ, du point de vue de la charte.
        ThemeControls.DrawFieldFrame(hdc, layout.CompatListRect, ControlState.None,
            Theme.Current, _dpi);
'''

replace_between(
    "cinq peintres → trois",
    "    private void PaintShortcutPanel(IntPtr hdc, LayoutInfo layout)\n",
    "        ThemeControls.DrawFieldFrame(hdc, layout.CompatListRect, ControlState.None,\n            Theme.Current, _dpi);\n",
    NEW_PAINTERS,
)

# ══════════════════════════════════════════════════════════════════════════
# 5. La mesure prend le plus grand des trois onglets
# ══════════════════════════════════════════════════════════════════════════

replace(
    "mesure : barre d'onglets",
    """            int headerWidth = margin + logoSize + S(6)
                + MeasureSingleLineWidth(hdc, _hFontTitle, ProductIdentity.DisplayName) + S(8)
                + MeasureSingleLineWidth(hdc, _hFontVersion, version) + S(24) + S(6) + margin;""",
    """            int headerWidth = margin + logoSize + S(6)
                + MeasureSingleLineWidth(hdc, _hFontTitle, ProductIdentity.DisplayName) + S(8)
                + MeasureSingleLineWidth(hdc, _hFontVersion, version) + S(24) + S(6) + margin;

            // La barre d'onglets ne se replie pas : les trois intitulés bout à bout sont un
            // plancher de largeur au même titre que l'en-tête.
            int tabStripWidth = margin * 2;
            for (int i = 0; i < TabCount; i++)
                tabStripWidth += ThemeControls.MeasureTabWidth(hdc, _hFontBold, TabLabel(i), _dpi);
            headerWidth = Math.Max(headerWidth, tabStripWidth);""",
)

replace(
    "mesure : hauteur des trois onglets",
    """        // GetLayout prend son propre DC : le nôtre est rendu avant de l'appeler.
        LayoutInfo layout = GetLayout(width, 0);
        int height = layout.CompatForceOffRect.bottom + S(12) + margin + reserve;
        return (width, Math.Max(S(BASE_WIN_H), height));""",
    """        // GetLayout prend son propre DC : le nôtre est rendu avant de l'appeler. La hauteur
        // est celle du plus haut des trois onglets, pas celle de l'onglet visible : changer
        // d'onglet ne doit pas faire sauter la fenêtre sous le curseur.
        int height = 0;
        for (int tab = 0; tab < TabCount; tab++)
            height = Math.Max(height, GetLayout(width, 0, tab).PanelBottom);

        return (width, height + margin + reserve);""",
)

# ══════════════════════════════════════════════════════════════════════════
# 6. Police des onglets
# ══════════════════════════════════════════════════════════════════════════

replace(
    "police des onglets",
    """        Win32.SendMessageW(_hWndRadioCompatForceOff, Win32.WM_SETFONT, _hFontBold, (IntPtr)1);""",
    """        Win32.SendMessageW(_hWndRadioCompatForceOff, Win32.WM_SETFONT, _hFontBold, (IntPtr)1);
        for (int i = 0; i < TabCount; i++)
            Win32.SendMessageW(_hWndTabs[i], Win32.WM_SETFONT, _hFontBold, (IntPtr)1);""",
)

if data.count(b"\r\n"):
    sys.exit("des CRLF sont apparus — rien n'est écrit")
SETTINGS.write_bytes(data)
print(f"SettingsWindow.cs  {len(data)} octets, {data.count(b'\n')} LF")
