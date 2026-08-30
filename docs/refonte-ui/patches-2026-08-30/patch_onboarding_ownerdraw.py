"""Reste 1 de CH3 — les contrôles d'Onboarding passent en owner-draw.

Défaut mesuré le 2026-08-30 sur `captures/ch3/b2/` : **1,44 %** de pixels système sur les 18
captures d'Onboarding, contre 0,12 % pour Paramètres après sa migration du même jour. Trois
boutons de navigation et trois cases de l'étape 3, tous encore peints par Windows.

Le chantier est celui de Paramètres (`patch_settings_ownerdraw.py`), transposé : la peinture part
dans `src/OnboardingWindow.Theme.cs`, `BS_AUTOCHECKBOX` disparaît au profit de `_checkedState`,
les trois cases gagnent un cas dans `WM_COMMAND`, et la mise en page réserve la marge de focus
que `ThemeControls` dessine à l'extérieur du contrôle.

Trois différences avec Paramètres, toutes propres à cette fenêtre :

  1. `WS_CLIPCHILDREN` manquait encore ici. Sans lui, `OnPaint` recopie son tampon par-dessus les
     enfants ; Windows repeignait ses contrôles natifs après coup et le masquait.
  2. `BS_DEFPUSHBUTTON` quitte « Suivant ». La fenêtre n'a aucune boucle `IsDialogMessage` — vérifié
     par grep sur `src/` — donc ce drapeau n'a jamais fait qu'épaissir une bordure.
  3. L'accent suit « Essayer maintenant » tant qu'il est à l'écran (arbitrage d'Antoine du
     2026-08-30), et revient à « Suivant » / « C'est parti ! » sinon.

⚠️ `OnboardingWindow.cs` est en fins de ligne mixtes **et porte un BOM** : 1 471 CRLF pour 242 LF,
la zone de création des contrôles étant en LF et le reste en CRLF. Chaque ancre déduit sa
terminaison de la région où elle tombe, jamais du fichier.
"""

import pathlib
import re
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = pathlib.Path(__file__).resolve().parents[3]
TARGET = ROOT / "src" / "OnboardingWindow.cs"

data = TARGET.read_bytes()
if data[:3] != b"\xef\xbb\xbf":
    sys.exit("OnboardingWindow.cs a perdu son BOM — vérifier avant de patcher")

CRLF_BEFORE = data.count(b"\r\n")
LF_BEFORE = data.count(b"\n") - CRLF_BEFORE
print(f"  avant : CRLF {CRLF_BEFORE}, LF isolés {LF_BEFORE}")

# Sauts isolés consommés par les régions remplacées, et sauts isolés réémis. La règle du dépôt
# interdit de déduire la fin de ligne d'une seule ancre sans vérifier le compte des deux côtés :
# c'est cette comptabilité qui le fait, et le script refuse d'écrire si elle ne tombe pas juste.
LF_CONSUMED = 0
LF_EMITTED = 0


def pattern_for(old):
    """Ancre en expression régulière, chaque saut de ligne rendu indifférent à sa terminaison.

    Le motif du dépôt — essayer le bloc en CRLF puis en LF — ne suffit pas ici : le mélange de ce
    fichier n'est pas régional mais **par ligne**. Le bloc de création du bouton « Suivant » en est
    la preuve, quatre lignes dont la première se termine par un saut isolé et les trois autres par
    un CRLF. Aucun des deux encodages ne le trouve.
    """
    lines = old.replace("\r\n", "\n").split("\n")
    return re.compile(r"\r?\n".join(re.escape(line) for line in lines))


def replace(name, old, new, expected=1):
    """Remplace une ancre, quelle que soit la terminaison de chacune de ses lignes.

    La sortie est écrite en CRLF dès que la région trouvée en contient un — la terminaison
    dominante du fichier, 1 471 contre 242 — et en LF quand la région est en LF pur. Les sauts
    isolés qui tombent dans une région remplacée disparaissent donc, et eux seuls : le compte
    final est vérifié à la fin du script contre ce qui est attendu, jamais laissé au hasard.
    """
    global data

    rx = pattern_for(old)
    matches = list(rx.finditer(data.decode("utf-8")))
    if len(matches) != expected:
        sys.exit(f"{name} : ancre attendue {expected} fois, trouvée {len(matches)}\n---\n{old}\n---")

    text = data.decode("utf-8")
    out = []
    cursor = 0
    for m in matches:
        eol = "\r\n" if "\r\n" in m.group(0) else "\n"
        out.append(text[cursor:m.start()])
        out.append(new.replace("\r\n", "\n").replace("\n", eol))
        cursor = m.end()
    out.append(text[cursor:])
    data = "".join(out).encode("utf-8")

    global LF_CONSUMED, LF_EMITTED
    mixed = 0
    for m in matches:
        body = m.group(0)
        isolated = body.count("\n") - body.count("\r\n")
        LF_CONSUMED += isolated
        if isolated and body.count("\r\n"):
            mixed += 1
        if "\r\n" not in body:
            LF_EMITTED += new.replace("\r\n", "\n").count("\n")
    print(f"  {name:44s} {expected}×{'  region mixte' if mixed else ''}")


