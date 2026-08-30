"""CH3 passe 2, lot A1 — Paramètres se dimensionne sur son contenu et devient redimensionnable.

Le dépôt interdit Edit sur un .cs : chaque remplacement est ancré en octets, son nombre
d'occurrences est vérifié avant qu'un seul octet ne soit écrit, et les fins de ligne sont
recomptées après coup. SettingsWindow.cs et ThemeControls.cs sont en LF pur sans BOM (mesuré
le 2026-08-30) — le script refuse d'écrire si ce n'est plus vrai.

Ce que le lot corrige, mesuré sur captures/ch3/ aux trois échelles et dans les deux thèmes :
  - quatre libellés de case tronqués (« Lancer au démarrage de Windows », « Fenêtre de
    bienvenue au démarrage », « Rappels d'entraînement "Défi du jour" », et le troisième mode
    de compatibilité) ;
  - le lien « Valeurs par défaut » rendu « Valeurs par » — largeur figée à S(118) ;
  - les cinq titres de section amputés de leur jambage, et le titre d'en-tête écrasé — rect de
    hauteur S(20) pour des polices de 18 et 24 px ;
  - la dernière ligne de la fenêtre hors du client.
"""

import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[3]
SETTINGS = ROOT / "src" / "SettingsWindow.cs"
CONTROLS = ROOT / "src" / "ThemeControls.cs"


def load(path):
    data = path.read_bytes()
    if data[:3] == b"\xef\xbb\xbf":
        sys.exit(f"{path.name} porte un BOM — ce script ne l'a pas prévu")
    crlf = data.count(b"\r\n")
    if crlf:
        sys.exit(f"{path.name} porte {crlf} CRLF — ce script écrit du LF pur")
    return data


def replace(data, name, old, new, expected=1):
    old_b = old.encode("utf-8")
    found = data.count(old_b)
    if found != expected:
        sys.exit(f"{name} : ancre attendue {expected} fois, trouvée {found} fois\n---\n{old}\n---")
    return data.replace(old_b, new.encode("utf-8"))


# ══════════════════════════════════════════════════════════════════════════
# ThemeControls.cs — la mesure qui manquait aux cases
# ══════════════════════════════════════════════════════════════════════════

controls = load(CONTROLS)

controls = replace(
    controls,
    "ThemeControls.MeasureBoxRowWidth",
    """    /// <summary>Place que l'anneau de focus réclame de chaque côté d'un contrôle.""",
    """    /// <summary>Largeur qu'une case ou une radio doit avoir pour porter son libellé sans le
    /// tronquer : l'anneau de focus des deux côtés, la boîte, son écart au libellé, et le texte.
    /// Compagnon de <see cref="MeasureButtonWidth"/>. Sans elle, une mise en page ne peut pas se
    /// dimensionner sur ses cases et retombe sur une constante, qui ment dès que la police ou la
    /// langue change — c'est exactement ce qui a tronqué quatre libellés de Paramètres.</summary>
    internal static int MeasureBoxRowWidth(IntPtr hdc, IntPtr font, string text, int dpi) =>
        2 * FocusMargin(dpi) + Scale(BaseBoxSize, dpi) + Scale(BaseBoxLabelGap, dpi)
            + GdiHelpers.MeasureSingleLineWidth(hdc, font, text);

    /// <summary>Place que l'anneau de focus réclame de chaque côté d'un contrôle.""",
)

# ══════════════════════════════════════════════════════════════════════════
# SettingsWindow.cs
# ══════════════════════════════════════════════════════════════════════════

s = load(SETTINGS)

