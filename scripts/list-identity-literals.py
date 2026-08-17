"""Liste les littéraux d'identité produit restés hors de ProductIdentity.

Contrôle du refactor du 2026-08-17 : 78 sites ont été ramenés sur
`src/ProductIdentity.cs`. Ce script dit si un nom, une URL ou un identifiant en dur
est revenu ailleurs.

    python scripts/list-identity-literals.py

Sortie attendue sur un dépôt sain : les seules déclarations de `ProductIdentity.cs`,
plus l'`AssemblyDescription`, dont la phrase française appartient à la moitié
localisée encore due. Tout autre fichier listé est une régression.

`src/Localization/` est exclu à dessein : le nom du produit y est enchâssé dans des
phrases traduites, et leur conversion en interpolations est un chantier séparé.
"""
import re
import sys
from pathlib import Path

for stream in (sys.stdout, sys.stderr):
    reconfigure = getattr(stream, "reconfigure", None)
    if reconfigure is not None:
        try:
            reconfigure(encoding="utf-8", errors="backslashreplace")
        except (OSError, ValueError):
            pass

SRC = Path(__file__).resolve().parent.parent / "src"

PATTERN = re.compile(
    r'"AZERTY Global[^"]*"'
    r'|"AZERTYGlobal_[^"]*"'
    r'|"https://azerty\.global[^"]*"'
    r'|"https://discord\.gg/[^"]*"'
    r'|"https://github\.com/[^"]*"'
    r'|9N4BTS43SSSZ'
    r'|"favicon-azerty-global\.png"'
    r'|AZERTYGlobalSingleInstance'
)

SKIP_DIRS = {
    "Localization", "AZERTYGlobal.Tests", "TypingEngine.Core",
    "TypingEngine.Core.Tests", "TypingEngine.Windows",
    "TypingEngine.Windows.Tests", "TestSupport", "bin", "obj",
}

EXPECTED = {"ProductIdentity.cs", "Properties/AssemblyInfo.cs"}


def main() -> int:
    if not SRC.is_dir():
        print(f"Dossier source introuvable : {SRC}")
        return 2

    unexpected = 0
    for path in sorted(SRC.rglob("*.cs")):
        parts = set(path.relative_to(SRC).parts)
        if SKIP_DIRS & parts:
            continue
        rel = path.relative_to(SRC).as_posix()
        hits = [
            (n, line.strip())
            for n, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1)
            if PATTERN.search(line)
        ]
        if not hits:
            continue
        flag = "" if rel in EXPECTED else "  <-- REGRESSION"
        if rel not in EXPECTED:
            unexpected += len(hits)
        print(f"\n### {rel}  ({len(hits)}){flag}")
        for n, line in hits:
            print(f"  {n:>5}  {line[:150]}")

    print(f"\nLittéraux hors ProductIdentity : {unexpected}")
    return 1 if unexpected else 0


if __name__ == "__main__":
    sys.exit(main())