# ── 1. La classe devient partielle ──────────────────────────────────────────
replace(
    "classe partielle",
    "sealed class OnboardingWindow : IDisposable",
    "sealed partial class OnboardingWindow : IDisposable",
)

# ── 2. Les constantes que Windows ne sert plus ──────────────────────────────
replace(
    "constantes BM_/BS_ retirées",
    """    private const uint BS_AUTOCHECKBOX = 0x0003;
    private const uint BM_GETCHECK = 0x00F0;
    private const uint BM_SETCHECK = 0x00F1;
    private const uint BST_CHECKED = 0x0001;
""",
    """    // BS_AUTOCHECKBOX, BM_GETCHECK, BM_SETCHECK et BST_CHECKED sont partis le 2026-08-30 :
    // une case peinte par la fenêtre ne peut pas être cochée par Windows. L'état vit dans
    // OnboardingWindow.Theme.cs, et SetCheck / GetCheck ont pris leurs cinq sites d'appel.

    /// <summary>
    /// Style de la fenêtre, un seul nom pour ses deux lecteurs : la création et
    /// <c>ResizeWindow</c>, qui en déduit le cadre à chaque changement de DPI. Ils portaient la
    /// même expression recopiée, et le 2026-08-30 n'en a d'abord modifié qu'une.
    ///
    /// <c>WS_CLIPCHILDREN</c> depuis cette date : sans lui, <c>OnPaint</c> recopie son tampon
    /// par-dessus les enfants et efface ceux qui se sont déjà peints. Windows repeignait ses
    /// contrôles natifs après coup et le masquait ; owner-draw n'a pas ce secours.
    /// </summary>
    private const uint WindowStyle = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU
        | Win32.WS_CLIPCHILDREN;
""",
)

# ── 3. WS_CLIPCHILDREN, par le style partagé ────────────────────────────────
replace(
    "style partagé création + ResizeWindow",
    """        uint dwStyle = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU;""",
    """        uint dwStyle = WindowStyle;""",
    expected=2,
)

# ── 4. Le survol s'attache après la création des contrôles ──────────────────
replace(
    "AttachHoverTracking",
    """        CreateControls();
        SetWindowIcon();""",
    """        CreateControls();
        AttachHoverTracking();
        SetWindowIcon();""",
)

# ── 5. Les trois boutons ────────────────────────────────────────────────────
replace(
    "bouton Suivant en owner-draw",
    """        _hWndBtnNext = Win32.CreateWindowExW(0, "BUTTON", L.Onboarding_Next,
            Win32.WS_CHILD | Win32.WS_VISIBLE | 0x0001 | Win32.WS_TABSTOP,
            winW - margin - S(BASE_BTN_W_NEXT_MIN), bottomY, S(BASE_BTN_W_NEXT_MIN), S(BASE_BTN_H),
            _hWnd, (IntPtr)IDC_BTN_NEXT, hInstance, IntPtr.Zero);""",
    """        // BS_OWNERDRAW remplace BS_DEFPUSHBUTTON (0x0001) : sans boucle IsDialogMessage,
        // ce drapeau n'epaississait qu'une bordure systeme, et Entree n'a jamais valide ici.
        _hWndBtnNext = Win32.CreateWindowExW(0, "BUTTON", L.Onboarding_Next,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_OWNERDRAW | Win32.WS_TABSTOP,
            winW - margin - S(BASE_BTN_W_NEXT_MIN), bottomY, S(BASE_BTN_W_NEXT_MIN), ButtonRowHeight(),
            _hWnd, (IntPtr)IDC_BTN_NEXT, hInstance, IntPtr.Zero);""",
)

