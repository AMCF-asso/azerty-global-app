"""Ressources et Préférences se séparent : le wizard passe à cinq étapes.

Arbitrage d'Antoine du 2026-08-30, le second de la journée sur cette fenêtre. Le premier avait
coupé les cinq améliorations en deux, ce qui n'a rendu que 38 px — parce que l'étape la plus
haute n'était plus celle-là. Mesuré sur capture : c'était **l'étape des ressources et des
préférences**, dont les deux panneaux remplissaient la fenêtre jusqu'à la barre de navigation.

Les deux panneaux se séparent donc en deux écrans. `GetStep3Layout`, qui les empilait dans un
seul calcul, devient deux fonctions : chacune pose son panneau à partir du haut de **son** écran,
et plus l'une sous l'autre.

Conséquence sur la persistance : `_step3Reached` gardait la règle « ne persister les préférences
que si l'utilisateur les a vues ». Ce n'est plus l'étape 3 qu'il faut avoir vue, c'est celle des
préférences, et le champ est renommé pour que la règle reste lisible.
"""

import pathlib
import re
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = pathlib.Path(__file__).resolve().parents[3]
TARGET = ROOT / "src" / "OnboardingWindow.cs"

data = TARGET.read_bytes()
if data[:3] != b"\xef\xbb\xbf":
    sys.exit("OnboardingWindow.cs a perdu son BOM")

LF_BEFORE = data.count(b"\n") - data.count(b"\r\n")
text = data.decode("utf-8")


def replace(name, old, new, expected=1):
    global text
    lines = old.replace("\r\n", "\n").split("\n")
    rx = re.compile(r"\r?\n".join(re.escape(l) for l in lines))
    matches = list(rx.finditer(text))
    if len(matches) != expected:
        sys.exit(f"{name} : {len(matches)} occurrence(s), {expected} attendue(s)\n---\n{old}\n---")
    out, cursor = [], 0
    for m in matches:
        eol = "\r\n" if "\r\n" in m.group(0) else "\n"
        out.append(text[cursor:m.start()])
        out.append(new.replace("\r\n", "\n").replace("\n", eol))
        cursor = m.end()
    out.append(text[cursor:])
    text = "".join(out)
    print(f"  {name:48s} {expected}×")


def replace_block(name, start_line, new):
    """Remplace de `start_line` jusqu'a l'accolade fermante de la methode."""
    global text
    i = text.find(start_line)
    if i < 0 or text.find(start_line, i + 1) >= 0:
        sys.exit(f"{name} : borne de debut absente ou multiple")
    end = text.find("\n    }", i)
    if end < 0:
        sys.exit(f"{name} : accolade fermante introuvable")
    end = text.index("}", end) + 1
    eol = "\r\n" if "\r\n" in text[i:end] else "\n"
    text = text[:i] + new.replace("\r\n", "\n").replace("\n", eol) + text[end:]
    print(f"  {name:48s} bloc")


# ── 1. Cinq etapes ──────────────────────────────────────────────────────────
replace(
    "cinquieme etape",
    """    private const int StepResources = 3;
    private const int StepCount = 4;""",
    """    private const int StepResources = 3;
    private const int StepPreferences = 4;
    private const int StepCount = 5;""",
)

# ── 2. La derniere etape n'est plus StepResources ──────────────────────────
replace(
    "derniere etape dans ShowStepForCapture",
    """        if (_currentStep == StepResources)
            _step3Reached = true;""",
    """        if (_currentStep == StepPreferences)
            _prefsScreenSeen = true;""",
)

replace(
    "libelle du bouton principal",
    """        string nextText = _currentStep == StepResources ? L.Onboarding_LetsGo : L.Onboarding_Next;""",
    """        string nextText = _currentStep == StepPreferences ? L.Onboarding_LetsGo : L.Onboarding_Next;""",
)

replace(
    "avance par le bouton Suivant",
    """                        if (_currentStep < StepResources) { _currentStep++; if (_currentStep == StepResources) _step3Reached = true; UpdateStepVisibility(); }""",
    """                        if (_currentStep < StepPreferences) { _currentStep++; if (_currentStep == StepPreferences) _prefsScreenSeen = true; UpdateStepVisibility(); }""",
)

