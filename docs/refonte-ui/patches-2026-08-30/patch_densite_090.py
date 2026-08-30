"""Les deux leviers passent à 0,90, et un garde les y tient.

Arbitrage d'Antoine du 2026-08-30, sur la matrice mesurée à 175 %. Sa règle : aucune fenêtre ne
doit dépasser un écran 1920 × 1080, même à 150 ou 175 % d'échelle Windows. Deux fenêtres la
violaient — Onboarding à 1 089 px et Paramètres à 1 011, pour 996 disponibles.

0,92 était le point le plus léger qui la respecte, avec 8 px de marge sur Onboarding. Il a pris
**0,90**, qui en laisse 28 : un libellé anglais plus long ou une ligne de texte ajoutée plus tard
ne referont pas déborder sans prévenir.

⚠️ Conséquence à connaître : la taille **rendue** du texte n'est plus celle que `Metrics` écrit.
`EchelleTypographique_EstCelleDeLaCharte` vérifie la table, pas le rendu, et reste donc verte —
c'est voulu, la rampe est toujours la rampe, elle est simplement appliquée à 90 %. Le garde ajouté
ici couvre l'autre moitié : les deux facteurs eux-mêmes, pour qu'aucun ne bouge par accident.
"""

import pathlib
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = pathlib.Path(__file__).resolve().parents[3]


def patch(rel, old, new, name):
    path = ROOT / rel
    data = path.read_bytes()
    if data.count(b"\r\n"):
        sys.exit(f"{rel} n'est plus en LF pur, ce patch ne sait pas le traiter")
    text = data.decode("utf-8")
    if text.count(old) != 1:
        sys.exit(f"{rel} / {name} : {text.count(old)} occurrence(s), 1 attendue")
    path.write_bytes(text.replace(old, new).encode("utf-8"))
    print(f"  {rel:44s} {name}")


patch(
    "src/ThemeControls.cs",
    """    internal static float Density { get; private set; } = 1.0f;""",
    """    internal static float Density { get; private set; } = 0.90f;""",
    "densite a 0,90",
)

patch(
    "src/Theme.cs",
    """    internal static float TypeScale { get; private set; } = 1.0f;""",
    """    internal static float TypeScale { get; private set; } = 0.90f;""",
    "echelle typo a 0,90",
)

patch(
    "src/AZERTYGlobal.Tests/ThemeTests.cs",
    """    public void EchelleTypographique_EstCelleDeLaCharte()""",
    """    public void LesDeuxFacteursGlobaux_SontCeuxQuAntoineAArretes()
    {
        // Décision du 2026-08-30, sur mesure : aucune fenêtre ne doit dépasser un écran
        // 1920 × 1080, même à 175 % d'échelle Windows. À 1,00 Onboarding rendait 1 089 px et
        // Paramètres 1 011, pour 996 de zone de travail. 0,92 passait de justesse, 0,90 laisse
        // 28 px de marge sur la plus critique.
        //
        // Ce garde n'existe pas pour empêcher de changer ces valeurs, mais pour qu'on ne les
        // change pas sans le savoir : elles pilotent la taille de sept fenêtres depuis un seul
        // point, et rien d'autre dans la suite ne rougirait.
        Assert.Equal(0.90f, ThemeControls.Density);
        Assert.Equal(0.90f, Theme.TypeScale);
    }

    [Fact]
    public void EchelleTypographique_EstCelleDeLaCharte()""",
    "garde des deux facteurs",
)