replace(
    "bouton Precedent en owner-draw",
    """        _hWndBtnPrev = Win32.CreateWindowExW(0, "BUTTON", L.Onboarding_Prev,
            Win32.WS_CHILD | Win32.WS_TABSTOP,
            margin, bottomY, S(BASE_BTN_W_PREV), S(BASE_BTN_H),
            _hWnd, (IntPtr)IDC_BTN_PREV, hInstance, IntPtr.Zero);""",
    """        _hWndBtnPrev = Win32.CreateWindowExW(0, "BUTTON", L.Onboarding_Prev,
            Win32.WS_CHILD | Win32.BS_OWNERDRAW | Win32.WS_TABSTOP,
            margin, bottomY, S(BASE_BTN_W_PREV), ButtonRowHeight(),
            _hWnd, (IntPtr)IDC_BTN_PREV, hInstance, IntPtr.Zero);""",
)

replace(
    "bouton Essayer en owner-draw",
    """        _hWndBtnTry = Win32.CreateWindowExW(0, "BUTTON", L.Onboarding_TryNow,
            Win32.WS_CHILD | Win32.WS_TABSTOP,
            0, bottomY, S(BASE_BTN_W_NEXT_MIN), S(BASE_BTN_H),
            _hWnd, (IntPtr)IDC_BTN_TRY, hInstance, IntPtr.Zero);""",
    """        _hWndBtnTry = Win32.CreateWindowExW(0, "BUTTON", L.Onboarding_TryNow,
            Win32.WS_CHILD | Win32.BS_OWNERDRAW | Win32.WS_TABSTOP,
            0, bottomY, S(BASE_BTN_W_NEXT_MIN), ButtonRowHeight(),
            _hWnd, (IntPtr)IDC_BTN_TRY, hInstance, IntPtr.Zero);""",
)

# ── 6. Les trois cases ──────────────────────────────────────────────────────
replace(
    "cases en owner-draw",
    """            Win32.WS_CHILD | BS_AUTOCHECKBOX | Win32.WS_TABSTOP,""",
    """            Win32.WS_CHILD | Win32.BS_OWNERDRAW | Win32.WS_TABSTOP,""",
    expected=3,
)

# ── 7. WM_DRAWITEM ──────────────────────────────────────────────────────────
replace(
    "WM_DRAWITEM",
    """            case Win32.WM_ERASEBKGND:
                return (IntPtr)1;""",
    """            case Win32.WM_ERASEBKGND:
                return (IntPtr)1;

            case Win32.WM_DRAWITEM:
                if (TryDrawItem(lParam))
                    return (IntPtr)1;
                break;""",
)

# ── 8. Les trois cases gagnent leur bascule ─────────────────────────────────
replace(
    "WM_COMMAND des trois cases",
    """                    case IDC_CHK_TRAINING:
                        // Applique immédiatement (pas à la fermeture du wizard) — même pattern
                        // que IDC_CHK_TRAINING dans SettingsWindow.cs.
                        if (code == 0)
                        {
                            bool trainingEnabled = Win32.SendMessageW(_hWndChkTraining, BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == (IntPtr)BST_CHECKED;
                            ConfigManager.SetTrainingEnabled(trainingEnabled);
                        }
                        break;""",
    """                    // Un contrôle owner-draw ne bascule plus tout seul. « Lancer au démarrage »
                    // et « Ne plus afficher » n'avaient aucun cas ici — leur valeur n'était lue
                    // qu'à la fermeture, dans Close() — et en gagnent un.
                    case IDC_CHK_AUTOSTART:
                        if (code == 0) ToggleCheck(_hWndChkAutoStart);
                        break;
                    case IDC_CHK_DONT_SHOW:
                        if (code == 0) ToggleCheck(_hWndChkDontShow);
                        break;
                    case IDC_CHK_TRAINING:
                        // Applique immédiatement (pas à la fermeture du wizard) — même pattern
                        // que IDC_CHK_TRAINING dans SettingsWindow.cs.
                        if (code == 0)
                        {
                            ToggleCheck(_hWndChkTraining);
                            ConfigManager.SetTrainingEnabled(GetCheck(_hWndChkTraining));
                        }
                        break;""",
)

# ── 9. Les cinq sites de BM_SETCHECK / BM_GETCHECK ──────────────────────────
replace(
    "resync de l'opt-in a l'etape 3",
    """            Win32.SendMessageW(_hWndChkTraining, BM_SETCHECK,
                ConfigManager.TrainingEnabled ? (IntPtr)BST_CHECKED : IntPtr.Zero, IntPtr.Zero);""",
    """            SetCheck(_hWndChkTraining, ConfigManager.TrainingEnabled);""",
)

