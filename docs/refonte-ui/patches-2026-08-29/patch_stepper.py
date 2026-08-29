# -*- coding: utf-8 -*-
"""Les fleches empilees deviennent un moins a gauche et un plus a droite du champ.

Mesure : chaque fleche fait SpinW x FieldH/2 = 26 x 14 px a 96 DPI, la moitie de la hauteur de
son champ, quand Windows demande 24 x 24 sous la souris. A pleine hauteur, un compteur de chaque
cote donne 28 x 28, soit quatre fois la cible.

ThemeControls.cs est en LF pur, PauseDurationDialog.cs melange 323 CRLF et 40 LF, son .Theme.cs
est en LF pur. Chaque fichier est mesure avant ecriture et ses fins de ligne recomptees apres.
"""
import sys
from pathlib import Path

SRC = Path(r"D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store\src")

# ── Renommages sans effet sur les fins de ligne ──────────────────────────────
RENAMES = [
    ("IDC_HOURS_UP", "IDC_HOURS_PLUS"),
    ("IDC_HOURS_DOWN", "IDC_HOURS_MINUS"),
    ("IDC_MINUTES_UP", "IDC_MINUTES_PLUS"),
    ("IDC_MINUTES_DOWN", "IDC_MINUTES_MINUS"),
    ("_hHoursUp", "_hHoursPlus"),
    ("_hHoursDown", "_hHoursMinus"),
    ("_hMinutesUp", "_hMinutesPlus"),
    ("_hMinutesDown", "_hMinutesMinus"),
]
RENAMED_FILES = ["PauseDurationDialog.cs", "PauseDurationDialog.Theme.cs"]