# ── 1. Les deux constantes deviennent un plancher, et disent pourquoi ──────
s = replace(
    s,
    "BASE_WIN commentaire",
    """    // 240 → 300 le 2026-07-16 (smoke test) : « Virtual keyboard » était tronqué en EN
    // et la fenêtre était disproportionnée (deux fois plus haute que large).
    // 470 → 680 le 2026-07-30 : section « Apps suspendues » + opt-in Défi du jour (v1.2.0).
    private const int BASE_WIN_W = 300;
    private const int BASE_WIN_H = 680;""",
    """    // Plancher, et non plus taille de la fenêtre : MeasureRequiredClientSize mesure ce que le
    // contenu réclame à ce DPI et dans cette langue, la fenêtre prend le plus grand des deux.
    //
    // Ces deux nombres sont l'histoire du défaut que la mesure supprime. 240 → 300 le
    // 2026-07-16 (smoke test) : « Virtual keyboard » était tronqué en EN. 470 → 680 le
    // 2026-07-30 : section « Apps suspendues » + opt-in Défi du jour (v1.2.0). Puis la charte
    // a remplacé les polices d'origine par de plus grandes, et 300 × 680 a coupé quatre
    // libellés de case, le lien « Valeurs par défaut » et la dernière ligne de la fenêtre,
    // aux trois échelles — mesuré le 2026-08-30. Une troisième constante aurait tenu jusqu'au
    // libellé suivant.
    private const int BASE_WIN_W = 300;
    private const int BASE_WIN_H = 680;

    /// <summary>Style de la fenêtre, un seul endroit pour les trois qui en avaient besoin.
    /// <c>WS_THICKFRAME</c> depuis le 2026-08-30, sur demande d'Antoine : la taille calculée
    /// n'est qu'un plancher, l'utilisateur peut agrandir. Ni minimiser ni maximiser — une
    /// fenêtre de réglages n'a rien à faire dans la barre des tâches ni en plein écran.</summary>
    private const uint WindowStyle =
        Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU | Win32.WS_THICKFRAME;""",
)

# ── 2. Création et redimensionnement lisent la mesure ────────────────────
s = replace(
    s,
    "winW/winH depuis la mesure",
    """        int winW = S(BASE_WIN_W);
        int winH = S(BASE_WIN_H);
        uint dwStyle = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU;""",
    """        var (winW, winH) = MeasureRequiredClientSize();
        uint dwStyle = WindowStyle;""",
    expected=2,
)

# ── 3. Le repositionnement suit la fenêtre réelle, pas les constantes ─────
s = replace(
    s,
    "RepositionControls sur le client",
    """    private void RepositionControls()
    {
        int winW = S(BASE_WIN_W);
        int winH = S(BASE_WIN_H);
        LayoutInfo layout = GetLayout(winW, winH);""",
    """    private void RepositionControls()
    {
        // La taille réelle fait foi depuis que la fenêtre est redimensionnable. Tant que ces
        // deux lignes lisaient les constantes, l'agrandir laissait les contrôles à leur ancienne
        // place pendant que la peinture, elle, suivait déjà le client (OnPaint lit
        // GetClientRect depuis toujours).
        Win32.GetClientRect(_hWnd, out var clientRect);
        LayoutInfo layout = GetLayout(clientRect.right, clientRect.bottom);""",
)

# ── 4. La largeur d'étiquette des raccourcis se mesure ────────────────────
s = replace(
    s,
    "labelWidth mesuré",
    """            int labelWidth = S(120); // assez pour « Virtual keyboard » sans troncature""",
    """            // Mesurée, pas constante : les 120 px dataient des polices d'avant la charte, et
            // le commentaire qui les justifiait — « assez pour "Virtual keyboard" » — avait
            // cessé d'être vrai sans que rien ne le dise.
            int labelWidth = Math.Max(
                MeasureSingleLineWidth(hdc, _hFontText, L.Settings_ShortcutLabelKeyboard),
                MeasureSingleLineWidth(hdc, _hFontText, L.Settings_ShortcutLabelSearch)) + S(6);""",
)

# ── 5. La largeur du lien « Valeurs par défaut » se mesure ────────────────
s = replace(
    s,
    "largeur du lien de réinitialisation",
    """            var resetRect = Rect(labelX, resetY, S(118), Math.Max(S(18), linkHeight));""",
    """            // S(118) rendait « Valeurs par défaut » en « Valeurs par » : la police soulignée de
            // la charte est plus large que celle d'origine, et un lien tronqué ne se voit pas
            // comme un libellé tronqué — il se lit comme un autre lien.
            int resetWidth = MeasureSingleLineWidth(hdc, _hFontLinkStrong, L.Settings_LinkResetDefaults);
            var resetRect = Rect(labelX, resetY, resetWidth, Math.Max(S(18), linkHeight));""",
)

