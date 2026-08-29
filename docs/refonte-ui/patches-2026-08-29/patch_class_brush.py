# -*- coding: utf-8 -*-
"""Patch : la brosse de fond de classe ne vient plus du cache partage de Theme.

Mesure du 2026-08-29 : SetClassLongPtrW(GCLP_HBRBACKGROUND, Theme.Brush(...)) confie la brosse
au systeme, qui la detruit au UnregisterClassW du Dispose. GetObjectType passe de 2 a 0 et le
cache continue de servir le meme handle mort aux fenetres suivantes.

Les trois fichiers touches sont en LF pur (verifie ci-dessous avant toute ecriture) : le script
refuse d'ecrire des qu'il voit un CRLF, et n'insere que du LF.
"""
import sys
from pathlib import Path

SRC = Path(r"D:\My files\Keyboard Layouts\projects\azerty-global\components\microsoft-store\src")

PATCHES = [
    # ── ThemeWindow : registre des brosses posees ────────────────────────────
    ("ThemeWindow.cs",
     '    // ═══════════════════════════════════════════════════════════════\n'
     '    // Fond de classe\n'
     '    // ═══════════════════════════════════════════════════════════════\n',

     '    // ═══════════════════════════════════════════════════════════════\n'
     '    // Fond de classe\n'
     '    // ═══════════════════════════════════════════════════════════════\n'
     '\n'
     '    /// <summary>Brosses posées par <see cref="ApplyClassBackground"/>, par fenêtre. Ce sont\n'
     '    /// les seules que ce helper ait le droit de détruire : celle qu\'une fenêtre non migrée\n'
     '    /// inscrit elle-même à l\'enregistrement de sa classe lui appartient encore.</summary>\n'
     '    private static readonly Dictionary<IntPtr, IntPtr> ClassBrushes = new();\n'),

    # ── ThemeWindow : la doctrine que la mesure dement ───────────────────────
    ("ThemeWindow.cs",
     '    /// La brosse vient du cache de <see cref="Theme"/> : elle survit à la fenêtre, ce qui est\n'
     '    /// exactement ce que demande une brosse de classe, et il ne faut jamais la détruire.\n',

     '    /// La brosse est créée pour cette fenêtre et pour elle seule. Une brosse du cache de\n'
     '    /// <see cref="Theme"/> ne convient pas : posée en fond de classe elle appartient au\n'
     '    /// système, qui la détruit au désenregistrement de la classe. Le cache servirait alors un\n'
     '    /// handle mort à toutes les fenêtres suivantes, dont le fond resterait blanc et les\n'
     '    /// étiquettes grises — c\'est le défaut mesuré sur Durée de pause le 2026-08-29, où\n'
     '    /// GetObjectType tombe de 2 à 0 au Dispose de la fenêtre précédente.\n'),

    # ── ThemeWindow : le corps ───────────────────────────────────────────────
    ("ThemeWindow.cs",
     '        Win32.SetClassLongPtrW(hwnd, Win32.GCLP_HBRBACKGROUND, Theme.Brush(color));\n',

     '        IntPtr fresh = Win32.CreateSolidBrush(color);\n'
     '        Win32.SetClassLongPtrW(hwnd, Win32.GCLP_HBRBACKGROUND, fresh);\n'
     '\n'
     '        if (ClassBrushes.TryGetValue(hwnd, out var previous) && previous != IntPtr.Zero)\n'
     '            Win32.DeleteObject(previous);\n'
     '        ClassBrushes[hwnd] = fresh;\n'
     '\n'),

    # ── ThemeWindow : ForgetClassBackground ──────────────────────────────────
    ("ThemeWindow.cs",
     '            | Win32.RDW_UPDATENOW);\n'
     '    }\n',

     '            | Win32.RDW_UPDATENOW);\n'
     '    }\n'
     '\n'
     '    /// <summary>\n'
     '    /// Oublie la brosse d\'une fenêtre qui se détruit, sans la libérer : elle est celle de la\n'
     '    /// classe, et c\'est le système qui la détruit au désenregistrement. Sans cet oubli, une\n'
     '    /// fenêtre rouverte sur un handle recyclé verrait ApplyClassBackground appeler\n'
     '    /// DeleteObject sur une brosse déjà morte.\n'
     '    /// </summary>\n'
     '    internal static void ForgetClassBackground(IntPtr hwnd)\n'
     '    {\n'
     '        ClassBrushes.Remove(hwnd);\n'
     '    }\n'),

    # ── AboutWindow : ne plus inscrire la brosse partagee a l'enregistrement ─
    ("AboutWindow.cs",
     '            hbrBackground = BgBrush,\n',

     '            // hbrBackground = IntPtr.Zero : une brosse inscrite ici appartient au système,\n'
     '            // qui la détruit au UnregisterClassW du Dispose. BgBrush vient du cache de Theme,\n'
     '            // que toutes les autres fenêtres partagent — il ne doit pas y passer. Le fond est\n'
     '            // effacé côté instance (WM_ERASEBKGND puis OnPaint), et ApplyClassBackground pose\n'
     '            // une brosse dédiée.\n'
     '            hbrBackground = IntPtr.Zero,\n'),

    # ── AboutWindow : oubli au Dispose ───────────────────────────────────────
    ("AboutWindow.cs",
     '        Theme.Changed -= _themeChanged;\n'
     '        if (_hWnd != IntPtr.Zero)\n'
     '        {\n'
     '            Win32.DestroyWindow(_hWnd);\n',

     '        Theme.Changed -= _themeChanged;\n'
     '        if (_hWnd != IntPtr.Zero)\n'
     '        {\n'
     '            ThemeWindow.ForgetClassBackground(_hWnd);\n'
     '            Win32.DestroyWindow(_hWnd);\n'),

]

