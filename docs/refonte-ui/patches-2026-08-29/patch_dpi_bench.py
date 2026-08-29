# -*- coding: utf-8 -*-
"""Les deux fenetres de CH2 qui lisaient leur DPI en direct passent par ThemeWindow.DpiOf.

Mesure du 2026-08-29 : le banc de captures rendait `conflit-*` en 576x479 et `statistiques-*`
en 476x499 aux trois echelles, parce que `Win32.GetDpiForWindow` ignore l'override que
`ThemeWindow.OverrideDpiForTests` installe. Les douze fichiers de la matrice n'etaient donc
que quatre rendus distincts. `AboutWindow` tenait deja le bon motif : c'est lui qui est repris.

Patch binaire, comme l'exige le depot : ancre en octets, une seule occurrence verifiee avant
la moindre ecriture, terminaisons de la region relues apres coup.
"""

import io
import os
import sys

SRC = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "src")

REPLACEMENTS = [
    (
        "LayoutConflictWindow.cs",
        "            int realDpi = Win32.GetDpiForWindow(_hWnd);\n",
        "            // Le DPI passe par ThemeWindow : seul ce point honore l'override du banc\n"
        "            // de captures. Lu en direct, la fenêtre rendait toujours à l'échelle du\n"
        "            // poste, et ses six cellules n'étaient qu'un rendu répété trois fois.\n"
        "            int realDpi = ThemeWindow.DpiOf(_hWnd);\n",
    ),
    (
        "UsageStatsWindow.cs",
        "            int realDpi = Win32.GetDpiForWindow(_hWnd);\n",
        "            // Le DPI passe par ThemeWindow : seul ce point honore l'override du banc\n"
        "            // de captures. Lu en direct, la fenêtre rendait toujours à l'échelle du\n"
        "            // poste, et ses six cellules n'étaient qu'un rendu répété trois fois.\n"
        "            int realDpi = ThemeWindow.DpiOf(_hWnd);\n",
    ),
    (
        "UsageStatsWindow.cs",
        "        catch { /* GetDpiForWindow non disponible (Windows 8.1-) */ }\n",
        "        catch { /* DpiOf absorbe déjà l'échec ; le filet reste par prudence */ }\n",
    ),
]


def region_endings(data, offset, span=400):
    """Terminaisons de la seule region de l'ancre, jamais du fichier entier."""
    start = max(0, offset - span)
    chunk = data[start : offset + span]
    crlf = chunk.count(b"\r\n")
    lf = chunk.count(b"\n") - crlf
    return crlf, lf


def apply(path, anchor, replacement):
    with open(path, "rb") as handle:
        data = handle.read()

    if data[:3] == b"\xef\xbb\xbf":
        sys.exit("%s : BOM inattendu, patch refuse" % path)

    anchor_bytes = anchor.encode("utf-8")
    count = data.count(anchor_bytes)
    if count != 1:
        sys.exit("%s : ancre trouvee %d fois, attendu 1" % (path, count))

    offset = data.index(anchor_bytes)
    crlf, lf = region_endings(data, offset)
    if crlf:
        sys.exit("%s : %d CRLF dans la region de l'ancre, lot annonce LF pur" % (path, crlf))

    patched = data.replace(anchor_bytes, replacement.encode("utf-8"))

    with open(path, "wb") as handle:
        handle.write(patched)

    after_crlf, after_lf = region_endings(patched, offset)
    print(
        "%-26s ancre 1/1, region LF avant %d apres %d, CRLF apres %d, %+d octets"
        % (os.path.basename(path), lf, after_lf, after_crlf, len(patched) - len(data))
    )


def main():
    for name, anchor, replacement in REPLACEMENTS:
        apply(os.path.normpath(os.path.join(SRC, name)), anchor, replacement)


if __name__ == "__main__":
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
    main()
