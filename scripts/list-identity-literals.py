"""Liste les littéraux d'identité produit restés hors de ProductIdentity.

Contrôle du refactor du 2026-08-17 : 78 sites ont d'abord été ramenés sur
`src/ProductIdentity.cs`, puis les 86 littéraux des phrases traduites de
`src/Localization/` ont été convertis en interpolations. Ce script dit si un nom, une
URL ou un identifiant en dur est revenu ailleurs.

    python scripts/list-identity-literals.py

Sortie attendue sur un dépôt sain : les seules déclarations de `ProductIdentity.cs`.
Tout autre fichier listé est une régression.

`src/Localization/` est désormais couvert : ses chaînes interpolent `L.Product`, alias
privé de `ProductIdentity.DisplayName`, et n'écrivent plus le nom en dur.
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

# Le nom et le domaine sont cherchés n'importe où DANS un littéral, pas seulement en
# tête : depuis la conversion de Localization/, une régression prendrait la forme d'une
# phrase traduite qui réécrit « AZERTY Global » au milieu, pas d'un littéral qui commence
# par lui. Une ancre en tête n'aurait rien vu.
# Même raison pour le family name de paquet : il s'écrit
# « AZERTYGlobal.AZERTYGlobal_<PublisherId> », donc un motif ancré sur AZERTYGlobal_ en
# tête de littéral ne le voyait pas. Mesuré le 2026-08-19 : le scanner rendait 0 alors que
# ProductIdentity.cs venait d'en déclarer un.
PATTERN = re.compile(
    r'"[^"\n]*AZERTY Global[^"\n]*"'
    r'|"[^"\n]*azerty\.global[^"\n]*"'
    r'|"[^"\n]*AZERTYGlobal_[^"\n]*"'
    r'|"https://discord\.gg/[^"]*"'
    r'|"https://github\.com/[^"]*"'
    r'|9N4BTS43SSSZ'
    r'|"favicon-azerty-global\.png"'
    r'|AZERTYGlobalSingleInstance'
)

SKIP_DIRS = {
    "AZERTYGlobal.Tests", "TypingEngine.Core",
    "TypingEngine.Core.Tests", "TypingEngine.Windows",
    "TypingEngine.Windows.Tests", "TestSupport", "bin", "obj",
}

EXPECTED = {"ProductIdentity.cs"}


def strip_line_comment(line: str) -> str:
    """Coupe la partie `//` d'une ligne, sans se laisser prendre par un `//` dans un
    littéral (« https://… »). Le motif étant élargi à l'intérieur des chaînes, un
    commentaire qui cite le nom entre guillemets serait sinon compté comme régression —
    c'est arrivé sur `TrayApplication.cs:1219`."""
    in_string = False
    escaped = False
    i = 0
    while i < len(line):
        c = line[i]
        if in_string:
            if escaped:
                escaped = False
            elif c == "\\":
                escaped = True
            elif c == '"':
                in_string = False
        elif c == '"':
            in_string = True
        elif c == "/" and line[i + 1:i + 2] == "/":
            return line[:i]
        i += 1
    return line


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
            if PATTERN.search(strip_line_comment(line))
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
