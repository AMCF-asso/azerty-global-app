"""Témoin des contrôles de `ResourceAlignmentTests` : ils doivent virer au rouge.

Un test qui n'a jamais échoué n'est pas un test, et ces trois-là ne peuvent pas se le prouver
seuls : ils lisent les ressources **embarquées** dans l'assemblage, qu'un test ne peut pas
muter depuis l'intérieur. Ce script casse les copies de `src/` d'une façon précise, lance les
tests, vérifie que chaque mutation fait échouer le test qui existe pour elle — et rien d'autre
— puis restaure les octets d'origine.

À lancer à la main après toute évolution des ressources embarquées, jamais en CI : il écrit
dans l'arbre de travail et coûte une reconstruction.

    python scripts/witness-embedded-resources.py

Deux pièges appris le 2026-08-17, tous deux vérifiés ici :

1. Restaurer les octets ne suffit pas. `shutil.copy2` rend aussi la date d'origine, donc
   MSBuild juge l'assemblage à jour et **garde les ressources mutées dedans**. Le `git status`
   est propre et le binaire ment. D'où le `touch` après restauration.
2. Les fichiers de `src/` sont des copies de fichiers protégés du dépôt du site. Ce script
   n'écrit jamais dans l'original, et il compare les empreintes avant de rendre la main.

Codes de sortie :

    0  les trois contrôles ont vu leur régression, les fichiers sont restaurés
    1  au moins un contrôle est resté vert sur une mutation, ou une restauration a échoué
    2  contrôle impossible : fichier absent, ou dotnet introuvable
"""
import hashlib
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

for stream in (sys.stdout, sys.stderr):
    reconfigure = getattr(stream, "reconfigure", None)
    if reconfigure is not None:
        try:
            reconfigure(encoding="utf-8", errors="backslashreplace")
        except (OSError, ValueError):
            pass

ROOT = Path(__file__).resolve().parent.parent
LAYOUT = ROOT / "src" / "AZERTY Global 2026.json"
INDEX = ROOT / "src" / "character-index.json"
PROJECT = ROOT / "src" / "AZERTYGlobal.Tests" / "AZERTYGlobal.Tests.csproj"

# Une mutation par contrôle, chacune sur ce que ce contrôle prétend surveiller.
MUTATIONS = (
    (
        "EmbeddedLayout_MatchesCurrentPublicShortcuts",
        LAYOUT,
        '"shift": "#"',
        '"shift": "@"',
        '"position": "E00"',
    ),
    (
        "EmbeddedCharacterIndex_DeclaredTotalMatchesItsEntries",
        INDEX,
        '"totalCharacters": ',
        '"totalCharacters": 1',
        None,
    ),
    (
        "EmbeddedCharacterIndex_LocksCriticalFinalMetadata",
        INDEX,
        '"unicodeNameFr": "CIRCONFLEXE"',
        '"unicodeNameFr": "CIRCONFLEXE MODIFIE"',
        None,
    ),
)


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def mutate(path: Path, old: str, new: str, after: str | None) -> None:
    with open(path, "r", encoding="utf-8", newline="") as handle:
        text = handle.read()
    start = text.index(after) if after else 0
    if text.count(old, start) != 1:
        raise LookupError(f"{path.name} : {old!r} n'est pas unique, le témoin ne peut rien viser")
    where = text.index(old, start)
    with open(path, "w", encoding="utf-8", newline="") as handle:
        handle.write(text[:where] + new + text[where + len(old):])


def failed_tests() -> set[str]:
    result = subprocess.run(
        ["dotnet", "test", str(PROJECT), "-c", "Release", "--nologo",
         "--filter", "FullyQualifiedName~ResourceAlignmentTests"],
        cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace",
    )
    return set(re.findall(r"Failed\s+\S+\.ResourceAlignmentTests\.(\w+)", result.stdout))


def main() -> int:
    for path in (LAYOUT, INDEX, PROJECT):
        if not path.is_file():
            print(f"Introuvable : {path}")
            return 2
    if shutil.which("dotnet") is None:
        print("dotnet est introuvable sur le PATH.")
        return 2

    with tempfile.TemporaryDirectory(prefix="temoin-ressources-") as folder:
        backup = {path: Path(folder) / path.name for path in (LAYOUT, INDEX)}
        original = {path: digest(path) for path in backup}
        for path, saved in backup.items():
            shutil.copy2(path, saved)

        verdicts = []
        try:
            for expected, path, old, new, after in MUTATIONS:
                mutate(path, old, new, after)
                seen = failed_tests()
                shutil.copy2(backup[path], path)
                # La date, pas seulement les octets : sans ce touch, la reconstruction
                # suivante resservirait l'assemblage muté.
                path.touch()

                unexpected = seen - {expected}
                verdicts.append((expected, expected in seen, sorted(unexpected)))
                state = "ROUGE" if expected in seen else "RESTE VERT"
                print(f"  {state:<11} {expected}")
                for other in sorted(unexpected):
                    print(f"              aussi en echec : {other}")
        finally:
            for path, saved in backup.items():
                shutil.copy2(saved, path)
                path.touch()

        broken = [path.name for path in backup if digest(path) != original[path]]
        if broken:
            print(f"\nRestauration incomplete : {', '.join(broken)}")
            return 1

    print("\nLes deux ressources sont revenues a leurs octets d'origine.")
    blind = [name for name, red, _ in verdicts if not red]
    noisy = [(name, others) for name, _, others in verdicts if others]
    for name, others in noisy:
        print(f"Bruit : la mutation visant {name} a aussi fait echouer {', '.join(others)}.")
    if blind:
        print(f"\n{len(blind)} controle(s) n'ont pas vu leur regression : {', '.join(blind)}")
        return 1
    print(f"{len(verdicts)} controle(s) ont vu leur regression. Les tests mordent.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