replace(
    "etats par defaut a l'ouverture",
    """        Win32.SendMessageW(_hWndChkAutoStart, BM_SETCHECK, (IntPtr)BST_CHECKED, IntPtr.Zero);
        Win32.SendMessageW(_hWndChkDontShow, BM_SETCHECK, IntPtr.Zero, IntPtr.Zero);""",
    """        SetCheck(_hWndChkAutoStart, true);
        SetCheck(_hWndChkDontShow, false);""",
)

replace(
    "lecture des deux cases a la fermeture",
    """            var checkState = Win32.SendMessageW(_hWndChkDontShow, BM_GETCHECK, IntPtr.Zero, IntPtr.Zero);
            ConfigManager.SetShowOnboardingAtStartup(checkState != (IntPtr)BST_CHECKED);

            var autoStartState = Win32.SendMessageW(_hWndChkAutoStart, BM_GETCHECK, IntPtr.Zero, IntPtr.Zero);
            bool autoStart = autoStartState == (IntPtr)BST_CHECKED;""",
    """            ConfigManager.SetShowOnboardingAtStartup(!GetCheck(_hWndChkDontShow));

            bool autoStart = GetCheck(_hWndChkAutoStart);""",
)

replace(
    "RefreshAutoStartCheckbox",
    """        Win32.SendMessageW(_hWndChkAutoStart, BM_SETCHECK,
            AutoStart.IsRegistered ? (IntPtr)BST_CHECKED : IntPtr.Zero, IntPtr.Zero);""",
    """        SetCheck(_hWndChkAutoStart, AutoStart.IsRegistered);""",
)

# ── 10. L'accent de la barre de navigation ──────────────────────────────────
replace(
    "suivi de la presence d'Essayer",
    """        bool isStep1Attempted    = _currentStep == 0 &&  _learningModuleAttempted && !_learningModuleDone; // etat B""",
    """        bool isStep1Attempted    = _currentStep == 0 &&  _learningModuleAttempted && !_learningModuleDone; // etat B
        // L'accent va a « Essayer maintenant » tant qu'il est la (arbitrage du 2026-08-30), a
        // « Suivant » sinon. KindOf lit ce champ et non IsWindowVisible, qui rend faux pour tout
        // enfant d'une fenetre que le banc n'a pas encore montree.
        _tryButtonShown = isStep1NotAttempted || isStep1Attempted;""",
)

# ── 11. Largeurs et hauteurs de la barre de navigation ──────────────────────
replace(
    "largeur d'Essayer",
    """            string tryText = L.Onboarding_TryNow;
            IntPtr hdc = Win32.GetDC(_hWnd);
            int tryTextW;
            try { tryTextW = MeasureSingleLineWidth(hdc, _hFontButton, tryText); }
            finally { Win32.ReleaseDC(_hWnd, hdc); }
            int tryWidth = Math.Max(S(BASE_BTN_W_NEXT_MIN), tryTextW + S(BASE_BTN_TEXT_PAD * 2));""",
    """            int tryWidth = ButtonRowWidth(L.Onboarding_TryNow, BASE_BTN_W_NEXT_MIN);""",
    expected=2,
)

replace(
    "hauteur d'Essayer",
    """            Win32.MoveWindow(_hWndBtnTry, tryX, btnBottomY, tryWidth, S(BASE_BTN_H), true);""",
    """            Win32.MoveWindow(_hWndBtnTry, tryX, btnBottomY, tryWidth, ButtonRowHeight(), true);""",
    expected=2,
)

replace(
    "hauteur de Suivant, etat B",
    """            Win32.MoveWindow(_hWndBtnNext, nextGeomB.x, btnBottomY, nextGeomB.width, S(BASE_BTN_H), true);""",
    """            Win32.MoveWindow(_hWndBtnNext, nextGeomB.x, btnBottomY, nextGeomB.width, ButtonRowHeight(), true);""",
)

replace(
    "hauteur de Suivant, etat C",
    """            Win32.MoveWindow(_hWndBtnNext, nextGeomC.x, btnBottomY, nextGeomC.width, S(BASE_BTN_H), true);""",
    """            Win32.MoveWindow(_hWndBtnNext, nextGeomC.x, btnBottomY, nextGeomC.width, ButtonRowHeight(), true);""",
)

