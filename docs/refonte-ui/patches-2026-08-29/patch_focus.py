# -*- coding: utf-8 -*-
"""L'anneau de focus manquait aux compteurs, qui sont pourtant WS_TABSTOP.

Les deux fichiers sont en LF pur (verifie avant ecriture).
"""
import sys
from pathlib import Path

SRC = Path(r"D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store\src")

PATCHES = [
    ("ThemeControls.cs",
     '''        var stem = new Win32.RECT
        {
            left = centerX - thickness / 2,
            top = centerY - arm,
            right = centerX - thickness / 2 + thickness,
            bottom = centerY + arm,
        };
        Win32.FillRect(hdc, ref stem, ink);
    }''',
     '''        var stem = new Win32.RECT
        {
            left = centerX - thickness / 2,
            top = centerY - arm,
            right = centerX - thickness / 2 + thickness,
            bottom = centerY + arm,
        };
        Win32.FillRect(hdc, ref stem, ink);
    }'''),

    ("ThemeControls.cs",
     '''    internal static void DrawStepperButton(IntPtr hdc, Win32.RECT rect, ControlState state,
        Palette palette, int dpi, bool adding)
    {
        var paint = ButtonPaint(ButtonKind.Secondary, state, palette);
        DrawRoundedBox(hdc, rect, paint, Scale(BaseRadius, dpi), dpi);
''',
     '''    internal static void DrawStepperButton(IntPtr hdc, Win32.RECT rect, ControlState state,
        Palette palette, int dpi, bool adding)
    {
        var paint = ButtonPaint(ButtonKind.Secondary, state, palette);
        DrawRoundedBox(hdc, rect, paint, Scale(BaseRadius, dpi), dpi);

        // Un compteur est WS_TABSTOP : sans anneau, on tabule dessus sans voir où l'on est.
        // Les flèches empilées qu'il remplace n'en portaient pas non plus.
        if (state.HasFlag(ControlState.Focused) && !state.HasFlag(ControlState.Disabled))
            DrawFocusRing(hdc, rect, palette, dpi);
'''),

    ("PauseDurationDialog.Theme.cs",
     '''    private const int MinusX = 108;
    private const int StepW = 28;
    private const int FieldX = 140;
    private const int FieldW = 64;
    private const int FieldH = 28;
    private const int PlusX = 208;''',
     '''    private const int MinusX = 104;
    private const int StepW = 28;
    private const int FieldX = 140;
    private const int FieldW = 64;
    private const int FieldH = 28;
    private const int PlusX = 212;'''),

    ("PauseDurationDialog.Theme.cs",
     '''        Move(_hHoursMinus, MinusX, Row1Y, StepW, FieldH);
        Move(_hHoursPlus, PlusX, Row1Y, StepW, FieldH);''',
     '''        MoveButton(_hHoursMinus, MinusX, Row1Y, StepW, FieldH, focus);
        MoveButton(_hHoursPlus, PlusX, Row1Y, StepW, FieldH, focus);'''),

    ("PauseDurationDialog.Theme.cs",
     '''        Move(_hMinutesMinus, MinusX, Row2Y, StepW, FieldH);
        Move(_hMinutesPlus, PlusX, Row2Y, StepW, FieldH);''',
     '''        MoveButton(_hMinutesMinus, MinusX, Row2Y, StepW, FieldH, focus);
        MoveButton(_hMinutesPlus, PlusX, Row2Y, StepW, FieldH, focus);'''),

    # La planche reserve desormais la marge de l'anneau autour de chaque compteur.
    ("AZERTYGlobal.Tests/StatesBoard.cs",
     '''    private static Win32.RECT Square(Win32.RECT cell, int index) => new()
    {
        left = cell.left + index * 36,
        top = cell.top,
        right = cell.left + index * 36 + 28,
        bottom = cell.top + 28,
    };''',
     '''    private static Win32.RECT Square(Win32.RECT cell, int index) => new()
    {
        left = cell.left + 6 + index * 44,
        top = cell.top,
        right = cell.left + 6 + index * 44 + 28,
        bottom = cell.top + 28,
    };'''),
]


def main():
    files = {}
    for rel, old, _new in PATCHES:
        if rel not in files:
            data = (SRC / rel).read_bytes()
            if data.count(b"\r\n"):
                print(f"REFUS {rel} : CRLF present")
                return 1
            files[rel] = data.decode("utf-8")
        if files[rel].count(old) != 1:
            print(f"REFUS {rel} : ancre trouvee {files[rel].count(old)} fois")
            print(f"        {old.splitlines()[0][:95]!r}")
            return 1

    for rel, old, new in PATCHES:
        files[rel] = files[rel].replace(old, new, 1)

    for rel, text in files.items():
        out = text.encode("utf-8")
        assert b"\r\n" not in out, rel
        (SRC / rel).write_bytes(out)
        print(f"ecrit {rel} : {len(out)} octets, 0 CRLF")
    return 0


if __name__ == "__main__":
    sys.exit(main())
