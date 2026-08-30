"""Le banc rend la matrice à la densité qu'on lui demande.

`AZERTY_CAPTURE_DENSITY` s'ajoute à `AZERTY_CAPTURE_THEME` et `AZERTY_CAPTURE_DPI` : même rôle,
même forme, et le défaut reste 1,00, c'est-à-dire les captures d'aujourd'hui à l'octet près.

Le nom du fichier gagne un suffixe dès que la densité n'est pas 1 — `…-sombre-150-d85.png`. Sans
lui, deux densités écriraient l'une sur l'autre dans le même dossier, et une comparaison de
densités est précisément ce pour quoi cette variable existe.
"""

import pathlib
import re
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = pathlib.Path(__file__).resolve().parents[3]
BENCH = ROOT / "src" / "AZERTYGlobal.Tests" / "CaptureBench.cs"

data = BENCH.read_bytes()
if data.count(b"\r\n"):
    sys.exit("CaptureBench.cs n'est plus en LF pur")
text = data.decode("utf-8")


def sub(name, old, new, expected=1):
    global text
    if text.count(old) != expected:
        sys.exit(f"{name} : {text.count(old)} occurrence(s), {expected} attendue(s)\n---\n{old}\n---")
    text = text.replace(old, new)
    print(f"  {name}")


sub(
    "lecture de la densite",
    """    private const string GateVariable = "AZERTY_CAPTURE";""",
    """    private const string GateVariable = "AZERTY_CAPTURE";

    /// <summary>
    /// Densité demandée, 1 par défaut. Un facteur global sur la géométrie, jamais sur le texte —
    /// voir <see cref="ThemeControls.Density"/>. Le banc est le seul endroit qui la force, comme
    /// il est le seul à forcer le DPI et le thème.
    /// </summary>
    private static float Density
    {
        get
        {
            string? raw = Environment.GetEnvironmentVariable("AZERTY_CAPTURE_DENSITY");
            if (string.IsNullOrWhiteSpace(raw))
                return 1.0f;
            return float.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float value) && value > 0
                ? value
                : 1.0f;
        }
    }

    /// <summary>
    /// Suffixe de nom de fichier de la densité : vide à 1, <c>-d85</c> à 0,85. Deux densités
    /// rendues dans le même dossier ne doivent pas s'écraser — c'est tout l'usage de la variable.
    /// </summary>
    private static string DensitySuffix =>
        Math.Abs(Density - 1.0f) < 0.001f ? "" : $"-d{(int)Math.Round(Density * 100)}";""",
)

sub(
    "portee de l'override",
    """                    using (Theme.OverrideForTests(variant))
                    using (ThemeWindow.OverrideDpiForTests(dpi))""",
    """                    using (Theme.OverrideForTests(variant))
                    using (ThemeWindow.OverrideDpiForTests(dpi))
                    using (ThemeControls.OverrideDensityForTests(Density))""",
)

# Les sept noms de fichier prennent le suffixe.
noms = [
    ('$"a-propos-{theme}-{percent}.png"', '$"a-propos-{theme}-{percent}{DensitySuffix}.png"'),
    ('$"duree-de-pause-{theme}-{percent}.png"', '$"duree-de-pause-{theme}-{percent}{DensitySuffix}.png"'),
    ('$"couches-{theme}-{percent}.png"', '$"couches-{theme}-{percent}{DensitySuffix}.png"'),
    ('$"statistiques-{theme}-{percent}.png"', '$"statistiques-{theme}-{percent}{DensitySuffix}.png"'),
    ('$"conflit-{theme}-{percent}.png"', '$"conflit-{theme}-{percent}{DensitySuffix}.png"'),
    ('$"onboarding-etape{step + 1}-{theme}-{percent}.png"',
     '$"onboarding-etape{step + 1}-{theme}-{percent}{DensitySuffix}.png"'),
]
for old, new in noms:
    sub(f"nom {old[2:20]}…", old, new)

# Paramètres a trois onglets : son nom se construit ailleurs, on le trouve tel qu'il est.
m = re.search(r'\$"parametres-\{[^"]*\}\.png"', text)
if not m:
    sys.exit("nom de fichier de Parametres introuvable")
old = m.group(0)
sub("nom parametres", old, old[:-5] + '{DensitySuffix}.png"')

BENCH.write_bytes(text.encode("utf-8"))
print(f"\nCaptureBench.cs écrit — LF pur : {chr(13) not in text}")