replace(
    "geometrie de Precedent",
    """        Win32.MoveWindow(_hWndBtnPrev, margin, bottomY, S(BASE_BTN_W_PREV), S(BASE_BTN_H), true);""",
    """        Win32.MoveWindow(_hWndBtnPrev, margin, bottomY,
            ButtonRowWidth(L.Onboarding_Prev, BASE_BTN_W_PREV), ButtonRowHeight(), true);""",
)

replace(
    "ComputeNextButtonGeometry",
    """    private (int x, int width) ComputeNextButtonGeometry(string text, int winW, int margin)
    {
        IntPtr hdc = Win32.GetDC(_hWnd);
        try
        {
            int textWidth = MeasureSingleLineWidth(hdc, _hFontButton, text);
            int width = Math.Max(S(BASE_BTN_W_NEXT_MIN), textWidth + S(BASE_BTN_TEXT_PAD * 2));
            int x = winW - margin - width;
            return (x, width);
        }
        finally
        {
            Win32.ReleaseDC(_hWnd, hdc);
        }
    }""",
    """    private (int x, int width) ComputeNextButtonGeometry(string text, int winW, int margin)
    {
        int width = ButtonRowWidth(text, BASE_BTN_W_NEXT_MIN);
        return (winW - margin - width, width);
    }""",
)

# ── 12. La ligne de case reserve la marge de focus ──────────────────────────
replace(
    "hauteur des cases",
    """            checkboxHeight = Math.Max(S(26), MeasureSingleLineHeight(hdc, _hFontBold) + S(10));""",
    """            // + la marge de focus des deux cotes : l'anneau se dessine a l'exterieur du
            // controle, donc TryDrawItem rend le libelle dans un rectangle rentre d'autant.
            checkboxHeight = Math.Max(S(26), MeasureSingleLineHeight(hdc, _hFontBold) + S(10))
                + ThemeControls.FocusMargin(_dpi) * 2;""",
)

# ── 13. Deux rafraichissements que les enfants ne recoivent pas ─────────────
replace(
    "repeindre les controles a la bascule de theme",
    """            ThemeWindow.ApplyClassBackground(_hWnd, CLR_BG);
            ThemeWindow.ApplyChrome(_hWnd);
        };""",
    """            ThemeWindow.ApplyClassBackground(_hWnd, CLR_BG);
            ThemeWindow.ApplyChrome(_hWnd);
            // InvalidateRect sur la fenetre n'atteint pas ses enfants, et depuis
            // WS_CLIPCHILDREN le parent ne peint meme plus dessus : sans cette ligne les six
            // controles garderaient la palette du theme precedent.
            InvalidateOwnerDrawControls();
        };""",
)

replace(
    "repeindre les controles au changement d'etape",
    """        if (_currentStep == 2)
            RepositionControls();

        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);""",
    """        if (_currentStep == 2)
            RepositionControls();

        InvalidateOwnerDrawControls();
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);""",
)

# ── 14. Detachement ─────────────────────────────────────────────────────────
replace(
    "DetachHoverTracking",
    """        if (_hWndBtnTry != IntPtr.Zero) Win32.RemoveWindowSubclass(_hWndBtnTry, _buttonArrowSubclassProc, (UIntPtr)22);""",
    """        if (_hWndBtnTry != IntPtr.Zero) Win32.RemoveWindowSubclass(_hWndBtnTry, _buttonArrowSubclassProc, (UIntPtr)22);
        DetachHoverTracking();""",
)

# ── Verification finale ─────────────────────────────────────────────────────
# Les quatre noms ne doivent plus survivre qu'en commentaire — celui qui explique leur départ.
for line in data.decode("utf-8").splitlines():
    if line.lstrip().startswith("//"):
        continue
    for dead in ("BM_SETCHECK", "BM_GETCHECK", "BST_CHECKED", "BS_AUTOCHECKBOX"):
        if dead in line:
            sys.exit(f"reste une reference a {dead} apres le patch :\n{line}")

crlf_after = data.count(b"\r\n")
lf_after = data.count(b"\n") - crlf_after
expected_lf = LF_BEFORE - LF_CONSUMED + LF_EMITTED
if lf_after != expected_lf:
    sys.exit(f"fins de ligne : {lf_after} LF isolés, {expected_lf} attendus "
             f"({LF_BEFORE} avant, {LF_CONSUMED} consommés, {LF_EMITTED} réémis)")

TARGET.write_bytes(data)
print(f"\n{TARGET.name} écrit — CRLF {crlf_after}, LF isolés {lf_after} "
      f"(était {LF_BEFORE} ; {LF_CONSUMED} tombés dans une région remplacée)")
