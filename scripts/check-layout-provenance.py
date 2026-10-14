"""Compare les JSON embarqués à leur original canonique publié par le site.

Les trois fichiers de `src/` ne sont pas des sources : ce sont des copies de fichiers du
dépôt du site (`AMCF-asso/azerty-global-website`), où ils sont protégés en écriture. Ce
script prouve que la copie n'a pas dérivé, en lisant l'original sur son URL brute publique
et en comparant les empreintes SHA-256.

L'original est lu à un commit épinglé du site, consigné dans
`scripts/layout-provenance.json` : un commit ne bouge pas, donc le contrôle exigé sur les
PR ne passe plus au rouge parce que le site a évolué entre-temps, ni ne casse quand une
branche du site disparaît (décision d'Antoine du 2026-10-14, tri CI).

    python scripts/check-layout-provenance.py                 # contre le commit épinglé (CI)
    python scripts/check-layout-provenance.py --branche       # contre la tête de la branche suivie
    python scripts/check-layout-provenance.py --epingler SHA  # vérifie, puis épingle SHA

`--branche` détecte la dérive : le site a changé et les copies sont à resynchroniser
(workflow planifié `provenance-drift.yml`, non exigé). Après `Sync-LayoutResources.ps1`,
`--epingler` vérifie que les trois copies sont identiques aux originaux du commit donné
(SHA complet, déjà poussé), puis seulement réécrit `layout-provenance.json`.

Il ne lit que par le réseau ; seul `--epingler` écrit, et seulement le fichier d'épinglage.
Les originaux ne sont jamais modifiés.

Codes de sortie, volontairement distincts :

    0  les trois copies sont identiques à leur original
    1  au moins une copie a dérivé — soit le site a évolué, soit la copie a été éditée
    2  un original n'a pas pu être lu (réseau, dépôt renommé, commit ou branche disparus),
       ou l'épinglage est invalide

Le 2 existe pour qu'une panne réseau ne se lise jamais comme une dérive.
"""
import argparse
import hashlib
import json
import re
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
PIN_FILE = ROOT / "scripts" / "layout-provenance.json"
FULL_SHA = re.compile(r"^[0-9a-f]{40}$")

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


def read_pin() -> dict:
    pin = json.loads(PIN_FILE.read_text(encoding="utf-8"))
    if not FULL_SHA.match(pin.get("commit", "")):
        raise ValueError(f"commit épinglé invalide : {pin.get('commit')!r} (SHA complet attendu)")
    if not pin.get("repository") or not pin.get("branch"):
        raise ValueError("repository et branch sont requis")
    return pin


def compare(repository: str, ref: str) -> int:
    base_url = f"https://raw.githubusercontent.com/{repository}/{ref}/"
    print(f"Original : {repository} @ {ref}\n")
    drifted = 0
    for remote, local in FILES:
        path = ROOT / local
        url = base_url + urllib.request.quote(remote)

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
            "Rejouer scripts/Sync-LayoutResources.ps1, relire le diff, puis épingler le commit "
            "du site d'où viennent les copies (--epingler SHA) avant de commiter."
        )
        return 1

    print(f"\nLes {len(FILES)} copies sont identiques à leur original canonique.")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="Provenance des JSON embarqués.")
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--branche", action="store_true",
                      help="comparer à la tête de la branche suivie (dérive), pas au commit épinglé")
    mode.add_argument("--epingler", metavar="SHA",
                      help="vérifier les copies contre ce commit du site, puis l'épingler")
    args = parser.parse_args()

    try:
        pin = read_pin()
    except (OSError, ValueError) as error:
        print(f"ÉPINGLAGE  {PIN_FILE.name} : {error}")
        return 2

    if args.branche:
        return compare(pin["repository"], pin["branch"])
    if args.epingler is None:
        return compare(pin["repository"], pin["commit"])

    sha = args.epingler.strip().lower()
    if not FULL_SHA.match(sha):
        print(f"ÉPINGLAGE  {sha!r} : SHA complet de 40 caractères attendu")
        return 2
    code = compare(pin["repository"], sha)
    if code != 0:
        print("\nÉpinglage refusé : les copies doivent être identiques aux originaux de ce commit.")
        return code
    pin["commit"] = sha
    PIN_FILE.write_text(json.dumps(pin, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print(f"\nÉpinglé : {PIN_FILE.name} → {sha}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
