"""Réserve la place de la pastille de rang dans le caractère principal d'une touche.

Défaut mesuré le 2026-08-31 sur `touches-surlignage-sombre.png` : la pastille de la table
candidate C est dessinée en haut à gauche de la touche, et le caractère principal est centré
dans la touche entière — les deux se recouvrent. La table C paraissait donc plus mauvaise
qu'elle ne l'est, ce qui faussait l'arbitrage qu'elle sert.

Patch en octets, ancre comptée, fins de ligne préservées : convention du dépôt pour tout `.cs`.
"""

import pathlib
import sys

sys.stdout.reconfigure(encoding="utf-8")

RACINE = pathlib.Path(__file__).resolve().parents[3]

REMPLACEMENTS = [
    (
        RACINE / "src" / "KeyboardTheme.cs",
        [
            (
                "    internal static void DrawKeyCap(IntPtr hdc, Win32.RECT rect, KeyPaint paint,\n"
                "        string label, string? subLabel, IntPtr labelFont, IntPtr subFont, int dpi)\n",
                "    internal static void DrawKeyCap(IntPtr hdc, Win32.RECT rect, KeyPaint paint,\n"
                "        string label, string? subLabel, IntPtr labelFont, IntPtr subFont, int dpi,\n"
                "        int labelLeftInset = 0)\n",
            ),
            (
                "        var main = rect;\n"
                "        if (!string.IsNullOrEmpty(subLabel))\n"
                "            main.bottom -= (rect.bottom - rect.top) / 3;\n",
                "        var main = rect;\n"
                "        if (!string.IsNullOrEmpty(subLabel))\n"
                "            main.bottom -= (rect.bottom - rect.top) / 3;\n"
                "        // La pastille de rang occupe le coin haut gauche : le caractère se centre\n"
                "        // dans ce qui reste, sinon les deux se superposent.\n"
                "        main.left += labelLeftInset;\n",
            ),
            (
                "    internal static void DrawRankBadge(IntPtr hdc, Win32.RECT rect, int rank, Palette p,\n"
                "        IntPtr font, int dpi)\n    {\n        if (rank <= 0)\n            return;\n\n"
                "        int size = ThemeControls.Scale(16, dpi);\n",
                "    internal static void DrawRankBadge(IntPtr hdc, Win32.RECT rect, int rank, Palette p,\n"
                "        IntPtr font, int dpi)\n    {\n        if (rank <= 0)\n            return;\n\n"
                "        int size = BadgeSize(dpi);\n",
            ),
            (
                "    /// <summary>Rang à écrire dans le badge, ou 0 quand le surlignage n'en a pas.</summary>\n",
                "    /// <summary>\n"
                "    /// Largeur que la pastille de rang retire au caractère principal, marge comprise.\n"
                "    /// Une seule table l'emploie, mais la mesure vaut pour toutes : c'est elle que\n"
                "    /// <c>DrawKeyCap</c> reçoit en <c>labelLeftInset</c>.\n"
                "    /// </summary>\n"
                "    internal static int BadgeSize(int dpi) => ThemeControls.Scale(16, dpi);\n\n"
                "    /// <summary>Rang à écrire dans le badge, ou 0 quand le surlignage n'en a pas.</summary>\n",
            ),
        ],
    ),
    (
        RACINE / "src" / "AZERTYGlobal.Tests" / "KeyboardStatesBoard.cs",
        [
            (
                "                    var rect = KeyRect(c + 1, r + 1);\n"
                "                    KeyboardTheme.DrawKeyCap(hdc, rect, paint, \"E\", \"€\", mono, caption, Dpi);\n"
                "                    if (KeyboardTheme.ShowsRankBadge(Schemes[r].Scheme))\n"
                "                        KeyboardTheme.DrawRankBadge(hdc, rect, KeyboardTheme.RankOf(highlight),\n"
                "                            palette, caption, Dpi);\n",
                "                    var rect = KeyRect(c + 1, r + 1);\n"
                "                    bool badge = KeyboardTheme.ShowsRankBadge(Schemes[r].Scheme)\n"
                "                        && KeyboardTheme.RankOf(highlight) > 0;\n"
                "                    KeyboardTheme.DrawKeyCap(hdc, rect, paint, \"E\", \"€\", mono, caption, Dpi,\n"
                "                        badge ? KeyboardTheme.BadgeSize(Dpi) : 0);\n"
                "                    if (badge)\n"
                "                        KeyboardTheme.DrawRankBadge(hdc, rect, KeyboardTheme.RankOf(highlight),\n"
                "                            palette, caption, Dpi);\n",
            ),
        ],
    ),
]


def patch(path, paires):
    brut = path.read_bytes()
    crlf = brut.count(b"\r\n")
    lf = brut.count(b"\n") - crlf
    if crlf:
        sys.exit(f"{path.name} : {crlf} CRLF, ce patch n'écrit qu'en LF pur")

    texte = brut.decode("utf-8")
    for avant, apres in paires:
        n = texte.count(avant)
        if n != 1:
            sys.exit(f"{path.name} : ancre trouvée {n} fois, attendu 1\n---\n{avant}")
        texte = texte.replace(avant, apres)

    path.write_bytes(texte.encode("utf-8"))
    print(f"{path.name} : {len(paires)} ancre(s), {lf} LF preserves")


for chemin, paires in REMPLACEMENTS:
    patch(chemin, paires)