replace(
    "avance au clavier",
    """                    else if (_currentStep < StepResources)
                    {
                        _currentStep++;
                        if (_currentStep == StepResources) _step3Reached = true;""",
    """                    else if (_currentStep < StepPreferences)
                    {
                        _currentStep++;
                        if (_currentStep == StepPreferences) _prefsScreenSeen = true;""",
)

# ── 3. Visibilite : les liens et les cases sur deux ecrans ─────────────────
replace(
    "visibilite des liens et des cases",
    """        int step3Vis = _currentStep == StepResources ? 1 : 0;
        Win32.ShowWindow(_hWndLinkLessons, step3Vis);
        Win32.ShowWindow(_hWndLinkGuide, step3Vis);
        Win32.ShowWindow(_hWndLinkFeedback, step3Vis);""",
    """        int linksVis = _currentStep == StepResources ? 1 : 0;
        int prefsVis = _currentStep == StepPreferences ? 1 : 0;
        int step3Vis = linksVis;
        Win32.ShowWindow(_hWndLinkLessons, linksVis);
        Win32.ShowWindow(_hWndLinkGuide, linksVis);
        Win32.ShowWindow(_hWndLinkFeedback, linksVis);""",
)

replace(
    "les trois cases suivent l'ecran des preferences",
    """        Win32.ShowWindow(_hWndChkAutoStart, step3Vis);
        Win32.ShowWindow(_hWndChkDontShow, step3Vis);
        Win32.ShowWindow(_hWndChkTraining, step3Vis);
        if (step3Vis == 1)""",
    """        Win32.ShowWindow(_hWndChkAutoStart, prefsVis);
        Win32.ShowWindow(_hWndChkDontShow, prefsVis);
        Win32.ShowWindow(_hWndChkTraining, prefsVis);
        if (prefsVis == 1)""",
)

replace(
    "repositionnement sur les deux derniers ecrans",
    """        if (_currentStep == StepResources)
            RepositionControls();""",
    """        if (_currentStep == StepResources || _currentStep == StepPreferences)
            RepositionControls();""",
)

# ── 4. Le champ de persistance dit ce qu'il garde ──────────────────────────
replace(
    "renommage du temoin de persistance",
    """    private bool _step3Reached;""",
    """    private bool _prefsScreenSeen;""",
)
text = text.replace("_step3Reached", "_prefsScreenSeen")

# ── 5. La barre de progression ─────────────────────────────────────────────
# Rien a faire : elle lit deja StepCount.

