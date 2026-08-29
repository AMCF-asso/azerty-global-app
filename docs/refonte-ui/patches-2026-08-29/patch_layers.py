# -*- coding: utf-8 -*-
"""Migre MaintainableLayersWindow sur la charte. Fichier en LF pur (verifie)."""
import sys
from pathlib import Path

PATH = Path(r"D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store"
            r"\src\MaintainableLayersWindow.cs")

PATCHES = [
    # ── Declaration : classe partielle, plus de couleur ni de police locales ──
    ("internal sealed class MaintainableLayersWindow : IDisposable\n{\n"
     "    private const uint BS_AUTOCHECKBOX = 0x0003;\n"
     "    private const uint BS_DEFPUSHBUTTON = 0x0001;\n"
     "    private const uint BM_GETCHECK = 0x00F0;\n"
     "    private const uint BM_SETCHECK = 0x00F1;\n"
     "    private const uint BST_CHECKED = 1;\n"
     "    private const uint ES_NUMBER = 0x2000;\n"
     "    private const uint ES_CENTER = 0x0001;\n"
     "    // Même fond que SettingsWindow et UsageStatsWindow.\n"
     "    private const uint CLR_BG = 0x00DDDDDD;\n",

     "internal sealed partial class MaintainableLayersWindow : IDisposable\n{\n"
     "    private const uint ES_NUMBER = 0x2000;\n"
     "    private const uint ES_CENTER = 0x0001;\n"),

    # ── Champs ────────────────────────────────────────────────────────────────
    ("    private IntPtr _hDelay;\n"
     "    private IntPtr _hFont;\n"
     "    private IntPtr _hFontTitle;\n"
     "    private IntPtr _hBgBrush;\n"
     "    private bool _visible;\n",

     "    private IntPtr _hDelay;\n"
     "    private IntPtr _hTitle;\n"
     "    private IntPtr _hExplainer;\n"
     "    private IntPtr _hAvailable;\n"
     "    private IntPtr _hDelayLabel;\n"
     "    private IntPtr _hDelayUnit;\n"
     "    private bool _visible;\n"),

    # ── Creation de la fenetre ────────────────────────────────────────────────
    ('''            hCursor = Win32.LoadCursorW(IntPtr.Zero, (IntPtr)32512),
            // Sans brosse de classe, le fond n'est jamais effacé : au-dessus d'un jeu
            // plein écran, la fenêtre laissait voir la scène et ses contrôles en double
            // (constaté au smoke v1.2.0 du 2026-08-24, sous Trackmania).
            hbrBackground = _hBgBrush = Win32.CreateSolidBrush(CLR_BG),
            lpszClassName = ProductIdentity.WindowClass("MaintainableLayers")''',

     '''            hCursor = Win32.LoadCursorW(IntPtr.Zero, (IntPtr)32512),
            // hbrBackground = IntPtr.Zero : une brosse inscrite ici appartient au système, qui
            // la détruit au UnregisterClassW du Dispose — la détruire une seconde fois, comme
            // faisait ce Dispose, ou la prendre dans le cache partagé de Theme, laisse un handle
            // mort derrière soi. ApplyClassBackground pose une brosse dédiée, ce qui garde la
            // propriété qui comptait : au-dessus d'un jeu plein écran, le fond est bien effacé
            // et la scène ne transparaît plus (smoke v1.2.0 du 2026-08-24, sous Trackmania).
            hbrBackground = IntPtr.Zero,
            lpszClassName = ProductIdentity.WindowClass("MaintainableLayers")'''),

    ('''        const int clientW = 520;
        const int clientH = 408;
        uint style = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU;
        var rect = new Win32.RECT { left = 0, top = 0, right = clientW, bottom = clientH };''',

     '''        uint style = Win32.WS_OVERLAPPED | Win32.WS_CAPTION | Win32.WS_SYSMENU;
        var rect = new Win32.RECT { left = 0, top = 0, right = ClientW, bottom = ClientH };'''),

    ('''        Win32.EnableDarkTitleBar(_hWnd);

        _hFont = Win32.CreateFontW(-16, 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
        // -18 : le titre complet tient sur une seule ligne dans 470 px.
        _hFontTitle = Win32.CreateFontW(-18, 0, 0, 0, 700, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");

        CreateStatic(instance, L.Layers_Title, 24, 20, 470, 26, _hFontTitle);
        // Le second groupe de phrases wrappe sur deux lignes dans 470 px :
        // réserver trois lignes pleines pour ne rien tronquer.
        CreateStatic(instance, L.Layers_Explainer, 24, 56, 470, 66, _hFont);

        _hMaster = CreateCheckbox(instance, IDC_MASTER, L.Layers_MasterCheckbox, 24, 134, 455, 28);
        CreateStatic(instance, L.Layers_AvailableLabel, 45, 175, 250, 24, _hFont);
        _hGreek = CreateCheckbox(instance, IDC_GREEK, L.Layers_GreekCheckbox, 45, 202, 210, 26);
        _hCyrillic = CreateCheckbox(instance, IDC_CYRILLIC, L.Layers_CyrillicCheckbox, 270, 202, 220, 26);
        _hScientific = CreateCheckbox(instance, IDC_SCIENTIFIC, L.Layers_ScientificCheckbox, 45, 234, 260, 26);

        _hVisual = CreateCheckbox(instance, IDC_VISUAL, L.Layers_VisualCheckbox, 24, 282, 360, 26);
        CreateStatic(instance, L.Layers_DelayLabel, 24, 322, 185, 26, _hFont);
        _hDelay = Win32.CreateWindowExW(0, "EDIT", "500",
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_BORDER | Win32.WS_TABSTOP | ES_NUMBER | ES_CENTER,
            210, 317, 70, 28, _hWnd, (IntPtr)IDC_DELAY, instance, IntPtr.Zero);
        SetFont(_hDelay, _hFont);
        CreateStatic(instance, L.Layers_DelayUnit, 290, 322, 145, 26, _hFont);

        IntPtr save = Win32.CreateWindowExW(0, "BUTTON", L.Layers_SaveButton,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | BS_DEFPUSHBUTTON,
            350, 357, 140, 34, _hWnd, (IntPtr)IDC_SAVE, instance, IntPtr.Zero);
        SetFont(save, _hFont);
    }

    private IntPtr CreateCheckbox(IntPtr instance, int id, string text, int x, int y, int w, int h)
    {
        IntPtr control = Win32.CreateWindowExW(0, "BUTTON", text,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | BS_AUTOCHECKBOX,
            x, y, w, h, _hWnd, (IntPtr)id, instance, IntPtr.Zero);
        SetFont(control, _hFont);
        return control;
    }

    private void CreateStatic(IntPtr instance, string text, int x, int y, int w, int h, IntPtr font)
    {
        IntPtr control = Win32.CreateWindowExW(0, "STATIC", text,
            Win32.WS_CHILD | Win32.WS_VISIBLE,
            x, y, w, h, _hWnd, IntPtr.Zero, instance, IntPtr.Zero);
        SetFont(control, font);
    }

    private static void SetFont(IntPtr control, IntPtr font) =>
        Win32.SendMessageW(control, Win32.WM_SETFONT, font, (IntPtr)1);
''',

     '''        AdoptWindowDpi();

        // L'ordre de création est l'ordre de tabulation : la fenêtre se parcourt comme elle se
        // lit. La mise en page vient après, par LayoutControls, à l'échelle réellement adoptée.
        _hTitle = CreateStatic(instance, L.Layers_Title);
        _hExplainer = CreateStatic(instance, L.Layers_Explainer);
        _hMaster = CreateCheckbox(instance, IDC_MASTER, L.Layers_MasterCheckbox);
        _hAvailable = CreateStatic(instance, L.Layers_AvailableLabel);
        _hGreek = CreateCheckbox(instance, IDC_GREEK, L.Layers_GreekCheckbox);
        _hCyrillic = CreateCheckbox(instance, IDC_CYRILLIC, L.Layers_CyrillicCheckbox);
        _hScientific = CreateCheckbox(instance, IDC_SCIENTIFIC, L.Layers_ScientificCheckbox);
        _hVisual = CreateCheckbox(instance, IDC_VISUAL, L.Layers_VisualCheckbox);

        _hDelayLabel = CreateStatic(instance, L.Layers_DelayLabel);
        // Le champ perd WS_BORDER : la bordure gravée du système est ce que la refonte supprime,
        // et c'est le parent qui dessine celle de la charte.
        _hDelay = Win32.CreateWindowExW(0, "EDIT", "500",
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | ES_NUMBER | ES_CENTER,
            0, 0, 0, 0, _hWnd, (IntPtr)IDC_DELAY, instance, IntPtr.Zero);
        _hDelayUnit = CreateStatic(instance, L.Layers_DelayUnit);

        _hSave = Win32.CreateWindowExW(0, "BUTTON", L.Layers_SaveButton,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | Win32.BS_OWNERDRAW,
            0, 0, 0, 0, _hWnd, (IntPtr)IDC_SAVE, instance, IntPtr.Zero);

        ApplyThemeToWindow();
    }

    private IntPtr CreateCheckbox(IntPtr instance, int id, string text)
    {
        _checked[id] = false;
        return Win32.CreateWindowExW(0, "BUTTON", text,
            Win32.WS_CHILD | Win32.WS_VISIBLE | Win32.WS_TABSTOP | Win32.BS_OWNERDRAW,
            0, 0, 0, 0, _hWnd, (IntPtr)id, instance, IntPtr.Zero);
    }

    private IntPtr CreateStatic(IntPtr instance, string text) =>
        Win32.CreateWindowExW(0, "STATIC", text,
            Win32.WS_CHILD | Win32.WS_VISIBLE,
            0, 0, 0, 0, _hWnd, IntPtr.Zero, instance, IntPtr.Zero);
'''),

    # ── WndProc ──────────────────────────────────────────────────────────────
    ('''                case Win32.WM_COMMAND:
                    int id = wParam.ToInt32() & 0xFFFF;
                    if (id == IDC_MASTER)
                    {
                        UpdateEnabledState();
                        return IntPtr.Zero;
                    }
                    if (id == IDC_SAVE)
                    {
                        Hide();
                        return IntPtr.Zero;
                    }
                    break;''',

     '''                case Win32.WM_COMMAND:
                    int id = wParam.ToInt32() & 0xFFFF;
                    if (_checked.ContainsKey(id))
                    {
                        // Une case owner-draw n'est plus cochée par Windows : c'est ce clic qui
                        // la bascule, et le contrôle se repeint sur l'état que nous tenons.
                        _checked[id] = !_checked[id];
                        Win32.InvalidateRect(lParam, IntPtr.Zero, true);
                        if (id == IDC_MASTER)
                            UpdateEnabledState();
                        return IntPtr.Zero;
                    }
                    if (id == IDC_SAVE)
                    {
                        Hide();
                        return IntPtr.Zero;
                    }
                    break;'''),

    ('''                case Win32.WM_CTLCOLORSTATIC:
                case Win32.WM_CTLCOLORBTN:
                    // Statics et cases posent leur texte directement sur le fond de classe.
                    Win32.SetBkMode(wParam, 1);
                    return _hBgBrush;
                case Win32.WM_CLOSE:''',

     '''                case Win32.WM_PAINT:
                    return OnPaint();
                case Win32.WM_ERASEBKGND:
                    // Le fond est peint par WM_PAINT. Laisser Windows l'effacer d'abord ferait
                    // clignoter la fenêtre à chaque frappe dans le champ.
                    return (IntPtr)1;
                case Win32.WM_CTLCOLORSTATIC:
                    return OnCtlColorStatic(wParam, lParam);
                case Win32.WM_CTLCOLORBTN:
                    // Windows efface le fond d'un bouton owner-draw avec la brosse rendue ici,
                    // avant d'envoyer WM_DRAWITEM.
                    return OnCtlColorStatic(wParam, lParam);
                case Win32.WM_CTLCOLOREDIT:
                    return OnCtlColorEdit(wParam);
                case Win32.WM_DRAWITEM:
                    if (TryDrawItem(lParam))
                        return (IntPtr)1;
                    break;
                case Win32.WM_DPICHANGED:
                    OnDpiChanged(wParam, lParam);
                    return IntPtr.Zero;
                case Win32.WM_CLOSE:'''),

    # ── Etat des cases ───────────────────────────────────────────────────────
    ('''    private static bool IsChecked(IntPtr control) =>
        Win32.SendMessageW(control, BM_GETCHECK, IntPtr.Zero, IntPtr.Zero) == (IntPtr)BST_CHECKED;

    private static void SetChecked(IntPtr control, bool value) =>
        Win32.SendMessageW(control, BM_SETCHECK, value ? (IntPtr)BST_CHECKED : IntPtr.Zero, IntPtr.Zero);''',

     '''    private bool IsChecked(int id) => _checked.TryGetValue(id, out bool value) && value;

    private void SetChecked(int id, bool value)
    {
        _checked[id] = value;
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }'''),

    ('''        SetChecked(_hMaster, ConfigManager.MaintainableLayersEnabled);
        SetChecked(_hGreek, ConfigManager.MaintainableGreekEnabled);
        SetChecked(_hCyrillic, ConfigManager.MaintainableCyrillicEnabled);
        SetChecked(_hScientific, ConfigManager.MaintainableScientificEnabled);
        SetChecked(_hVisual, ConfigManager.MaintainableVisualFeedbackEnabled);''',

     '''        SetChecked(IDC_MASTER, ConfigManager.MaintainableLayersEnabled);
        SetChecked(IDC_GREEK, ConfigManager.MaintainableGreekEnabled);
        SetChecked(IDC_CYRILLIC, ConfigManager.MaintainableCyrillicEnabled);
        SetChecked(IDC_SCIENTIFIC, ConfigManager.MaintainableScientificEnabled);
        SetChecked(IDC_VISUAL, ConfigManager.MaintainableVisualFeedbackEnabled);'''),

    ('''        bool wasEnabled = ConfigManager.MaintainableLayersEnabled;
        bool enabled = IsChecked(_hMaster);
        int delay = ReadDelay();

        ConfigManager.SetMaintainableGreekEnabled(IsChecked(_hGreek));
        ConfigManager.SetMaintainableCyrillicEnabled(IsChecked(_hCyrillic));
        ConfigManager.SetMaintainableScientificEnabled(IsChecked(_hScientific));
        ConfigManager.SetMaintainableVisualFeedbackEnabled(IsChecked(_hVisual));''',

     '''        bool wasEnabled = ConfigManager.MaintainableLayersEnabled;
        bool enabled = IsChecked(IDC_MASTER);
        int delay = ReadDelay();

        ConfigManager.SetMaintainableGreekEnabled(IsChecked(IDC_GREEK));
        ConfigManager.SetMaintainableCyrillicEnabled(IsChecked(IDC_CYRILLIC));
        ConfigManager.SetMaintainableScientificEnabled(IsChecked(IDC_SCIENTIFIC));
        ConfigManager.SetMaintainableVisualFeedbackEnabled(IsChecked(IDC_VISUAL));'''),

    ('''    private void UpdateEnabledState()
    {
        bool enabled = IsChecked(_hMaster);
        Win32.EnableWindow(_hGreek, enabled);
        Win32.EnableWindow(_hCyrillic, enabled);
        Win32.EnableWindow(_hScientific, enabled);
        Win32.EnableWindow(_hVisual, enabled);
        Win32.EnableWindow(_hDelay, enabled);
    }''',

     '''    private void UpdateEnabledState()
    {
        bool enabled = IsChecked(IDC_MASTER);
        _secondaryEnabled = enabled;
        Win32.EnableWindow(_hGreek, enabled);
        Win32.EnableWindow(_hCyrillic, enabled);
        Win32.EnableWindow(_hScientific, enabled);
        Win32.EnableWindow(_hVisual, enabled);
        Win32.EnableWindow(_hDelay, enabled);
        Win32.InvalidateRect(_hWnd, IntPtr.Zero, true);
    }'''),

    # ── Dispose : plus de police ni de brosse a liberer ──────────────────────
    ('''    public void Dispose()
    {
        if (_hWnd != IntPtr.Zero)
        {
            Win32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }
        if (_hFont != IntPtr.Zero) Win32.DeleteObject(_hFont);
        if (_hFontTitle != IntPtr.Zero) Win32.DeleteObject(_hFontTitle);
        Win32.UnregisterClassW(ProductIdentity.WindowClass("MaintainableLayers"), Win32.GetModuleHandleW(null));
        if (_hBgBrush != IntPtr.Zero) Win32.DeleteObject(_hBgBrush);
    }''',

     '''    public void Dispose()
    {
        DisposeTheme();
        if (_hWnd != IntPtr.Zero)
        {
            ThemeWindow.ForgetClassBackground(_hWnd);
            Win32.DestroyWindow(_hWnd);
            _hWnd = IntPtr.Zero;
        }
        // Ni police ni brosse à libérer : les polices appartiennent au cache de Theme, et la
        // brosse de fond appartient au système, qui la détruit avec la classe.
        Win32.UnregisterClassW(ProductIdentity.WindowClass("MaintainableLayers"), Win32.GetModuleHandleW(null));
    }'''),
]


def main():
    data = PATH.read_bytes()
    if data.count(b"\r\n"):
        print(f"REFUS : {data.count(chr(13).encode() + chr(10).encode())} CRLF")
        return 1
    text = data.decode("utf-8")
    for old, _new in PATCHES:
        if text.count(old) != 1:
            print(f"REFUS : ancre trouvee {text.count(old)} fois")
            print(f"        {old.splitlines()[0][:95]!r}")
            return 1
    for old, new in PATCHES:
        text = text.replace(old, new, 1)
    out = text.encode("utf-8")
    assert b"\r\n" not in out
    PATH.write_bytes(out)
    print(f"ecrit : {len(out)} octets, 0 CRLF")
    return 0


if __name__ == "__main__":
    sys.exit(main())
