"""CH3 passe 2, lot A2 — les déclarations Win32 que l'owner-draw d'une LISTBOX réclame.

⚠️ Win32.cs est **mixte** : 772 CRLF pour 270 LF, mesuré le 2026-08-30. La fin de ligne ne se
déduit donc ni du fichier, ni de la région — les deux régions visées ici comptent des deux
sortes à quelques lignes d'écart. Elle se déduit de la **terminaison de la ligne d'ancre
elle-même**, lue octet par octet, et le bloc inséré la reprend. Les deux comptes sont vérifiés
avant et après écriture, et le script refuse d'écrire s'il en a créé ou détruit.
"""

import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parents[3]
WIN32 = ROOT / "src" / "Win32.cs"


def insert_after_line(data, name, anchor, block_lines):
    """Insère `block_lines` après la ligne unique portant `anchor`, avec la fin de ligne de
    cette ligne-là."""
    anchor_b = anchor.encode("utf-8")
    found = data.count(anchor_b)
    if found != 1:
        sys.exit(f"{name} : ancre trouvée {found} fois, attendu 1")

    start = data.index(anchor_b)
    end = data.index(b"\n", start) + 1
    eol = b"\r\n" if data[end - 2 : end] == b"\r\n" else b"\n"
    print(f"  {name:20s} ancre terminée par {'CRLF' if eol == b'\r\n' else 'LF  '}")

    block = b"".join(line.encode("utf-8") + eol for line in block_lines)
    return data[:end] + block + data[end:]


data = WIN32.read_bytes()
crlf_before = data.count(b"\r\n")
lf_before = data.count(b"\n") - crlf_before

data = insert_after_line(
    data,
    "WM_MEASUREITEM",
    "    public const uint WM_DRAWITEM = 0x002B;",
    [
        "    public const uint WM_MEASUREITEM = 0x002C;",
        "    public const uint WM_CTLCOLORLISTBOX = 0x0134;",
    ],
)

data = insert_after_line(
    data,
    "ODT_*",
    "    public const uint ODS_FOCUS = 0x0010;",
    [
        "",
        "    // Type du contrôle qui demande sa peinture ou sa mesure, dans DRAWITEMSTRUCT.CtlType",
        "    // et MEASUREITEMSTRUCT.CtlType. Une fenêtre qui peint plusieurs sortes de contrôles",
        "    // doit trier dessus : le handle seul ne dit pas si l'appel vient d'un bouton ou d'une",
        "    // ligne de liste, et les deux n'ont pas le même contrat.",
        "    public const uint ODT_LISTBOX = 2;",
        "    public const uint ODT_BUTTON = 4;",
    ],
)

# MEASUREITEMSTRUCT : ajouté après la fermeture de DRAWITEMSTRUCT, dont la région est en CRLF
# pur (mesurée). Remplacement littéral plutôt qu'insertion après une ligne, la ligne de
# fermeture « } » n'étant unique nulle part dans ce fichier.
OLD_TAIL = (
    "        public RECT rcItem;\r\n"
    "        public IntPtr itemData;\r\n"
    "    }\r\n"
)
NEW_TAIL = (
    "        public RECT rcItem;\r\n"
    "        public IntPtr itemData;\r\n"
    "    }\r\n"
    "\r\n"
    "    /// <summary>\r\n"
    "    /// Hauteur d'une ligne owner-draw. Une LISTBOX LBS_OWNERDRAWFIXED ne pose la question\r\n"
    "    /// qu'une fois, à sa création : sur changement d'échelle la réponse est périmée, et\r\n"
    "    /// c'est LB_SETITEMHEIGHT qui la remet à jour.\r\n"
    "    /// </summary>\r\n"
    "    [StructLayout(LayoutKind.Sequential)]\r\n"
    "    public struct MEASUREITEMSTRUCT\r\n"
    "    {\r\n"
    "        public uint CtlType;\r\n"
    "        public uint CtlID;\r\n"
    "        public uint itemID;\r\n"
    "        public uint itemWidth;\r\n"
    "        public uint itemHeight;\r\n"
    "        public IntPtr itemData;\r\n"
    "    }\r\n"
)

old_b = OLD_TAIL.encode("utf-8")
found = data.count(old_b)
if found != 1:
    sys.exit(f"MEASUREITEMSTRUCT : queue de DRAWITEMSTRUCT trouvée {found} fois, attendu 1")
data = data.replace(old_b, NEW_TAIL.encode("utf-8"))
print("  MEASUREITEMSTRUCT    queue de DRAWITEMSTRUCT en CRLF pur")

crlf_after = data.count(b"\r\n")
lf_after = data.count(b"\n") - crlf_after
added_lf = lf_after - lf_before
if added_lf != 0:
    sys.exit(f"{added_lf} ligne(s) LF créée(s) dans un bloc annoncé CRLF — rien n'est écrit")

WIN32.write_bytes(data)
print(f"Win32.cs  CRLF {crlf_before} → {crlf_after}   LF {lf_before} → {lf_after}")