# PauseDurationDialog.cs melange 322 CRLF et 40 LF : il est traite a part, en octets, avec une
# ancre en CRLF mesuree dans sa seule region du Dispose (22 CRLF, 5 LF ailleurs dans la region).
CRLF_PATCHES = [
    ("PauseDurationDialog.cs",
     '        if (_hWnd != IntPtr.Zero)\r\n'
     '        {\r\n'
     '            Win32.DestroyWindow(_hWnd);\r\n'
     '            _hWnd = IntPtr.Zero;\r\n'
     '        }\r\n'
     '        DisposeTheme();\r\n',

     '        if (_hWnd != IntPtr.Zero)\r\n'
     '        {\r\n'
     '            ThemeWindow.ForgetClassBackground(_hWnd);\r\n'
     '            Win32.DestroyWindow(_hWnd);\r\n'
     '            _hWnd = IntPtr.Zero;\r\n'
     '        }\r\n'
     '        DisposeTheme();\r\n'),
]


def main():
    files = {}
    for rel, _old, _new in PATCHES:
        if rel in files:
            continue
        data = (SRC / rel).read_bytes()
        crlf = data.count(b"\r\n")
        if crlf:
            print(f"REFUS {rel} : {crlf} CRLF — ce script n'ecrit que du LF pur")
            return 1
        files[rel] = data.decode("utf-8")

    ok = True
    for rel, old, _new in PATCHES:
        found = files[rel].count(old)
        if found != 1:
            print(f"REFUS {rel} : ancre trouvee {found} fois au lieu de 1")
            print(f"        {old.splitlines()[0][:95]!r}")
            ok = False
    if not ok:
        return 1

    for rel, old, new in PATCHES:
        files[rel] = files[rel].replace(old, new, 1)

    # Fichiers a fins de ligne melangees : patch en octets, ancre en CRLF.
    mixed = {}
    for rel, old, _new in CRLF_PATCHES:
        if rel not in mixed:
            mixed[rel] = (SRC / rel).read_bytes()
        found = mixed[rel].count(old.encode("utf-8"))
        if found != 1:
            print(f"REFUS {rel} : ancre CRLF trouvee {found} fois au lieu de 1")
            return 1

    for rel, old, new in CRLF_PATCHES:
        before = mixed[rel]
        crlf_before = before.count(b"\r\n")
        lf_before = before.count(b"\n") - crlf_before
        after = before.replace(old.encode("utf-8"), new.encode("utf-8"), 1)
        crlf_after = after.count(b"\r\n")
        lf_after = after.count(b"\n") - crlf_after
        added = new.count("\r\n") - old.count("\r\n")
        if lf_after != lf_before or crlf_after != crlf_before + added:
            print(f"REFUS {rel} : fins de ligne deplacees "
                  f"(CRLF {crlf_before}->{crlf_after}, LF {lf_before}->{lf_after})")
            return 1
        mixed[rel] = after

    for rel, text in files.items():
        out = text.encode("utf-8")
        assert b"\r\n" not in out, rel
        (SRC / rel).write_bytes(out)
        print(f"ecrit {rel} : {len(out)} octets, {out.count(chr(10).encode())} LF, 0 CRLF")

    for rel, out in mixed.items():
        (SRC / rel).write_bytes(out)
        crlf = out.count(b"\r\n")
        print(f"ecrit {rel} : {len(out)} octets, CRLF={crlf}, LF seuls={out.count(chr(10).encode()) - crlf}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