# ── Remplacements, appliques APRES les renommages ────────────────────────────
LF_PATCHES = [
    # ThemeControls : la primitive du compteur
    ("ThemeControls.cs",
     '''        var oldBrush = Win32.SelectObject(hdc, Theme.Brush(paint.Text));
        var oldPen = Win32.SelectObject(hdc, Theme.Pen(paint.Text, 1));
        Win32.Polygon(hdc, points, points.Length);
        Win32.SelectObject(hdc, oldPen);
        Win32.SelectObject(hdc, oldBrush);
    }
}''',
     '''        var oldBrush = Win32.SelectObject(hdc, Theme.Brush(paint.Text));
        var oldPen = Win32.SelectObject(hdc, Theme.Pen(paint.Text, 1));
        Win32.Polygon(hdc, points, points.Length);
        Win32.SelectObject(hdc, oldPen);
        Win32.SelectObject(hdc, oldBrush);
    }

    /// <summary>
    /// Compteur d'une valeur : un trait pour retrancher, une croix pour ajouter. Il remplace la
    /// paire de flèches empilées, qui ne pouvait pas dépasser la moitié de la hauteur de son
    /// champ — 26 × 14 px à 96 DPI, là où Windows demande 24 × 24 sous la souris. À pleine
    /// hauteur, de part et d'autre du champ, la même fenêtre offre quatre fois la cible, et
    /// « moins » et « plus » disent ce que le bouton fait d'une durée quand « haut » et « bas »
    /// décrivaient un déplacement dans une liste.
    /// </summary>
    internal static void DrawStepperButton(IntPtr hdc, Win32.RECT rect, ControlState state,
        Palette palette, int dpi, bool adding)
    {
        var paint = ButtonPaint(ButtonKind.Secondary, state, palette);
        DrawRoundedBox(hdc, rect, paint, Scale(BaseRadius, dpi), dpi);

        int arm = Math.Max(3, Math.Min(rect.right - rect.left, rect.bottom - rect.top) / 4);
        int centerX = rect.left + (rect.right - rect.left) / 2;
        int centerY = rect.top + (rect.bottom - rect.top) / 2;
        int thickness = Math.Max(1, Scale(2, dpi));
        IntPtr ink = Theme.Brush(paint.Text);

        var bar = new Win32.RECT
        {
            left = centerX - arm,
            top = centerY - thickness / 2,
            right = centerX + arm,
            bottom = centerY - thickness / 2 + thickness,
        };
        Win32.FillRect(hdc, ref bar, ink);

        if (!adding)
            return;

        var stem = new Win32.RECT
        {
            left = centerX - thickness / 2,
            top = centerY - arm,
            right = centerX - thickness / 2 + thickness,
            bottom = centerY + arm,
        };
        Win32.FillRect(hdc, ref stem, ink);
    }
}'''),

    # Theme.cs du dialogue : geometrie
    ("PauseDurationDialog.Theme.cs",
     '''    private const int LabelW = 76;
    private const int FieldX = 108;
    private const int FieldW = 64;
    private const int FieldH = 28;
    private const int SpinX = 176;
    private const int SpinW = 26;''',
     '''    private const int LabelW = 76;
    // Le compteur, le champ et le compteur se suivent sur l'échelle d'espacement : 4 px entre
    // chaque, et un bouton carré de la hauteur du champ de part et d'autre.
    private const int MinusX = 108;
    private const int StepW = 28;
    private const int FieldX = 140;
    private const int FieldW = 64;
    private const int FieldH = 28;
    private const int PlusX = 208;'''),

    ("PauseDurationDialog.Theme.cs",
     '''        Move(_hHoursPlus, SpinX, Row1Y, SpinW, FieldH / 2);
        Move(_hHoursMinus, SpinX, Row1Y + FieldH / 2, SpinW, FieldH / 2);''',
     '''        Move(_hHoursMinus, MinusX, Row1Y, StepW, FieldH);
        Move(_hHoursPlus, PlusX, Row1Y, StepW, FieldH);'''),

    ("PauseDurationDialog.Theme.cs",
     '''        Move(_hMinutesPlus, SpinX, Row2Y, SpinW, FieldH / 2);
        Move(_hMinutesMinus, SpinX, Row2Y + FieldH / 2, SpinW, FieldH / 2);''',
     '''        Move(_hMinutesMinus, MinusX, Row2Y, StepW, FieldH);
        Move(_hMinutesPlus, PlusX, Row2Y, StepW, FieldH);'''),

    ("PauseDurationDialog.Theme.cs",
     '''        if (dis.hwndItem == _hHoursPlus || dis.hwndItem == _hMinutesPlus)
        {
            ThemeControls.DrawSpinnerButton(dis.hDC, rect, state, Theme.Current, _dpi, pointingUp: true);
            return true;
        }

        if (dis.hwndItem == _hHoursMinus || dis.hwndItem == _hMinutesMinus)
        {
            ThemeControls.DrawSpinnerButton(dis.hDC, rect, state, Theme.Current, _dpi, pointingUp: false);
            return true;
        }''',
     '''        if (dis.hwndItem == _hHoursPlus || dis.hwndItem == _hMinutesPlus)
        {
            ThemeControls.DrawStepperButton(dis.hDC, rect, state, Theme.Current, _dpi, adding: true);
            return true;
        }

        if (dis.hwndItem == _hHoursMinus || dis.hwndItem == _hMinutesMinus)
        {
            ThemeControls.DrawStepperButton(dis.hDC, rect, state, Theme.Current, _dpi, adding: false);
            return true;
        }'''),

    # L'ordre du tableau suit desormais l'ordre visuel de la rangee.
    ("PauseDurationDialog.Theme.cs",
     "    private IntPtr[] Buttons => new[] { _hHoursPlus, _hHoursMinus, _hMinutesPlus, _hMinutesMinus, _hBtnOk, _hBtnCancel };",
     "    private IntPtr[] Buttons => new[] { _hHoursMinus, _hHoursPlus, _hMinutesMinus, _hMinutesPlus, _hBtnOk, _hBtnCancel };"),
]

