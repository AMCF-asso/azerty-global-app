"""Compare les JSON embarqués à leur original canonique publié par le site.

Les trois fichiers de `src/` ne sont pas des sources : ce sont des copies de fichiers du
dépôt `AZERTYGlobal/website`, où ils sont protégés en écriture. Ce script prouve que la
copie n'a pas dérivé, en lisant l'original sur son URL brute publique et en comparant les
empreintes SHA-256.

    python scripts/check-layout-provenance.py

Il ne lit que par le réseau et n'écrit rien. Les originaux ne sont jamais modifiés.

Exception : `src/lessons.json` est une copie enrichie. L'app y ajoute ses propres leçons
(APP_ONLY_LESSON_IDS) ; le contrôle les retire de la copie, puis exige que le reste soit
le même JSON que l'original (décision d'Antoine, QCM du 2026-10-12).

Codes de sortie, volontairement distincts :

    0  les trois copies sont identiques à leur original (lessons.json : hors leçons de l'app)
    1  au moins une copie a dérivé — soit le site a évolué, soit la copie a été éditée
    2  un original n'a pas pu être lu (réseau, dépôt renommé, branche disparue)

Le 2 existe pour qu'une panne réseau ne se lise jamais comme une dérive.
"""
import hashlib
import json
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
# Branche `refonte` tant que la disposition 2026.1 n'est publiée qu'avec la v2 du site
# (décision du 2026-09-25) ; revenir à `main` à la bascule de la v2.
BASE_URL = "https://raw.githubusercontent.com/AZERTYGlobal/website/refonte/"

# (chemin distant dans le dépôt du site, chemin local dans ce dépôt).
# Le nom local de la disposition porte son millésime, celui du site non : c'est la seule
# divergence de nom, et elle est intentionnelle.
FILES = [
    ("data/AZERTY Global.json", "src/AZERTY Global 2026.json"),
    ("tester/character-index.json", "src/character-index.json"),
    ("tester/lessons.json", "src/lessons.json"),
]

ENRICHED_COPY = "src/lessons.json"

# Leçons propres à l'app, absentes du site : « Un tour d'Afrique » (1.3.0, décision du
# 2026-10-07). Même liste dans scripts/Sync-LayoutResources.ps1, qui ne copie plus
# lessons.json par-dessus elles.
APP_ONLY_LESSON_IDS = frozenset({"course-20-millions-afrique"})

TIMEOUT_SECONDS = 30


def digest(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def strip_app_lessons(payload: bytes) -> tuple[object, list[str]]:
    data = json.loads(payload)
    removed = []
    for module in data.get("modules", []):
        kept = []
        for lesson in module.get("lessons", []):
            if lesson.get("id") in APP_ONLY_LESSON_IDS:
                removed.append(lesson["id"])
            else:
                kept.append(lesson)
        module["lessons"] = kept
    return data, removed


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

        if local == ENRICHED_COPY:
            stripped, removed = strip_app_lessons(embedded)
            if removed and stripped == json.loads(canonical):
                print(f"ENRICHIE   {local}  original + leçons de l'app : {', '.join(removed)}")
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
            "Rejouer scripts/Sync-LayoutResources.ps1, puis relire le diff avant de commiter.\n"
            "lessons.json enrichi : reporter à la main les changements du site."
        )
        return 1

    print(f"\nLes {len(FILES)} copies sont identiques à leur original canonique.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
