"""CH3 passe 2, lot B (4/4) — la charte du test accueille le septième rôle.

`ThemeTests.EchelleTypographique_EstCelleDeLaCharte` est rouge depuis l'ajout de
`FontRole.Display`, et c'est ce qu'on lui demande : sa première assertion compare la taille de
sa propre table au nombre de valeurs de l'énumération, précisément pour qu'un rôle ajouté sans
décision ne tombe pas en silence sur le repli de `Metrics`. Le même garde avait déjà attrapé
`BodyStrong` le 2026-08-29.

Ce patch inscrit la décision plutôt que de contourner le garde : `Display` 26/700 entre dans la
table, 26 entre dans la liste des tailles, et le commentaire qui annonçait « cinq tailles » pour
six en annonce sept.

ThemeTests.cs est en LF pur sans BOM.
"""

import pathlib
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = pathlib.Path(__file__).resolve().parents[3]
TESTS = ROOT / "src" / "AZERTYGlobal.Tests" / "ThemeTests.cs"

data = TESTS.read_bytes()
if data.count(b"\r\n") or data[:3] == b"\xef\xbb\xbf":
    sys.exit("ThemeTests.cs n'est plus en LF pur sans BOM")


def replace(name, old, new):
    global data
    b = old.encode("utf-8")
    if data.count(b) != 1:
        sys.exit(f"{name} : {data.count(b)} occurrences, attendu 1")
    data = data.replace(b, new.encode("utf-8"))
    print(f"  {name}")


replace(
    "Display dans la table",
    """            (FontRole.StatNumber, 28, 600, "Segoe UI"),
            (FontRole.Mono, 14, 400, "Consolas"),""",
    """            (FontRole.StatNumber, 28, 600, "Segoe UI"),
            (FontRole.Display, 26, 700, "Segoe UI"),
            (FontRole.Mono, 14, 400, "Consolas"),""",
)

replace(
    "la liste des tailles",
    """        // L'échelle des tailles ne bouge pas : BodyStrong reprend celle du corps et n'en change
        // que la graisse. Cinq tailles, celles qu'Antoine a arrêtées au chantier CH1.
        Assert.Equal(new[] { 13, 14, 15, 18, 24, 28 },
            charte.Select(c => c.Size).Distinct().OrderBy(s => s).ToArray());""",
    """        // Sept tailles : les six arrêtées par Antoine au chantier CH1 — BodyStrong reprenant
        // celle du corps et n'en changeant que la graisse — plus le 26 de Display, arrêté le
        // 2026-08-30 pour les deux titres de la fenêtre de bienvenue, que WindowTitle (24/600)
        // aplatissait. Le commentaire annonçait « cinq » pour six depuis l'origine.
        Assert.Equal(new[] { 13, 14, 15, 18, 24, 26, 28 },
            charte.Select(c => c.Size).Distinct().OrderBy(s => s).ToArray());""",
)

TESTS.write_bytes(data)
print(f"ThemeTests.cs  {len(data)} octets, {data.count(chr(10).encode())} LF")