# ── 6. Deux mises en page au lieu d'une ────────────────────────────────────
replace_block(
    "GetStep3Layout coupee en deux",
    "    private void GetStep3Layout(int topY, int winW,",
    """    /// <summary>
    /// Mise en page du panneau des ressources, posé sous le titre de **son** écran.
    ///
    /// Il partageait un seul calcul avec le panneau des préférences, qui se plaçait sous lui.
    /// Depuis la coupe du 2026-08-30 ils sont sur deux écrans, et chacun part du haut du sien.
    /// </summary>
    private void GetResourcesLayout(int topY, int winW,
        out Win32.RECT panel,
        out int linksX, out int linksWidth, out int linkStartY, out int linkRowH,
        out int linkControlHeight)
    {
        int margin = S(BASE_MARGIN);
        int panelWidth = winW - margin * 2;
        int panelPaddingX = S(18);

        IntPtr hdc = Win32.GetDC(_hWnd);
        try
        {
            int pageTitleHeight = MeasureSingleLineHeight(hdc, _hFontPageTitle);
            linkControlHeight = Math.Max(S(28), MeasureSingleLineHeight(hdc, _hFontLinkStrong) + S(6));
            linkRowH = linkControlHeight + S(2);
            int height = S(16) + linkRowH * 4 + S(12);
            int top = topY + pageTitleHeight + S(12);

            panel = new Win32.RECT
            {
                left = margin,
                top = top,
                right = margin + panelWidth,
                bottom = top + height
            };
            linksX = panel.left + panelPaddingX;
            linksWidth = panelWidth - panelPaddingX * 2;
            linkStartY = panel.top + S(16);
        }
        finally
        {
            Win32.ReleaseDC(_hWnd, hdc);
        }
    }

    /// <summary>Mise en page du panneau des préférences, posé sous le titre de son écran.</summary>
    private void GetPrefsLayout(int topY, int winW,
        out Win32.RECT panel,
        out int checkboxX, out int checkboxWidth, out int checkboxY, out int checkboxSpacing,
        out int checkboxHeight, out int checkboxTrainingY,
        out int trainingDescY, out int trainingDescHeight)
    {
        int margin = S(BASE_MARGIN);
        int panelWidth = winW - margin * 2;
        int panelPaddingX = S(18);

        IntPtr hdc = Win32.GetDC(_hWnd);
        try
        {
            int pageTitleHeight = MeasureSingleLineHeight(hdc, _hFontPageTitle);
            int top = topY + pageTitleHeight + S(12);

            checkboxX = margin + panelPaddingX;
            checkboxWidth = panelWidth - panelPaddingX * 2;
            // + la marge de focus des deux cotes : l'anneau se dessine a l'exterieur du
            // controle, donc TryDrawItem rend le libelle dans un rectangle rentre d'autant.
            checkboxHeight = Math.Max(S(26), MeasureSingleLineHeight(hdc, _hFontBold) + S(10))
                + ThemeControls.FocusMargin(_dpi) * 2;
            checkboxY = top + S(16);
            checkboxSpacing = checkboxHeight + S(10);
            checkboxTrainingY = checkboxY + checkboxSpacing * 2;
            trainingDescY = checkboxTrainingY + checkboxHeight + S(2);
            trainingDescHeight = MeasureTextHeight(hdc, _hFontReassure, L.Onboarding_ChkTrainingDesc, checkboxWidth);

            int height = S(16) + checkboxHeight * 3 + S(10) * 2 + S(2) + trainingDescHeight + S(16);
            panel = new Win32.RECT
            {
                left = margin,
                top = top,
                right = margin + panelWidth,
                bottom = top + height
            };
        }
        finally
        {
            Win32.ReleaseDC(_hWnd, hdc);
        }
    }""",
)

