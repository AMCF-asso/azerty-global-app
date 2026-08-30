"""Onboarding mesure son contenu au lieu de le supposer.

Décision d'Antoine du 2026-08-30 : « si le problème c'est les constantes en dur, on résout ce
problème ». `BASE_WIN_H = 763` était le dernier obstacle — la hauteur de la fenêtre valait
`763 × échelle × densité` quoi qu'il y ait dedans, donc ni la rampe typographique ni rien d'autre
ne pouvait la réduire : baisser la taille du texte y laissait la même fenêtre avec plus de vide.

La mesure se fait comme celle de Paramètres à `3d158aa`, à une différence près : ici la mise en
page **est** le code de peinture, et il n'existe nulle part en double. Plutôt que de le réécrire
en version « mesure » — deux copies qui divergeraient au premier changement — la fenêtre le
rejoue une fois dans un DC de rebut et lit le `y` final. Les trois `PaintStepN` rendent donc
désormais le bas de leur contenu.

La hauteur retenue est le plus grand des trois, plus l'écart de la barre de navigation et sa
propre marge basse. Elle est ensuite **plafonnée à la zone de travail de l'écran** : la barre de
navigation étant ancrée sur le bas du client, elle reste atteignable même quand le contenu ne
tient pas — ce qui n'était pas le cas jusqu'ici, la capture à 200 % du 2026-08-30 montrant une
fenêtre dont les boutons tombaient hors de l'écran.

`BASE_WIN_H` disparaît. `BASE_WIN_W` reste : c'est une largeur de conception, les cartes
s'étendent dessus, et aucune mesure ne la contredit.
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
    print(f"  {name:46s} {expected}×")


# ── 1. La constante s'en va, deux constantes de barre la remplacent ─────────
replace(
    "BASE_WIN_H remplacee",
    """    private const int BASE_WIN_W = 560;
    // +90 (v1.2.0) pour la 3e case « Défi du jour » + son texte descriptif sur 2 lignes,
    // ajoutés au panneau Préférences de l'étape 3 (cf. GetStep3Layout).
    private const int BASE_WIN_H = 763;""",
    """    private const int BASE_WIN_W = 560;

    /// <summary>
    /// Écart entre le bas du contenu le plus long et le haut de la barre de navigation.
    ///
    /// BASE_WIN_H = 763 vivait ici jusqu'au 2026-08-30. C'était la hauteur du client, posée en
    /// dur : la fenêtre faisait 763 × échelle × densité quel que soit son contenu, et aucune
    /// réduction de la taille du texte ne pouvait la faire maigrir. Elle se mesure désormais,
    /// et cette constante-ci est la seule qui reste de son ancienne mise en page.
    /// </summary>
    private const int BASE_NAV_GAP = 14;""",
)

# ── 2. Les champs de taille mesurée ─────────────────────────────────────────
replace(
    "champs de taille mesuree",
    """    private bool _inputPaused;""",
    """    private bool _inputPaused;

    /// <summary>
    /// Hauteur du client, mesurée sur le plus long des trois écrans et plafonnée à la zone de
    /// travail. Vaut 0 tant que <see cref="MeasureClientHeight"/> n'a pas tourné, ce qui
    /// n'arrive qu'avant la création de la fenêtre.
    /// </summary>
    private int _clientH;""",
)

# ── 3. Les trois PaintStepN rendent le bas de leur contenu ─────────────────
replace(
    "PaintStep1 rend son bas",
    """    private void PaintStep1(IntPtr hdc, int cw, int ch, int y)
    {""",
    """    private int PaintStep1(IntPtr hdc, int cw, int ch, int y)
    {""",
)
replace(
    "PaintStep1 : bas du contenu",
    """        Win32.DrawTextW(hdc, reassure, -1, ref reassureRect,
            Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
    }""",
    """        Win32.DrawTextW(hdc, reassure, -1, ref reassureRect,
            Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
        return reassureRect.bottom;
    }""",
)

replace(
    "PaintStep2 rend son bas",
    """    private void PaintStep2(IntPtr hdc, IntPtr gfx, int cw, int ch, int y)
    {""",
    """    private int PaintStep2(IntPtr hdc, IntPtr gfx, int cw, int ch, int y)
    {""",
)
replace(
    "PaintStep2 : bas du contenu",
    """        DrawStepCardWithRuns(hdc, margin, cw, ref y, "4",
            L.Onboarding_Card4Title,
            GetShortcutRuns(null, $"Ctrl + {L.Settings_ShortcutModifier2} + W", L.Onboarding_Card4Suffix));
    }""",
    """        DrawStepCardWithRuns(hdc, margin, cw, ref y, "4",
            L.Onboarding_Card4Title,
            GetShortcutRuns(null, $"Ctrl + {L.Settings_ShortcutModifier2} + W", L.Onboarding_Card4Suffix));
        return y;
    }""",
)

replace(
    "PaintStep3 rend son bas",
    """    private void PaintStep3(IntPtr hdc, IntPtr gfx, int cw, int ch, int y)
    {""",
    """    private int PaintStep3(IntPtr hdc, IntPtr gfx, int cw, int ch, int y)
    {""",
)
replace(
    "PaintStep3 : bas du contenu",
    """        Win32.DrawTextW(hdc, L.Onboarding_ChkTrainingDesc, -1, ref trainingDescRect,
            Win32.DT_LEFT | Win32.DT_WORDBREAK | Win32.DT_NOPREFIX);
    }""",
    """        Win32.DrawTextW(hdc, L.Onboarding_ChkTrainingDesc, -1, ref trainingDescRect,
            Win32.DT_LEFT | Win32.DT_WORDBREAK | Win32.DT_NOPREFIX);
        return Math.Max(prefsPanel.bottom, trainingDescRect.bottom);
    }""",
)

# ── 4. OnPaint appelle les nouvelles signatures ────────────────────────────
replace(
    "OnPaint ignore le bas rendu",
    """            case 0: PaintStep1(hdc, cw, ch, y); break;
            case 1: PaintStep2(hdc, gfx, cw, ch, y); break;
            case 2: PaintStep3(hdc, gfx, cw, ch, y); break;""",
    """            case 0: PaintStep1(hdc, cw, ch, y); break;
            case 1: PaintStep2(hdc, gfx, cw, ch, y); break;
            case 2: PaintStep3(hdc, gfx, cw, ch, y); break;
            // Le bas rendu ne sert qu'à MeasureClientHeight ; en peinture il est déjà connu.""",
)

# ── 5. La mesure elle-meme ─────────────────────────────────────────────────
replace(
    "MeasureClientHeight",
    """    // ═══════════════════════════════════════════════════════════════
    // Redimensionnement et repositionnement
    // ═══════════════════════════════════════════════════════════════
    private void ResizeWindow()""",
    """    // ═══════════════════════════════════════════════════════════════
    // Mesure
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Hauteur de client que réclame le plus long des trois écrans, barre de navigation comprise,
    /// plafonnée à la zone de travail de l'écran où la fenêtre va naître.
    ///
    /// La mise en page de cette fenêtre **est** son code de peinture : il n'en existe pas de
    /// seconde version, et en écrire une la ferait diverger au premier changement. La mesure
    /// rejoue donc la peinture une fois par étape dans un DC de rebut et lit le bas rendu. Le
    /// coût est de trois rendus hors écran, à la création et au changement de DPI.
    ///
    /// Le plafond n'est pas cosmétique : la barre de navigation est ancrée sur le bas du client,
    /// donc plafonner la garde atteignable. Sans lui, la fenêtre à 200 % dépassait l'écran par le
    /// bas et ses boutons devenaient inaccessibles — mesuré le 2026-08-30.
    /// </summary>
    private int MeasureClientHeight()
    {
        int cw = S(BASE_WIN_W);
        int required = 0;

        IntPtr hdcScreen = Win32.GetDC(IntPtr.Zero);
        IntPtr hdc = Win32.CreateCompatibleDC(hdcScreen);
        IntPtr bmp = Win32.CreateCompatibleBitmap(hdcScreen, Math.Max(cw, 1), 8);
        IntPtr previous = Win32.SelectObject(hdc, bmp);
        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);
        Win32.SetBkMode(hdc, 1);
        Win32.GdipCreateFromHDC(hdc, out IntPtr gfx);

        try
        {
            for (int step = 0; step < StepCountForCapture; step++)
            {
                int y = S(10);
                DrawHeader(hdc, gfx, cw, ref y);
                DrawProgressBar(hdc, cw, ref y);
                int bottom = step switch
                {
                    0 => PaintStep1(hdc, cw, 0, y),
                    1 => PaintStep2(hdc, gfx, cw, 0, y),
                    _ => PaintStep3(hdc, gfx, cw, 0, y),
                };
                required = Math.Max(required, bottom);
            }
        }
        finally
        {
            if (gfx != IntPtr.Zero)
                Win32.GdipDeleteGraphics(gfx);
            Win32.SelectObject(hdc, previous);
            Win32.DeleteObject(bmp);
            Win32.DeleteDC(hdc);
        }

        required += S(BASE_NAV_GAP) + S(BASE_BOTTOM_MARGIN);
        return Math.Min(required, MaxClientHeight());
    }

    /// <summary>
    /// Plus grande hauteur de client que l'écran laisse, cadre déduit. L'écran est celui sous le
    /// curseur, comme celui que <see cref="CreateMainWindow"/> choisit pour se centrer.
    /// </summary>
    private int MaxClientHeight()
    {
        Win32.GetCursorPos(out var cursor);
        var monitor = Win32.MonitorFromPoint(cursor, 0x00000001);
        var info = new Win32.MONITORINFO { cbSize = Marshal.SizeOf<Win32.MONITORINFO>() };
        if (!Win32.GetMonitorInfo(monitor, ref info))
            return int.MaxValue;

        int work = info.rcWork.bottom - info.rcWork.top;
        var frame = new Win32.RECT { left = 0, top = 0, right = 100, bottom = 100 };
        Win32.AdjustWindowRectEx(ref frame, WindowStyle, false, Win32.WS_EX_TOPMOST);
        int chrome = (frame.bottom - frame.top) - 100;
        return Math.Max(S(200), work - chrome);
    }

    // ═══════════════════════════════════════════════════════════════
    // Redimensionnement et repositionnement
    // ═══════════════════════════════════════════════════════════════
    private void ResizeWindow()""",
)

# ── 6. Les six lectures de S(BASE_WIN_H) ───────────────────────────────────
replace(
    "ResizeWindow mesure",
    """        int winW = S(BASE_WIN_W);
        int winH = S(BASE_WIN_H);
        uint dwStyle = WindowStyle;
        uint dwExStyle = Win32.WS_EX_TOPMOST;
        var adjustRect = new Win32.RECT { left = 0, top = 0, right = winW, bottom = winH };
        Win32.AdjustWindowRectEx(ref adjustRect, dwStyle, false, dwExStyle);
        int windowW = adjustRect.right - adjustRect.left;
        int windowH = adjustRect.bottom - adjustRect.top;
        Win32.GetWindowRect(_hWnd, out var currentRect);""",
    """        _clientH = MeasureClientHeight();
        int winW = S(BASE_WIN_W);
        int winH = _clientH;
        uint dwStyle = WindowStyle;
        uint dwExStyle = Win32.WS_EX_TOPMOST;
        var adjustRect = new Win32.RECT { left = 0, top = 0, right = winW, bottom = winH };
        Win32.AdjustWindowRectEx(ref adjustRect, dwStyle, false, dwExStyle);
        int windowW = adjustRect.right - adjustRect.left;
        int windowH = adjustRect.bottom - adjustRect.top;
        Win32.GetWindowRect(_hWnd, out var currentRect);""",
)

replace(
    "CreateMainWindow mesure",
    """        int winW = S(BASE_WIN_W);
        int winH = S(BASE_WIN_H);
        uint dwStyle = WindowStyle;
        uint dwExStyle = Win32.WS_EX_TOPMOST;
        var adjustRect = new Win32.RECT { left = 0, top = 0, right = winW, bottom = winH };""",
    """        _clientH = MeasureClientHeight();
        int winW = S(BASE_WIN_W);
        int winH = _clientH;
        uint dwStyle = WindowStyle;
        uint dwExStyle = Win32.WS_EX_TOPMOST;
        var adjustRect = new Win32.RECT { left = 0, top = 0, right = winW, bottom = winH };""",
)

# CreateControls et RepositionControls portent la meme ligne.
replace(
    "bottomY suit la mesure",
    """        int bottomY = S(BASE_WIN_H) - S(BASE_BOTTOM_MARGIN);""",
    """        int bottomY = _clientH - S(BASE_BOTTOM_MARGIN);""",
    expected=2,
)

replace(
    "UpdateStepVisibility suit la mesure",
    """        int btnBottomY = S(BASE_WIN_H) - S(BASE_BOTTOM_MARGIN);""",
    """        int btnBottomY = _clientH - S(BASE_BOTTOM_MARGIN);""",
)

replace(
    "Precedent suit la mesure",
    """        MoveButton(_hWndBtnPrev, S(BASE_MARGIN), S(BASE_WIN_H) - S(BASE_BOTTOM_MARGIN),
            ButtonRowWidth(L.Onboarding_Prev, BASE_BTN_W_PREV));""",
    """        MoveButton(_hWndBtnPrev, S(BASE_MARGIN), _clientH - S(BASE_BOTTOM_MARGIN),
            ButtonRowWidth(L.Onboarding_Prev, BASE_BTN_W_PREV));""",
)

# ── Verification ────────────────────────────────────────────────────────────
# Le nom ne doit plus survivre qu'en commentaire, celui qui explique sa disparition.
for line in text.splitlines():
    if "BASE_WIN_H" in line and not line.lstrip().startswith("//"):
        sys.exit(f"BASE_WIN_H subsiste hors commentaire :\n{line}")

data = text.encode("utf-8")
lf_after = data.count(b"\n") - data.count(b"\r\n")
TARGET.write_bytes(data)
print(f"\n{TARGET.name} écrit — LF isolés {LF_BEFORE} → {lf_after}")