# ── 6. La mesure elle-même ─────────────────────────────────────────────────
s = replace(
    s,
    "MeasureRequiredClientSize",
    """    private static Win32.RECT Rect(int left, int top, int width, int height)""",
    """    /// <summary>
    /// Taille de client où rien n'est tronqué, à ce DPI et dans cette langue. Elle sert de
    /// taille d'ouverture et de plancher au redimensionnement (<c>WM_GETMINMAXINFO</c>).
    ///
    /// La largeur est celle du plus large bloc qui ne peut pas se replier : l'en-tête, la ligne
    /// de raccourci, le lien, les neuf cases et radios, les quatre boutons. La hauteur vient du
    /// bas du dernier contrôle que <see cref="GetLayout"/> place, majorée de la ligne de
    /// validation quand elle n'est pas affichée : sans cette réserve, saisir un raccourci
    /// invalide ferait grandir la fenêtre sous le curseur.
    ///
    /// Les cases se mesurent à l'anatomie de la charte (<see cref="ThemeControls.MeasureBoxRowWidth"/>)
    /// et non à celle du contrôle Windows qui les peint encore : c'est la cible du lot suivant,
    /// et sa boîte est la plus large des deux.
    /// </summary>
    private (int Width, int Height) MeasureRequiredClientSize()
    {
        int margin = S(8);
        int panelPadX = S(9);
        int logoSize = S(24);
        int width;
        int reserve;

        IntPtr hdc = Win32.GetDC(_hWnd);
        try
        {
            int Box(string text) =>
                ThemeControls.MeasureBoxRowWidth(hdc, _hFontBold, text, _dpi);
            int Button(string text) =>
                ThemeControls.MeasureButtonWidth(hdc, _hFontButton, text, _dpi);

            // En-tête : logo, titre, numéro de version. Il occupe toute la largeur de client,
            // pas l'intérieur d'un panneau.
            string version = $"v{Program.Version}";
            int headerWidth = margin + logoSize + S(6)
                + MeasureSingleLineWidth(hdc, _hFontTitle, ProductIdentity.DisplayName) + S(8)
                + MeasureSingleLineWidth(hdc, _hFontVersion, version) + S(24) + S(6) + margin;

            // Ligne de raccourci : étiquette, préfixe « Ctrl + Maj + », boîte de touche.
            int labelWidth = Math.Max(
                MeasureSingleLineWidth(hdc, _hFontText, L.Settings_ShortcutLabelKeyboard),
                MeasureSingleLineWidth(hdc, _hFontText, L.Settings_ShortcutLabelSearch)) + S(6);
            int prefixWidth = 0;
            foreach (var (text, _, font) in GetShortcutPrefixRuns())
                prefixWidth += MeasureSingleLineWidth(hdc, font, text);
            int shortcutRow = labelWidth + S(6) + prefixWidth + S(8) + S(28);

            int inner = shortcutRow;
            inner = Math.Max(inner,
                MeasureSingleLineWidth(hdc, _hFontLinkStrong, L.Settings_LinkResetDefaults));
            inner = Math.Max(inner, Box(L.Settings_AutoStart));
            inner = Math.Max(inner, Box(L.Settings_Notifications));
            inner = Math.Max(inner, Box(L.Settings_OnboardingWindow));
            inner = Math.Max(inner, Box(L.Challenge_OptIn));
            inner = Math.Max(inner, Box("Français"));
            inner = Math.Max(inner, Box("English"));
            inner = Math.Max(inner, Box(L.Settings_CompatModeAuto));
            inner = Math.Max(inner, Box(L.Settings_CompatModeForceOn));
            inner = Math.Max(inner, Box(L.Settings_CompatModeForceOff));
            inner = Math.Max(inner, Button(L.Settings_ResetVirtualKeyboard));
            inner = Math.Max(inner, Button(L.Settings_ResetLessonsModule));
            inner = Math.Max(inner,
                Button(L.Settings_CompatAdd) + S(6) + Button(L.Settings_CompatRemove));

            // Les lignes « Géré par votre organisation » sont indentées sous leur case.
            inner = Math.Max(inner, S(18)
                + MeasureSingleLineWidth(hdc, _hFontSmall, L.Settings_ManagedByOrganization));

            width = Math.Max(S(BASE_WIN_W),
                Math.Max(headerWidth, inner + panelPadX * 2 + margin * 2));

            // Réserve de la ligne de validation, que GetLayout n'insère que lorsqu'un message
            // est en cours. La fenêtre ne doit pas changer de taille pour un message.
            reserve = string.IsNullOrEmpty(_validationMessage)
                ? S(5) + Math.Max(S(15), MeasureSingleLineHeight(hdc, _hFontSmall))
                : 0;
        }
        finally
        {
            Win32.ReleaseDC(_hWnd, hdc);
        }

        // GetLayout prend son propre DC : le nôtre est rendu avant de l'appeler.
        LayoutInfo layout = GetLayout(width, 0);
        int height = layout.CompatForceOffRect.bottom + S(12) + margin + reserve;
        return (width, Math.Max(S(BASE_WIN_H), height));
    }

    private static Win32.RECT Rect(int left, int top, int width, int height)""",
)