# ── 7. Deux peintres au lieu d'un ──────────────────────────────────────────
replace_block(
    "PaintStep3 coupee en deux",
    "    private int PaintStep3(IntPtr hdc, IntPtr gfx, int cw, int ch, int y)",
    """    private int PaintStep3(IntPtr hdc, IntPtr gfx, int cw, int ch, int y)
    {
        int margin = S(BASE_MARGIN);
        GetResourcesLayout(y, cw, out var panel,
            out int linksX, out int linksWidth, out int linkStartY, out int linkRowH, out _);

        Win32.SelectObject(hdc, _hFontPageTitle);
        Win32.SetTextColor(hdc, CLR_TITLE);
        var titleRect = new Win32.RECT { left = margin, top = y, right = cw - margin, bottom = panel.top - S(12) };
        Win32.DrawTextW(hdc, L.Onboarding_SectionResources, -1, ref titleRect,
            Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);

        GdiHelpers.DrawPanel(hdc, panel, CLR_PANEL_BG, CLR_PANEL_BORDER, CLR_BADGE_BG, S(4));

        for (int row = 1; row < 4; row++)
        {
            var rowSep = new Win32.RECT
            {
                left = linksX,
                top = linkStartY + row * linkRowH - S(6),
                right = linksX + linksWidth,
                bottom = linkStartY + row * linkRowH - S(5)
            };
            GdiHelpers.FillSolidRect(hdc, rowSep, 0x00E3E3E3);
        }

        return panel.bottom;
    }

    /// <summary>
    /// Écran des préférences, séparé de celui des ressources le 2026-08-30. Les deux panneaux
    /// empilés faisaient de l'ancienne étape 3 la plus haute des quatre, et donc la hauteur de
    /// toute la fenêtre.
    /// </summary>
    private int PaintStep4(IntPtr hdc, IntPtr gfx, int cw, int ch, int y)
    {
        int margin = S(BASE_MARGIN);
        GetPrefsLayout(y, cw, out var panel,
            out int checkboxX, out int checkboxWidth, out _, out _, out _, out _,
            out int trainingDescY, out int trainingDescHeight);

        Win32.SelectObject(hdc, _hFontPageTitle);
        Win32.SetTextColor(hdc, CLR_TITLE);
        var titleRect = new Win32.RECT { left = margin, top = y, right = cw - margin, bottom = panel.top - S(12) };
        Win32.DrawTextW(hdc, L.Settings_SectionPreferences, -1, ref titleRect,
            Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);

        GdiHelpers.DrawPanel(hdc, panel, CLR_PANEL_BG, CLR_PANEL_BORDER, CLR_BADGE_BG, S(4));

        // Texte descriptif sous la case « Défi du jour » — simple texte dessiné, pas de contrôle
        // STATIC : pas d'interaction, juste une précision sous le libellé de la case.
        Win32.SelectObject(hdc, _hFontReassure);
        Win32.SetTextColor(hdc, CLR_REASSURE);
        var descRect = new Win32.RECT
        {
            left = checkboxX,
            top = trainingDescY,
            right = checkboxX + checkboxWidth,
            bottom = trainingDescY + trainingDescHeight
        };
        Win32.DrawTextW(hdc, L.Onboarding_ChkTrainingDesc, -1, ref descRect,
            Win32.DT_LEFT | Win32.DT_WORDBREAK | Win32.DT_NOPREFIX);

        return Math.Max(panel.bottom, descRect.bottom);
    }""",
)

# ── 8. Les deux aiguillages ────────────────────────────────────────────────
replace(
    "aiguillage de peinture",
    """            case StepResources: PaintStep3(hdc, gfx, cw, ch, y); break;""",
    """            case StepResources: PaintStep3(hdc, gfx, cw, ch, y); break;
            case StepPreferences: PaintStep4(hdc, gfx, cw, ch, y); break;""",
)

replace(
    "aiguillage de mesure",
    """                    StepUsage => PaintStep2(hdc, gfx, cw, 0, y),
                    _ => PaintStep3(hdc, gfx, cw, 0, y),""",
    """                    StepUsage => PaintStep2(hdc, gfx, cw, 0, y),
                    StepResources => PaintStep3(hdc, gfx, cw, 0, y),
                    _ => PaintStep4(hdc, gfx, cw, 0, y),""",
)

# ── 9. RepositionControls lit les deux mises en page ───────────────────────
replace(
    "RepositionControls",
    """        GetStep3Layout(_contentY, winW, out _, out _,
            out int linksX, out int linksWidth, out int linkStartY, out int linkRowH, out int linkControlHeight,
            out int checkboxX, out int checkboxWidth, out int checkboxY, out int checkboxSpacing, out int checkboxHeight,
            out int checkboxTrainingY, out _, out _);""",
    """        GetResourcesLayout(_contentY, winW, out _,
            out int linksX, out int linksWidth, out int linkStartY, out int linkRowH, out int linkControlHeight);
        GetPrefsLayout(_contentY, winW, out _,
            out int checkboxX, out int checkboxWidth, out int checkboxY, out int checkboxSpacing,
            out int checkboxHeight, out int checkboxTrainingY, out _, out _);""",
)

# ── Verification ────────────────────────────────────────────────────────────
if "GetStep3Layout" in text:
    sys.exit("GetStep3Layout subsiste")
if "_step3Reached" in text:
    sys.exit("_step3Reached subsiste")

data = text.encode("utf-8")
lf_after = data.count(b"\n") - data.count(b"\r\n")
TARGET.write_bytes(data)
print(f"\n{TARGET.name} écrit — LF isolés {LF_BEFORE} → {lf_after}")
