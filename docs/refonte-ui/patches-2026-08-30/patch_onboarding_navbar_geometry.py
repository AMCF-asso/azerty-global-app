"""Reste 1 de CH3, suite — la barre de navigation garde la géométrie qu'elle avait.

`patch_onboarding_ownerdraw.py` a réservé la marge de focus en agrandissant les contrôles, le
motif de Paramètres. Il suffit à une case posée dans un panneau et empilée entre ses semblables.
Il ne suffit pas ici : les trois boutons de cette fenêtre sont **alignés sur les marges de la
fenêtre elle-même**, et grandir sans se décaler déplace ce que l'œil voit.

Mesuré en unités 96 DPI sur la géométrie que le premier patch produisait :

  - « Suivant » alignait son bord peint à `winW - 28 - 4`, soit **4 px en deçà** de la marge de
    28 px sur laquelle sont alignés l'en-tête, les panneaux et les titres ;
  - l'écart voulu de **12 px** entre « Essayer maintenant » et « Suivant » devenait **20**, les
    deux marges de focus s'ajoutant entre eux ;
  - même décalage de 4 px vers le bas sur les trois, la ligne de base de la barre étant fixe.

`MoveButton` prend désormais la géométrie **dessinée** et pose le contrôle autour : décalage de
la marge de focus sur les quatre côtés, largeur et hauteur augmentées du double. Le rendu
retrouve exactement les positions d'avant le chantier, et l'anneau de focus a sa place.

`ButtonRowWidth` rend de son côté la largeur dessinée, plus la marge : c'est `MoveButton` qui
l'ajoute, et un seul des deux doit le faire.

Un piège de séquence corrigé au passage : « Précédent » n'était placé que par
`RepositionControls`, appelée au changement de DPI et à l'étape 3 seulement. À l'étape 2 il
s'affichait donc à sa géométrie de création. Il est désormais placé dans
`UpdateStepVisibility`, juste avant d'être montré — le seul endroit qui court à chaque étape.
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

LF_CONSUMED = 0
LF_EMITTED = 0


def replace(name, old, new, expected=1):
    """Même mécanique que le patch précédent : l'ancre ignore la terminaison de chaque ligne, et
    la comptabilité des sauts isolés est vérifiée avant écriture."""
    global data, LF_CONSUMED, LF_EMITTED

    lines = old.replace("\r\n", "\n").split("\n")
    rx = re.compile(r"\r?\n".join(re.escape(line) for line in lines))
    text = data.decode("utf-8")
    matches = list(rx.finditer(text))
    if len(matches) != expected:
        sys.exit(f"{name} : ancre attendue {expected} fois, trouvée {len(matches)}\n---\n{old}\n---")

    out = []
    cursor = 0
    for m in matches:
        body = m.group(0)
        eol = "\r\n" if "\r\n" in body else "\n"
        LF_CONSUMED += body.count("\n") - body.count("\r\n")
        if eol == "\n":
            LF_EMITTED += new.replace("\r\n", "\n").count("\n")
        out.append(text[cursor:m.start()])
        out.append(new.replace("\r\n", "\n").replace("\n", eol))
        cursor = m.end()
    out.append(text[cursor:])
    data = "".join(out).encode("utf-8")
    print(f"  {name:44s} {expected}×")


# ── Les quatre placements de la barre ───────────────────────────────────────
replace(
    "Essayer, etat A",
    """            Win32.MoveWindow(_hWndBtnTry, tryX, btnBottomY, tryWidth, ButtonRowHeight(), true);""",
    """            MoveButton(_hWndBtnTry, tryX, btnBottomY, tryWidth);""",
    expected=2,
)

replace(
    "Suivant, etat B",
    """            Win32.MoveWindow(_hWndBtnNext, nextGeomB.x, btnBottomY, nextGeomB.width, ButtonRowHeight(), true);""",
    """            MoveButton(_hWndBtnNext, nextGeomB.x, btnBottomY, nextGeomB.width);""",
)

replace(
    "Suivant, etat C",
    """            Win32.MoveWindow(_hWndBtnNext, nextGeomC.x, btnBottomY, nextGeomC.width, ButtonRowHeight(), true);""",
    """            MoveButton(_hWndBtnNext, nextGeomC.x, btnBottomY, nextGeomC.width);""",
)

replace(
    "Precedent, dans RepositionControls",
    """        Win32.MoveWindow(_hWndBtnPrev, margin, bottomY,
            ButtonRowWidth(L.Onboarding_Prev, BASE_BTN_W_PREV), ButtonRowHeight(), true);""",
    """        MoveButton(_hWndBtnPrev, margin, bottomY, ButtonRowWidth(L.Onboarding_Prev, BASE_BTN_W_PREV));""",
)

# ── Precedent est place a chaque etape, pas seulement a la troisieme ────────
replace(
    "Precedent place avant d'etre montre",
    """        Win32.ShowWindow(_hWndBtnPrev, _currentStep > 0 ? 1 : 0);""",
    """        // Place avant d'etre montre : RepositionControls, qui portait seule ce placement, ne
        // court qu'au changement de DPI et a l'etape 3. A l'etape 2, « Precedent » s'affichait
        // donc a sa geometrie de creation, et la largeur mesuree de son libelle n'y arrivait pas.
        MoveButton(_hWndBtnPrev, S(BASE_MARGIN), S(BASE_WIN_H) - S(BASE_BOTTOM_MARGIN),
            ButtonRowWidth(L.Onboarding_Prev, BASE_BTN_W_PREV));
        Win32.ShowWindow(_hWndBtnPrev, _currentStep > 0 ? 1 : 0);""",
)

# ── Verification ────────────────────────────────────────────────────────────
# Plus un seul placement direct : les trois boutons passent par MoveButton. ButtonRowHeight()
# survit aux trois CreateWindowExW, où la géométrie n'est que provisoire mais la hauteur juste.
if b"Win32.MoveWindow(_hWndBtn" in data:
    sys.exit("un bouton est encore placé par MoveWindow, sans la marge de focus")

crlf_after = data.count(b"\r\n")
lf_after = data.count(b"\n") - crlf_after
expected_lf = LF_BEFORE - LF_CONSUMED + LF_EMITTED
if lf_after != expected_lf:
    sys.exit(f"fins de ligne : {lf_after} LF isolés, {expected_lf} attendus "
             f"({LF_BEFORE} avant, {LF_CONSUMED} consommés, {LF_EMITTED} réémis)")

TARGET.write_bytes(data)
print(f"\n{TARGET.name} écrit — CRLF {crlf_after}, LF isolés {lf_after} (était {LF_BEFORE})")
