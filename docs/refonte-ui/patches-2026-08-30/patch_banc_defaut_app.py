"""Sans variable, le banc rend ce que l'application rend.

Défaut mesuré le 2026-08-30, et il rendait un faux vert dans le mauvais sens : la matrice rendue
sans `AZERTY_CAPTURE_DENSITY` sortait à densité **1,00** alors que l'application était passée à
0,90. Le banc repliait sur `1.0f` en dur au lieu de replier sur la valeur de l'application, si
bien que la matrice de contrôle ne montrait pas le produit — elle montrait un produit qui
n'existait plus.

Le repli devient `ThemeControls.Density` et `Theme.TypeScale`. Une variable absente ne force donc
plus rien : elle laisse l'application décider, ce qui est le seul comportement qui rende la
matrice représentative.

Le suffixe de nom de fichier suit la même logique : il ne marque un fichier que lorsque la valeur
rendue diffère de celle de l'application, et non lorsqu'elle diffère de 1.
"""

import pathlib
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = pathlib.Path(__file__).resolve().parents[3]
TARGET = ROOT / "src" / "AZERTYGlobal.Tests" / "CaptureBench.cs"

data = TARGET.read_bytes()
if data.count(b"\r\n"):
    sys.exit("CaptureBench.cs n'est plus en LF pur")
text = data.decode("utf-8")


def sub(name, old, new):
    global text
    if text.count(old) != 1:
        sys.exit(f"{name} : {text.count(old)} occurrence(s), 1 attendue")
    text = text.replace(old, new)
    print(f"  {name}")


sub(
    "repli de la densite",
    """            string? raw = Environment.GetEnvironmentVariable("AZERTY_CAPTURE_DENSITY");
            if (string.IsNullOrWhiteSpace(raw))
                return 1.0f;
            return float.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float value) && value > 0
                ? value
                : 1.0f;""",
    """            string? raw = Environment.GetEnvironmentVariable("AZERTY_CAPTURE_DENSITY");
            if (string.IsNullOrWhiteSpace(raw))
                return ThemeControls.Density;
            return float.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float value) && value > 0
                ? value
                : ThemeControls.Density;""",
)

sub(
    "repli de l'echelle typo",
    """            string? raw = Environment.GetEnvironmentVariable("AZERTY_CAPTURE_TYPE");
            if (string.IsNullOrWhiteSpace(raw))
                return 1.0f;
            return float.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float value) && value > 0
                ? value
                : 1.0f;""",
    """            string? raw = Environment.GetEnvironmentVariable("AZERTY_CAPTURE_TYPE");
            if (string.IsNullOrWhiteSpace(raw))
                return Theme.TypeScale;
            return float.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float value) && value > 0
                ? value
                : Theme.TypeScale;""",
)

sub(
    "suffixe de densite",
    """    private static string DensitySuffix =>
        Math.Abs(Density - 1.0f) < 0.001f ? "" : $"-d{(int)Math.Round(Density * 100)}";""",
    """    private static string DensitySuffix =>
        Math.Abs(Density - ThemeControls.Density) < 0.001f
            ? ""
            : $"-d{(int)Math.Round(Density * 100)}";""",
)

sub(
    "suffixe d'echelle typo",
    """    private static string TypeSuffix =>
        Math.Abs(TypeScale - 1.0f) < 0.001f ? "" : $"-t{(int)Math.Round(TypeScale * 100)}";""",
    """    private static string TypeSuffix =>
        Math.Abs(TypeScale - Theme.TypeScale) < 0.001f
            ? ""
            : $"-t{(int)Math.Round(TypeScale * 100)}";""",
)

TARGET.write_bytes(text.encode("utf-8"))
print("\nCaptureBench.cs écrit")