# ── 7. WM_SIZE et WM_GETMINMAXINFO ─────────────────────────────────────────
s = replace(
    s,
    "WM_SIZE / WM_GETMINMAXINFO",
    """            case Win32.WM_ERASEBKGND:
                return (IntPtr)1;
""",
    """            case Win32.WM_ERASEBKGND:
                return (IntPtr)1;

            case Win32.WM_SIZE:
                // _hWnd est encore nul pendant CreateWindowExW, qui envoie déjà ce message :
                // les contrôles n'existent pas, il n'y a rien à replacer.
                if (_hWnd != IntPtr.Zero)
                {
                    RepositionControls();
                    Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
                }
                return IntPtr.Zero;

            case Win32.WM_GETMINMAXINFO:
            {
                // Recalculé à chaque demande plutôt que mémorisé : la langue et le DPI changent
                // tous les deux en cours de vie de la fenêtre, et un plancher périmé laisserait
                // l'utilisateur réduire la fenêtre sous la taille de son propre contenu.
                var (minW, minH) = MeasureRequiredClientSize();
                var frame = new Win32.RECT { left = 0, top = 0, right = minW, bottom = minH };
                Win32.AdjustWindowRectEx(ref frame, WindowStyle, false, 0);
                var mmi = Marshal.PtrToStructure<Win32.MINMAXINFO>(lParam);
                mmi.ptMinTrackSize.x = frame.right - frame.left;
                mmi.ptMinTrackSize.y = frame.bottom - frame.top;
                Marshal.StructureToPtr(mmi, lParam, false);
                return IntPtr.Zero;
            }
""",
)

# ── 8. Les rects de titre cessent de couper les jambages ──────────────────
s = replace(
    s,
    "titre d'en-tête",
    """            bottom = layout.HeaderTitleY + S(20)""",
    """            // S(20) pour une police de 24 px : le titre était écrasé en haut et en bas.
            bottom = layout.HeaderTitleY + titleHeight""",
)

s = replace(
    s,
    "titres de section",
    """            bottom = titleY + S(20)""",
    """            bottom = titleY + MeasureSingleLineHeight(hdc, _hFontPanelTitle)""",
    expected=5,
)

# ══════════════════════════════════════════════════════════════════════════
# Écriture
# ══════════════════════════════════════════════════════════════════════════

for path, data in ((CONTROLS, controls), (SETTINGS, s)):
    crlf = data.count(b"\r\n")
    if crlf:
        sys.exit(f"{path.name} : {crlf} CRLF après remplacement, rien n'est écrit")
    path.write_bytes(data)
    print(f"{path.name:24s} {len(data):7d} octets, {data.count(chr(10).encode()):5d} LF")