# PauseDurationDialog.cs : region CreateControls, mesuree en CRLF
CRLF_PATCHES = [
    ("PauseDurationDialog.cs",
     '        _hEditHours = CreateEdit(hInstance, IDC_EDIT_HOURS, "0", 0, 0, 0, 0);\r\n'
     '        _hEditMinutes = CreateEdit(hInstance, IDC_EDIT_MINUTES, "5", 0, 0, 0, 0);\r\n'
     '        _hHoursPlus = CreateButton(hInstance, IDC_HOURS_PLUS, "▲", 0, 0, 0, 0, BS_PUSHBUTTON);\r\n'
     '        _hHoursMinus = CreateButton(hInstance, IDC_HOURS_MINUS, "▼", 0, 0, 0, 0, BS_PUSHBUTTON);\r\n'
     '        _hMinutesPlus = CreateButton(hInstance, IDC_MINUTES_PLUS, "▲", 0, 0, 0, 0, BS_PUSHBUTTON);\r\n'
     '        _hMinutesMinus = CreateButton(hInstance, IDC_MINUTES_MINUS, "▼", 0, 0, 0, 0, BS_PUSHBUTTON);\r\n',

     '        // L\'ordre de création est l\'ordre de tabulation : chaque rangée se parcourt comme\r\n'
     '        // elle se lit, moins puis champ puis plus. Le texte des boutons n\'est jamais peint\r\n'
     '        // — ils sont owner-draw — mais il reste ce qu\'un lecteur d\'écran annonce.\r\n'
     '        _hHoursMinus = CreateButton(hInstance, IDC_HOURS_MINUS, "−", 0, 0, 0, 0, BS_PUSHBUTTON);\r\n'
     '        _hEditHours = CreateEdit(hInstance, IDC_EDIT_HOURS, "0", 0, 0, 0, 0);\r\n'
     '        _hHoursPlus = CreateButton(hInstance, IDC_HOURS_PLUS, "+", 0, 0, 0, 0, BS_PUSHBUTTON);\r\n'
     '\r\n'
     '        _hMinutesMinus = CreateButton(hInstance, IDC_MINUTES_MINUS, "−", 0, 0, 0, 0, BS_PUSHBUTTON);\r\n'
     '        _hEditMinutes = CreateEdit(hInstance, IDC_EDIT_MINUTES, "5", 0, 0, 0, 0);\r\n'
     '        _hMinutesPlus = CreateButton(hInstance, IDC_MINUTES_PLUS, "+", 0, 0, 0, 0, BS_PUSHBUTTON);\r\n'),
]


def main():
    # 1. Renommages
    for rel in RENAMED_FILES:
        path = SRC / rel
        data = path.read_bytes()
        crlf_before = data.count(b"\r\n")
        lf_before = data.count(b"\n") - crlf_before
        text = data.decode("utf-8")
        for old, new in RENAMES:
            text = text.replace(old, new)
        out = text.encode("utf-8")
        if out.count(b"\r\n") != crlf_before or out.count(b"\n") - out.count(b"\r\n") != lf_before:
            print(f"REFUS {rel} : renommage a deplace des fins de ligne")
            return 1
        path.write_bytes(out)
        print(f"renomme {rel} : CRLF={crlf_before}, LF seuls={lf_before} inchanges")

    # 2. Patches LF pur
    lf_files = {}
    for rel, old, _new in LF_PATCHES:
        if rel not in lf_files:
            data = (SRC / rel).read_bytes()
            if data.count(b"\r\n"):
                print(f"REFUS {rel} : CRLF present, ce lot n'ecrit que du LF")
                return 1
            lf_files[rel] = data.decode("utf-8")
        if lf_files[rel].count(old) != 1:
            print(f"REFUS {rel} : ancre trouvee {lf_files[rel].count(old)} fois")
            print(f"        {old.splitlines()[0][:95]!r}")
            return 1
    for rel, old, new in LF_PATCHES:
        lf_files[rel] = lf_files[rel].replace(old, new, 1)

    # 3. Patches CRLF
    mixed = {}
    for rel, old, new in CRLF_PATCHES:
        if rel not in mixed:
            mixed[rel] = (SRC / rel).read_bytes()
        if mixed[rel].count(old.encode("utf-8")) != 1:
            print(f"REFUS {rel} : ancre CRLF trouvee {mixed[rel].count(old.encode('utf-8'))} fois")
            return 1
        before = mixed[rel]
        crlf_before = before.count(b"\r\n")
        lf_before = before.count(b"\n") - crlf_before
        after = before.replace(old.encode("utf-8"), new.encode("utf-8"), 1)
        crlf_after = after.count(b"\r\n")
        lf_after = after.count(b"\n") - crlf_after
        if lf_after != lf_before or crlf_after != crlf_before + (new.count("\r\n") - old.count("\r\n")):
            print(f"REFUS {rel} : fins de ligne deplacees "
                  f"(CRLF {crlf_before}->{crlf_after}, LF {lf_before}->{lf_after})")
            return 1
        mixed[rel] = after

    for rel, text in lf_files.items():
        out = text.encode("utf-8")
        assert b"\r\n" not in out, rel
        (SRC / rel).write_bytes(out)
        print(f"ecrit {rel} : {len(out)} octets, 0 CRLF")
    for rel, out in mixed.items():
        (SRC / rel).write_bytes(out)
        crlf = out.count(b"\r\n")
        print(f"ecrit {rel} : {len(out)} octets, CRLF={crlf}, LF seuls={out.count(chr(10).encode()) - crlf}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
