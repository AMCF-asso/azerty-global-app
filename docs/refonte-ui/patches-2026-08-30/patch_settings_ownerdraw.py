"""CH3 passe 2, lot A2 — les treize contrôles et la liste de Paramètres passent en owner-draw.

SettingsWindow.cs est en LF pur sans BOM (mesuré le 2026-08-30) ; le script refuse d'écrire si
ce n'est plus vrai. Chaque ancre est comptée avant qu'un seul octet ne soit écrit.

⚠️ **L'ordre des remplacements de style compte.** Les quatre boutons ordinaires, les quatre
cases et les cinq radios convergent tous vers la même ligne de style une fois patchés : si les
boutons passaient après les radios, leur ancre — la ligne sans BS_AUTORADIOBUTTON — se
retrouverait à sept occurrences au lieu de quatre. Les boutons passent donc en premier.
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
    print(f"  {name:38s} {expected}×")


# ══════════════════════════════════════════════════════════════════════════
# 1. La classe devient partielle — SettingsWindow.Theme.cs porte la peinture
# ══════════════════════════════════════════════════════════════════════════

replace(
    "classe partielle",
    "sealed class SettingsWindow : IDisposable",
    "sealed partial class SettingsWindow : IDisposable",
)

# ══════════════════════════════════════════════════════════════════════════
# 2. Styles de création — boutons d'abord, voir l'avertissement en tête
# ══════════════════════════════════════════════════════════════════════════

replace(
    "4 boutons → BS_OWNERDRAW",
    "            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP,\n            0, 0, 0, 0,",
    "            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_OWNERDRAW | Win32.WS_TABSTOP,\n            0, 0, 0, 0,",
    expected=4,
)

replace(
    "4 cases → BS_OWNERDRAW",
    "            Win32.WS_CHILD | Win32.WS_VISIBLE | BS_AUTOCHECKBOX | Win32.WS_TABSTOP,",
    "            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_OWNERDRAW | Win32.WS_TABSTOP,",
    expected=4,
)

replace(
    "2 radios de tête de groupe",
    "            Win32.WS_CHILD | Win32.WS_VISIBLE | BS_AUTORADIOBUTTON | WS_GROUP | Win32.WS_TABSTOP,",
    "            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_OWNERDRAW | WS_GROUP | Win32.WS_TABSTOP,",
    expected=2,
)

replace(
    "3 radios de suite de groupe",
    "            Win32.WS_CHILD | Win32.WS_VISIBLE | BS_AUTORADIOBUTTON | Win32.WS_TABSTOP,",
    "            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.BS_OWNERDRAW | Win32.WS_TABSTOP,",
    expected=3,
)

replace(
    "liste → LBS_OWNERDRAWFIXED",
    "            LBS_NOTIFY | LBS_NOINTEGRALHEIGHT | WS_VSCROLL | WS_BORDER,",
    "            // WS_BORDER retiré : le cadre système ne connaît aucun jeton, et la charte le\n"
    "            // remplace par celui d'un champ, peint par PaintCompatPanel.\n"
    "            LBS_NOTIFY | LBS_NOINTEGRALHEIGHT | LBS_OWNERDRAWFIXED | LBS_HASSTRINGS | WS_VSCROLL,",
)

# ══════════════════════════════════════════════════════════════════════════
# 3. BM_SETCHECK / BM_GETCHECK → SetCheck / GetCheck
# ══════════════════════════════════════════════════════════════════════════

for field in ("_hWndChkNotifications", "_hWndChkOnboarding", "_hWndChkTraining"):
    replace(
        f"création : {field}",
        f"            Win32.SendMessageW({field}, BM_SETCHECK, (IntPtr)BST_CHECKED, IntPtr.Zero);",
        f"            SetCheck({field}, true);",
    )

replace(
    "Show : trois cases",
    """        Win32.SendMessageW(_hWndChkTraining, BM_SETCHECK,
            ConfigManager.TrainingEnabled ? (IntPtr)BST_CHECKED : IntPtr.Zero, IntPtr.Zero);
        Win32.SendMessageW(_hWndChkNotifications, BM_SETCHECK,
            ConfigManager.NotificationsEnabled ? (IntPtr)BST_CHECKED : IntPtr.Zero, IntPtr.Zero);""",
    """        SetCheck(_hWndChkTraining, ConfigManager.TrainingEnabled);
        SetCheck(_hWndChkNotifications, ConfigManager.NotificationsEnabled);""",
)

replace(
    "Show : onboarding",
    """        Win32.SendMessageW(_hWndChkOnboarding, BM_SETCHECK,
            ConfigManager.ShowOnboardingAtStartup ? (IntPtr)BST_CHECKED : IntPtr.Zero, IntPtr.Zero);""",
    """        SetCheck(_hWndChkOnboarding, ConfigManager.ShowOnboardingAtStartup);""",
)

replace(
    "Close : autostart",
    "        bool autoStart = Win32.SendMessageW(_hWndChkAutoStart, BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == (IntPtr)BST_CHECKED;",
    "        bool autoStart = GetCheck(_hWndChkAutoStart);",
)

replace(
    "Close : notifications",
    "            bool notifications = Win32.SendMessageW(_hWndChkNotifications, BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == (IntPtr)BST_CHECKED;",
    "            bool notifications = GetCheck(_hWndChkNotifications);",
)

replace(
    "Close : onboarding",
    "            bool showOnboarding = Win32.SendMessageW(_hWndChkOnboarding, BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == (IntPtr)BST_CHECKED;",
    "            bool showOnboarding = GetCheck(_hWndChkOnboarding);",
)

replace(
    "RefreshAutoStartCheckbox",
    """        Win32.SendMessageW(_hWndChkAutoStart, BM_SETCHECK,
            AutoStart.IsRegistered ? (IntPtr)BST_CHECKED : IntPtr.Zero, IntPtr.Zero);""",
    """        SetCheck(_hWndChkAutoStart, AutoStart.IsRegistered);""",
)

replace(
    "RefreshLanguageRadios",
    """        Win32.SendMessageW(_hWndRadioLangFr, BM_SETCHECK, isEnglish ? IntPtr.Zero : (IntPtr)BST_CHECKED, IntPtr.Zero);
        Win32.SendMessageW(_hWndRadioLangEn, BM_SETCHECK, isEnglish ? (IntPtr)BST_CHECKED : IntPtr.Zero, IntPtr.Zero);""",
    """        SetCheck(_hWndRadioLangFr, !isEnglish);
        SetCheck(_hWndRadioLangEn, isEnglish);""",
)

replace(
    "RefreshCompatSelectionUi",
    """        Win32.SendMessageW(_hWndRadioCompatAuto, BM_SETCHECK,
            hasSelection && mode == null ? (IntPtr)BST_CHECKED : IntPtr.Zero, IntPtr.Zero);
        Win32.SendMessageW(_hWndRadioCompatForceOn, BM_SETCHECK,
            mode == "forceOn" ? (IntPtr)BST_CHECKED : IntPtr.Zero, IntPtr.Zero);
        Win32.SendMessageW(_hWndRadioCompatForceOff, BM_SETCHECK,
            mode == "forceOff" ? (IntPtr)BST_CHECKED : IntPtr.Zero, IntPtr.Zero);""",
    """        SetCheck(_hWndRadioCompatAuto, hasSelection && mode == null);
        SetCheck(_hWndRadioCompatForceOn, mode == "forceOn");
        SetCheck(_hWndRadioCompatForceOff, mode == "forceOff");""",
)

# ══════════════════════════════════════════════════════════════════════════
# 4. Un clic ne bascule plus rien tout seul
# ══════════════════════════════════════════════════════════════════════════

replace(
    "WM_COMMAND : bascule des cases",
    """                    case IDC_CHK_TRAINING:
                        if (code == 0)
                        {
                            bool enabled = Win32.SendMessageW(_hWndChkTraining, BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == (IntPtr)BST_CHECKED;
                            ConfigManager.SetTrainingEnabled(enabled);
                        }
                        break;""",
    """                    // Les quatre cases basculent ici depuis qu'elles sont owner-draw :
                    // BS_AUTOCHECKBOX le faisait avant d'envoyer BN_CLICKED, plus personne ne
                    // le fait à sa place. Les trois dernières n'avaient aucun cas — leur valeur
                    // n'était lue qu'à la fermeture de la fenêtre, ce qui suffisait tant que
                    // Windows la tenait.
                    case IDC_CHK_TRAINING:
                        if (code == 0)
                        {
                            ToggleCheck(_hWndChkTraining);
                            ConfigManager.SetTrainingEnabled(GetCheck(_hWndChkTraining));
                        }
                        break;
                    case IDC_CHK_AUTOSTART:
                        if (code == 0) ToggleCheck(_hWndChkAutoStart);
                        break;
                    case IDC_CHK_NOTIFICATIONS:
                        if (code == 0) ToggleCheck(_hWndChkNotifications);
                        break;
                    case IDC_CHK_ONBOARDING:
                        if (code == 0) ToggleCheck(_hWndChkOnboarding);
                        break;""",
)

# ══════════════════════════════════════════════════════════════════════════
# 5. Les trois messages que l'owner-draw ajoute
# ══════════════════════════════════════════════════════════════════════════

replace(
    "WM_DRAWITEM / WM_MEASUREITEM / WM_CTLCOLORLISTBOX",
    """            case Win32.WM_COMMAND:
            {
                int id = wParam.ToInt32() & 0xFFFF;""",
    """            case Win32.WM_DRAWITEM:
                if (TryDrawItem(lParam))
                    return (IntPtr)1;
                break;

            case Win32.WM_MEASUREITEM:
                if (TryMeasureItem(lParam))
                    return (IntPtr)1;
                break;

            case Win32.WM_CTLCOLORLISTBOX:
                // La liste peint ses lignes, pas la bande que Windows laisse sous la dernière.
                Win32.SetBkColor(wParam, CLR_PANEL_BG);
                return Theme.Brush(CLR_PANEL_BG);

            case Win32.WM_COMMAND:
            {
                int id = wParam.ToInt32() & 0xFFFF;""",
)

# ══════════════════════════════════════════════════════════════════════════
# 6. Survol, hauteur de ligne, cadre de la liste
# ══════════════════════════════════════════════════════════════════════════

replace(
    "attache du survol",
    """        CreateMainWindow();
        CreateControls();
        ApplyFontsToControls();""",
    """        CreateMainWindow();
        CreateControls();
        AttachHoverTracking();
        ApplyFontsToControls();""",
)

replace(
    "détache du survol",
    """        if (_hWndLinkReset != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndLinkReset, _linkSubclassProc, (UIntPtr)2);""",
    """        if (_hWndLinkReset != IntPtr.Zero)
            Win32.RemoveWindowSubclass(_hWndLinkReset, _linkSubclassProc, (UIntPtr)2);
        DetachHoverTracking();""",
)

replace(
    "hauteur de ligne à la bascule de DPI",
    """        Win32.SendMessageW(_hWndCompatList, Win32.WM_SETFONT, _hFontText, (IntPtr)1);""",
    """        Win32.SendMessageW(_hWndCompatList, Win32.WM_SETFONT, _hFontText, (IntPtr)1);
        // LBS_OWNERDRAWFIXED ne demande sa hauteur de ligne qu'une fois, à la création : à la
        // bascule d'échelle, c'est ici qu'elle se remet au DPI courant.
        if (_hWndCompatList != IntPtr.Zero)
            Win32.SendMessageW(_hWndCompatList, LB_SETITEMHEIGHT, IntPtr.Zero, (IntPtr)CompatItemHeight());""",
)

replace(
    "cadre de la liste",
    """        Win32.DrawTextW(hdc, L.Settings_SectionCompat, -1, ref titleRect, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);
    }""",
    """        Win32.DrawTextW(hdc, L.Settings_SectionCompat, -1, ref titleRect, Win32.DT_LEFT | Win32.DT_SINGLELINE | Win32.DT_NOPREFIX);

        // Le cadre de la liste, que WS_BORDER dessinait aux couleurs du système. Même primitive
        // que les champs de raccourci : une liste est un champ, du point de vue de la charte.
        ThemeControls.DrawFieldFrame(hdc, layout.CompatListRect, ControlState.None,
            Theme.Current, _dpi);
    }""",
)

# ══════════════════════════════════════════════════════════════════════════
# 7. Les cinq constantes que plus rien n'emploie
# ══════════════════════════════════════════════════════════════════════════

replace(
    "constantes mortes",
    """    private const uint BS_AUTOCHECKBOX = 0x0003;
    private const uint BS_AUTORADIOBUTTON = 0x0009;
    private const uint WS_GROUP = 0x00020000;
    private const uint BM_GETCHECK = 0x00F0;
    private const uint BM_SETCHECK = 0x00F1;
    private const uint BST_CHECKED = 0x0001;""",
    """    // BS_AUTOCHECKBOX, BS_AUTORADIOBUTTON, BM_GETCHECK, BM_SETCHECK et BST_CHECKED ont
    // disparu avec le passage en owner-draw : Windows ne tient plus l'état coché, et le
    // demander à un contrôle BS_OWNERDRAW rendrait toujours zéro. Voir SettingsWindow.Theme.cs.
    private const uint WS_GROUP = 0x00020000;""",
)

replace(
    "WS_BORDER mort",
    """    private const uint WS_VSCROLL = 0x00200000;
    private const uint WS_BORDER = 0x00800000;""",
    """    private const uint WS_VSCROLL = 0x00200000;""",
)

# ══════════════════════════════════════════════════════════════════════════

if data.count(b"\r\n"):
    sys.exit("des CRLF sont apparus — rien n'est écrit")
SETTINGS.write_bytes(data)
print(f"SettingsWindow.cs  {len(data)} octets, {data.count(b'\n')} LF")
