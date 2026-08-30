"""CH3 passe 2, lot A3 — Paramètres passe à trois onglets.

Décision d'Antoine du 2026-08-30, après mesure : la fenêtre dimensionnée sur son contenu
réclamait 1466 px de haut à 150 %, pour 1440 de zone de travail sur ce poste et ~1032 sur un
portable 1080p. Trois onglets — Raccourcis, Préférences, Apps suspendues — ramènent le plus
haut des trois à ce qu'un 1080p affiche à 150 %.

Répartition arrêtée avec lui : Raccourcis porte les deux raccourcis et « Valeurs par défaut » ;
Préférences porte les quatre cases, Langue et Fenêtres ; Apps suspendues porte la liste, ses
deux boutons et les trois modes.

**Le premier bloc d'un onglet n'a pas de titre** — l'onglet le nomme déjà. Les blocs suivants
gardent le leur : c'est ce qui distingue « Langue » et « Fenêtres » à l'intérieur de Préférences.

Les onglets sont des BUTTON BS_OWNERDRAW, pas un SysTabControl32 : le contrôle système ne
connaît aucun jeton de la charte, et un bouton nous rend gratuitement le focus, la navigation au
clavier, l'espace et l'entrée, et WM_COMMAND. Ils passent par le même TryDrawItem que le reste.

SettingsWindow.cs est en LF pur sans BOM.
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
    """Remplace tout ce qui sépare `start` de `end`, bornes comprises dans le résultat."""
    global data
    s_b, e_b = start.encode("utf-8"), end.encode("utf-8")
    if data.count(s_b) != 1 or data.count(e_b) != 1:
        sys.exit(f"{name} : bornes non uniques ({data.count(s_b)}, {data.count(e_b)})")
    i = data.index(s_b)
    j = data.index(e_b, i)
    if j < i:
        sys.exit(f"{name} : bornes inversées")
    data = data[:i] + new_middle.encode("utf-8") + data[j:]
    print(f"  {name:36s} {j - i} octets remplacés")


# ══════════════════════════════════════════════════════════════════════════
# 1. Identifiants et champ d'onglet actif
# ══════════════════════════════════════════════════════════════════════════

replace(
    "identifiants des onglets",
    """    private const int IDC_RADIO_COMPAT_FORCEOFF = 3117;""",
    """    private const int IDC_RADIO_COMPAT_FORCEOFF = 3117;
    // Barre d'onglets (2026-08-30)
    private const int IDC_TAB_SHORTCUTS = 3119;
    private const int IDC_TAB_PREFERENCES = 3120;
    private const int IDC_TAB_COMPAT = 3121;""",
)

# ══════════════════════════════════════════════════════════════════════════
# 2. LayoutInfo : cinq ancres de panneau remplacées par l'onglet visible
# ══════════════════════════════════════════════════════════════════════════

replace(
    "LayoutInfo",
    """        public Win32.RECT LogoRect;
        public Win32.RECT ShortcutsPanel;
        public int ShortcutsLabelX;""",
    """        public Win32.RECT LogoRect;
        // Onglet visible, ses trois rectangles de barre, et le panneau qui porte son contenu.
        // Les cinq « Panel » d'avant les onglets ont disparu : ils décrivaient cinq sections
        // empilées dans un seul panneau, qui vivent désormais dans trois.
        public int Tab;
        public Win32.RECT[] TabRects;
        public int TabStripBottom;
        public Win32.RECT ContentPanel;
        public int PanelBottom;
        public int ShortcutsLabelX;""",
)

replace(
    "LayoutInfo : ancres de titre",
    """        public Win32.RECT ResetRect;
        public Win32.RECT PreferencesPanel;
        public Win32.RECT AutoStartRect;""",
    """        public Win32.RECT ResetRect;
        public Win32.RECT AutoStartRect;""",
)

replace(
    "LayoutInfo : Langue et Fenêtres",
    """        public Win32.RECT TrainingRect;
        public Win32.RECT LanguagePanel;
        public Win32.RECT LanguageFrRect;
        public Win32.RECT LanguageEnRect;
        public Win32.RECT ManagedLanguageRect;
        public Win32.RECT WindowsPanel;
        public Win32.RECT ResetVirtualKeyboardWindowRect;
        public Win32.RECT ResetLessonsWindowRect;
        public Win32.RECT CompatPanel;
        public Win32.RECT CompatListRect;""",
    """        public Win32.RECT TrainingRect;
        public int LanguageTitleTop;
        public Win32.RECT LanguageFrRect;
        public Win32.RECT LanguageEnRect;
        public Win32.RECT ManagedLanguageRect;
        public int WindowsTitleTop;
        public Win32.RECT ResetVirtualKeyboardWindowRect;
        public Win32.RECT ResetLessonsWindowRect;
        public Win32.RECT CompatListRect;""",
)

replace(
    "handles des onglets",
    """    private IntPtr _hWndCompatList;""",
    """    private readonly IntPtr[] _hWndTabs = new IntPtr[TabCount];
    private int _activeTab = TabShortcuts;
    private IntPtr _hWndCompatList;""",
)

# ══════════════════════════════════════════════════════════════════════════
# 3. La mise en page, réécrite par onglet
# ══════════════════════════════════════════════════════════════════════════

NEW_LAYOUT = '''    /// <summary>Les trois onglets, arrêtés par Antoine le 2026-08-30.</summary>
    private const int TabCount = 3;
    private const int TabShortcuts = 0;
    private const int TabPreferences = 1;
    private const int TabCompat = 2;

    private static string TabLabel(int tab) => tab switch
    {
        TabShortcuts => L.Settings_SectionShortcuts,
        TabCompat => L.Settings_SectionCompat,
        _ => L.Settings_SectionPreferences,
    };

    private LayoutInfo GetLayout(int winW, int winH) => GetLayout(winW, winH, _activeTab);

    /// <summary>
    /// Mise en page d'un onglet. Les trois flux partent du même haut de panneau : seul celui de
    /// l'onglet demandé décide de la hauteur du panneau, les deux autres sont calculés quand
    /// même — <see cref="MeasureRequiredClientSize"/> en a besoin pour que la fenêtre ne change
    /// pas de taille quand on change d'onglet.
    /// </summary>
    private LayoutInfo GetLayout(int winW, int winH, int tab)
    {
        int margin = S(8);
        int contentWidth = winW - margin * 2;
        int headerTop = S(8);
        int logoSize = S(24);
        int panelPadX = S(9);

        IntPtr hdc = Win32.GetDC(_hWnd);
        try
        {
            int titleHeight = MeasureSingleLineHeight(hdc, _hFontTitle);
            int versionHeight = MeasureSingleLineHeight(hdc, _hFontVersion);
            int headerLineHeight = Math.Max(logoSize, Math.Max(titleHeight, versionHeight) + S(4));
            int logoY = headerTop + Math.Max(0, (headerLineHeight - logoSize) / 2);
            int headerTitleY = headerTop + Math.Max(0, (headerLineHeight - titleHeight) / 2);
            int headerBottom = headerTop + headerLineHeight + S(9);

            int panelTitleHeight = MeasureSingleLineHeight(hdc, _hFontPanelTitle);
            int textLineHeight = MeasureSingleLineHeight(hdc, _hFontText);
            // + la marge de focus des deux côtés : l'anneau se dessine à l'extérieur du
            // contrôle, donc TryDrawItem rend le libellé dans un rectangle rentré d'autant.
            int focusMargin = ThemeControls.FocusMargin(_dpi);
            int checkboxHeight = Math.Max(S(18), MeasureSingleLineHeight(hdc, _hFontBold))
                + focusMargin * 2;
            int buttonHeight = ThemeControls.MeasureButtonHeight(hdc, _hFontButton, _dpi)
                + focusMargin * 2;
            int linkHeight = MeasureSingleLineHeight(hdc, _hFontLinkStrong);
            int validationHeight = MeasureSingleLineHeight(hdc, _hFontSmall);

            // ── Barre d'onglets ─────────────────────────────────────────
            // Largeurs inégales, chacune celle de son libellé : un intitulé court n'a aucune
            // raison d'occuper la place du plus long, et la traduction anglaise ne les aligne
            // pas non plus.
            int tabHeight = ThemeControls.MeasureTabHeight(hdc, _hFontBold, _dpi);
            int tabTop = headerBottom + S(6);
            var tabRects = new Win32.RECT[TabCount];
            int tabX = margin;
            for (int i = 0; i < TabCount; i++)
            {
                int tabW = ThemeControls.MeasureTabWidth(hdc, _hFontBold, TabLabel(i), _dpi);
                tabRects[i] = Rect(tabX, tabTop, tabW, tabHeight);
                tabX += tabW;
            }

            int panelTop = tabTop + tabHeight;
            int labelX = margin + panelPadX;
            int innerWidth = contentWidth - panelPadX * 2;
            int cursor = panelTop + S(12);

            // ── Onglet Raccourcis ───────────────────────────────────────
            int labelWidth = Math.Max(
                MeasureSingleLineWidth(hdc, _hFontText, L.Settings_ShortcutLabelKeyboard),
                MeasureSingleLineWidth(hdc, _hFontText, L.Settings_ShortcutLabelSearch)) + S(6);
            int keyOuterW = S(28);
            int keyOuterH = S(24);
            int keyOuterX = margin + contentWidth - panelPadX - keyOuterW;
            int shortcutX = labelX + labelWidth + S(6);
            int shortcutWidth = keyOuterX - shortcutX - S(8);

            int keyboardRowY = cursor;
            int searchRowY = keyboardRowY + Math.Max(S(28), textLineHeight + S(11));

            var keyboardBoxRect = Rect(keyOuterX, keyboardRowY - S(4), keyOuterW, keyOuterH);
            var searchBoxRect = Rect(keyOuterX, searchRowY - S(4), keyOuterW, keyOuterH);
            var keyboardEditRect = Rect(keyOuterX + 1, keyboardRowY - S(3), keyOuterW - 2, keyOuterH - 2);
            var searchEditRect = Rect(keyOuterX + 1, searchRowY - S(3), keyOuterW - 2, keyOuterH - 2);

            int resetY = searchRowY + Math.Max(S(20), textLineHeight + S(7));
            int resetWidth = MeasureSingleLineWidth(hdc, _hFontLinkStrong, L.Settings_LinkResetDefaults);
            var resetRect = Rect(labelX, resetY, resetWidth, Math.Max(S(18), linkHeight));

            bool showValidation = !string.IsNullOrEmpty(_validationMessage);
            int validationTop = showValidation ? resetRect.bottom + S(5) : resetRect.bottom;
            int currentValidationHeight = showValidation ? Math.Max(S(15), validationHeight) : 0;
            var validationRect = Rect(labelX, validationTop, innerWidth, currentValidationHeight);
            int shortcutsBottom = validationRect.bottom;

            // ── Onglet Préférences ──────────────────────────────────────
            int checkboxGap = S(6);
            int managedHeight = Math.Max(S(13), validationHeight);
            int managedIndent = S(18);
            int managedGap = S(2);
            int managedWidth = innerWidth - managedIndent;

            var autoStartRect = Rect(labelX, cursor, innerWidth, checkboxHeight);
            var notificationsRect = Rect(labelX, autoStartRect.bottom + checkboxGap,
                innerWidth, checkboxHeight);
            // Lignes « Géré par votre organisation » : sous la case, décalées de la largeur de
            // la coche pour s'aligner sur son libellé. Hauteur nulle quand rien n'est imposé.
            var managedNotificationsRect = Rect(labelX + managedIndent,
                notificationsRect.bottom + managedGap, managedWidth,
                _managedNotifications ? managedHeight : 0);
            var onboardingRect = Rect(labelX,
                (_managedNotifications ? managedNotificationsRect.bottom : notificationsRect.bottom) + checkboxGap,
                innerWidth, checkboxHeight);
            var managedOnboardingRect = Rect(labelX + managedIndent,
                onboardingRect.bottom + managedGap, managedWidth,
                _managedOnboarding ? managedHeight : 0);
            var trainingRect = Rect(labelX,
                (_managedOnboarding ? managedOnboardingRect.bottom : onboardingRect.bottom) + checkboxGap,
                innerWidth, checkboxHeight);

            int languageTitleTop = trainingRect.bottom + S(18);
            var languageFrRect = Rect(labelX, languageTitleTop + panelTitleHeight + S(9),
                innerWidth, checkboxHeight);
            var languageEnRect = Rect(labelX, languageFrRect.bottom + checkboxGap,
                innerWidth, checkboxHeight);
            var managedLanguageRect = Rect(labelX + managedIndent,
                languageEnRect.bottom + managedGap, managedWidth,
                _managedLanguage ? managedHeight : 0);

            int windowsTitleTop =
                (_managedLanguage ? managedLanguageRect.bottom : languageEnRect.bottom) + S(18);
            var resetVirtualKeyboardWindowRect = Rect(labelX, windowsTitleTop + panelTitleHeight + S(9),
                innerWidth, buttonHeight);
            var resetLessonsWindowRect = Rect(labelX, resetVirtualKeyboardWindowRect.bottom + S(7),
                innerWidth, buttonHeight);
            int preferencesBottom = resetLessonsWindowRect.bottom;

            // ── Onglet Apps suspendues ──────────────────────────────────
            var compatListRect = Rect(labelX, cursor, innerWidth, S(58));
            int compatBtnW = (innerWidth - S(6)) / 2;
            var compatAddRect = Rect(labelX, compatListRect.bottom + S(6), compatBtnW, buttonHeight);
            var compatRemoveRect = Rect(labelX + compatBtnW + S(6), compatListRect.bottom + S(6),
                innerWidth - compatBtnW - S(6), buttonHeight);
            var compatAutoRect = Rect(labelX, compatAddRect.bottom + S(8), innerWidth, checkboxHeight);
            var compatForceOnRect = Rect(labelX, compatAutoRect.bottom + S(4), innerWidth, checkboxHeight);
            var compatForceOffRect = Rect(labelX, compatForceOnRect.bottom + S(4), innerWidth, checkboxHeight);
            int compatBottom = compatForceOffRect.bottom;

            int activeBottom = tab switch
            {
                TabShortcuts => shortcutsBottom,
                TabCompat => compatBottom,
                _ => preferencesBottom,
            };
            int panelBottom = activeBottom + S(12);

            return new LayoutInfo
            {
                Margin = margin,
                HeaderTitleX = margin + logoSize + S(6),
                HeaderTitleY = headerTitleY,
                HeaderDividerY = headerBottom,
                LogoRect = Rect(margin, logoY, logoSize, logoSize),
                Tab = tab,
                TabRects = tabRects,
                TabStripBottom = panelTop,
                ContentPanel = Rect(margin, panelTop, contentWidth, panelBottom - panelTop),
                PanelBottom = panelBottom,
                ShortcutsLabelX = labelX,
                ShortcutsLabelWidth = labelWidth,
                ShortcutsShortcutX = shortcutX,
                ShortcutsShortcutWidth = shortcutWidth,
                KeyboardRowY = keyboardRowY,
                SearchRowY = searchRowY,
                KeyboardBoxRect = keyboardBoxRect,
                SearchBoxRect = searchBoxRect,
                KeyboardEditRect = keyboardEditRect,
                SearchEditRect = searchEditRect,
                ValidationRect = validationRect,
                ResetRect = resetRect,
                AutoStartRect = autoStartRect,
                NotificationsRect = notificationsRect,
                ManagedNotificationsRect = managedNotificationsRect,
                OnboardingRect = onboardingRect,
                ManagedOnboardingRect = managedOnboardingRect,
                TrainingRect = trainingRect,
                LanguageTitleTop = languageTitleTop,
                LanguageFrRect = languageFrRect,
                LanguageEnRect = languageEnRect,
                ManagedLanguageRect = managedLanguageRect,
                WindowsTitleTop = windowsTitleTop,
                ResetVirtualKeyboardWindowRect = resetVirtualKeyboardWindowRect,
                ResetLessonsWindowRect = resetLessonsWindowRect,
                CompatListRect = compatListRect,
                CompatAddRect = compatAddRect,
                CompatRemoveRect = compatRemoveRect,
                CompatAutoRect = compatAutoRect,
                CompatForceOnRect = compatForceOnRect,
                CompatForceOffRect = compatForceOffRect,
            };
        }
        finally
        {
            Win32.ReleaseDC(_hWnd, hdc);
        }
    }

'''

replace_between(
    "GetLayout réécrit",
    "    private LayoutInfo GetLayout(int winW, int winH)\n",
    "    /// <summary>\n    /// Taille de client où rien n'est tronqué",
    NEW_LAYOUT,
)

if data.count(b"\r\n"):
    sys.exit("des CRLF sont apparus — rien n'est écrit")
SETTINGS.write_bytes(data)
print(f"SettingsWindow.cs  {len(data)} octets, {data.count(b'\n')} LF")
