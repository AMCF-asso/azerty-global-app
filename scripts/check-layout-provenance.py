"""Compare les JSON embarqués à leur original canonique publié par le site.

Les trois fichiers de `src/` ne sont pas des sources : ce sont des copies de fichiers du
dépôt `AZERTYGlobal/website`, où ils sont protégés en écriture. Ce script prouve que la
copie n'a pas dérivé, en lisant l'original sur son URL brute publique et en comparant les
empreintes SHA-256.

    python scripts/check-layout-provenance.py

Il ne lit que par le réseau et n'écrit rien. Les originaux ne sont jamais modifiés.

Codes de sortie, volontairement distincts :

    0  les trois copies sont identiques à leur original
    1  au moins une copie a dérivé — soit le site a évolué, soit la copie a été éditée
    2  un original n'a pas pu être lu (réseau, dépôt renommé, branche disparue)

Le 2 existe pour qu'une panne réseau ne se lise jamais comme une dérive.
"""
import hashlib
import sys
import urllib.error
import urllib.request
from pathlib import Path

for stream in (sys.stdout, sys.stderr):
    reconfigure = getattr(stream, "reconfigure", None)
    if reconfigure is not None:
        try:
            reconfigure(encoding="utf-8", errors="backslashreplace")
        except (OSError, ValueError):
            pass

ROOT = Path(__file__).resolve().parent.parent
BASE_URL = "https://raw.githubusercontent.com/AZERTYGlobal/website/main/"

# (chemin distant dans le dépôt du site, chemin local dans ce dépôt).
# Le nom local de la disposition porte son millésime, celui du site non : c'est la seule
# divergence de nom, et elle est intentionnelle.
FILES = [
    ("data/AZERTY Global.json", "src/AZERTY Global 2026.json"),
    ("tester/character-index.json", "src/character-index.json"),
    ("tester/lessons.json", "src/lessons.json"),
]

TIMEOUT_SECONDS = 30


def digest(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def fetch(url: str) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": "azerty-global-ci"})
    with urllib.request.urlopen(request, timeout=TIMEOUT_SECONDS) as response:
        return response.read()


def main() -> int:
    drifted = 0
    for remote, local in FILES:
        path = ROOT / local
        url = BASE_URL + urllib.request.quote(remote)

        if not path.is_file():
            print(f"ABSENTE    {local} — la copie n'existe pas dans ce dépôt")
            return 2
        try:
            canonical = fetch(url)
        except (urllib.error.URLError, TimeoutError, OSError) as error:
            print(f"ILLISIBLE  {remote}\n           {url}\n           {error}")
            print("\nOriginal inaccessible : ce n'est pas une dérive, le contrôle n'a pas pu avoir lieu.")
            return 2

        embedded = path.read_bytes()
        expected, actual = digest(canonical), digest(embedded)
        if expected == actual:
            print(f"IDENTIQUE  {local}  {actual[:16]}  {len(embedded)} o")
            continue

        drifted += 1
        print(
            f"DERIVE     {local}\n"
            f"           original {expected[:16]}  {len(canonical)} o  ({remote})\n"
            f"           copie    {actual[:16]}  {len(embedded)} o"
        )

    if drifted:
        print(
            f"\n{drifted} copie(s) ont dérivé de leur original.\n"
            "Rejouer scripts/Sync-LayoutResources.ps1, puis relire le diff avant de commiter."
        )
        return 1

    print(f"\nLes {len(FILES)} copies sont identiques à leur original canonique.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
