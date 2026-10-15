"""Construit `src/language-search.json`, la table de la recherche par langue.

La recherche de caractère trouve, en plus des noms et des alias de `character-index.json`,
les caractères d'une langue quand on tape son nom (« pulaar », « wolof », « soussou »…) et
les accents combinants des tons (« ton », « tone », « ton haut »…).

Aucun alphabet n'est écrit ici. Les lettres de chaque langue viennent du référentiel Afrique
du site (`data/afrique/*.json` du dépôt `AZERTYGlobal/website`), lu à une révision fixée par
`git show` : rien n'est copié à la main, et la révision est inscrite dans la table. Ce script
n'ajoute que des NOMS de langue (anglais, endonymes, variantes), chacun avec sa source, et les
fonctions de ton des accents combinants, sourcées par l'alphabet phonétique international.

Seuls les caractères présents dans `src/character-index.json` entrent : les autres
(« a̱ », « ⱳ », « ꞵ »…) ne sont pas trouvables par la recherche, la table les liste à part.

    python scripts/build-language-search.py --site "<checkout du site>" --rev d65e51d

`character-index.json` n'est pas modifié : c'est une copie du site, sous contrôle de
provenance (`check-layout-provenance.py`).
"""
import argparse
import json
import re
import subprocess
import sys
import unicodedata
from pathlib import Path

for stream in (sys.stdout, sys.stderr):
    reconfigure = getattr(stream, "reconfigure", None)
    if reconfigure is not None:
        reconfigure(encoding="utf-8", errors="backslashreplace")

ROOT = Path(__file__).resolve().parent.parent
INDEX = ROOT / "src" / "character-index.json"
OUTPUT = ROOT / "src" / "language-search.json"

# Mots retirés des noms du référentiel avant d'en faire des alias : un pays, une précision
# d'écriture ou de groupe ne nomme pas la langue (« Pulaar (peul Sénégal) » ne doit pas
# répondre à « sénégal », « Haoussa (latin) » pas à « latin »).
MOTS_RETIRES = {
    "de", "du", "des", "la", "le", "groupe", "latin", "nord", "sud", "cote", "est",
    "senegal", "guinee", "nigerian",
}

ISO = "ISO 639-3 (SIL), nom de référence du code"
PEUL = ("site : scripts/build-afrique-data.mjs, commentaire « Le peul porte un code de variété "
        "différent selon le pays » (fuc, fuf, fub) ; data/afrique : « Fulfulde Maasina » (ffm), "
        "« Peul (fulfulde) » (fuv)")
INDEX_ALIAS = "src/character-index.json : alias déjà présents (« b peul », « b fula »)"
WIKI_FULA = "en.wikipedia.org/wiki/Fula_language : « Fula, also known as Fulani or Fulah »"

# Alias ajoutés aux noms du référentiel : (alias, source). Les variantes sans accent ne sont
# pas listées : la recherche normalise les diacritiques.
FULA = [("peul", PEUL), ("fula", INDEX_ALIAS), ("fulani", WIKI_FULA), ("fulah", WIKI_FULA),
        ("fulfulde", PEUL), ("pulaar", PEUL), ("pular", PEUL)]
ALIAS_AJOUTES = {
    "fuc": FULA, "fuf": FULA, "fub": FULA, "ffm": FULA, "fuv": FULA,
    "bam": [("bamanankan", "en.wikipedia.org/wiki/Bambara_language : « Bambara, also known as Bamanankan »")],
    "emk": [("maninka", f"{ISO} emk « Eastern Maninkakan » et nom du site « Maninkakan »"),
            ("malinke", "nom du site « Manding-malinké »"),
            ("mandingue", "nom du site « Manding-malinké » (manding, forme française)")],
    "man": [("mandingue", "en.wikipedia.org/wiki/Mandinka_language : langue mandingue"),
            ("mandingo", f"{ISO} man « Mandingo »")],
    "sus": [("susu", f"{ISO} sus « Susu »")],
    "dyu": [("dyula", f"{ISO} dyu « Dyula »"), ("jula", "en.wikipedia.org/wiki/Dyula_language : « Dyula (or Jula) »")],
    "mos": [("mossi", f"{ISO} mos « Mossi »"), ("moore", "nom du site « Mooré », sans accent")],
    "hau": [("hausa", f"{ISO} hau « Hausa »")],
    "srr": [("serer", f"{ISO} srr « Serer »")],
    "dyo": [("jola", f"{ISO} dyo « Jola-Fonyi »")],
    "dje": [("djerma", "fr.wikipedia.org/wiki/Zarma_(langue) : « zarma ou djerma »")],
    "mlg": [("malagasy", f"{ISO} mlg « Malagasy »")],
    "dua": [("duala", f"{ISO} dua « Duala »")],
    "bci": [("baule", f"{ISO} bci « Baoulé » (forme anglaise Baule)")],
    "any": [("anyin", f"{ISO} any « Anyin »")],
    "gux": [("gourmanchema", f"{ISO} gux « Gourmanchéma »")],
    "kbp": [("kabiye", f"{ISO} kbp « Kabiyè »")],
    "kon": [("kongo", f"{ISO} kon « Kongo »")],
    "lua": [("luba", f"{ISO} lua « Luba-Lulua »")],
    "tmh": [("tamasheq", "en.wikipedia.org/wiki/Tamasheq_language")],
    "tzm": [("tamazight", f"{ISO} tzm « Central Atlas Tamazight »")],
    "naq": [("khoekhoe", f"{ISO} naq « Khoekhoe »")],
    "ewe": [("ewe", f"{ISO} ewe « Ewe »")],
}

IPA = "Alphabet phonétique international (révision 2020), « Tones and word accents »"
UNICODE_CONTOUR = "Unicode, tableau U+1DC0, rubrique « Contour tone marks »"
TONS = {
    chr(0x030B): (["ton très haut", "extra high tone"], IPA),
    chr(0x0301): (["ton haut", "high tone"], IPA),
    chr(0x0304): (["ton moyen", "mid tone"], IPA),
    chr(0x0300): (["ton bas", "low tone"], IPA),
    chr(0x030F): (["ton très bas", "extra low tone"], IPA),
    chr(0x0302): (["ton descendant", "falling tone"], IPA),
    chr(0x030C): (["ton montant", "rising tone"], IPA),
    chr(0x1DC6): (["ton modulé", "contour tone"], UNICODE_CONTOUR),
    chr(0x1DC7): (["ton modulé", "contour tone"], UNICODE_CONTOUR),
}
# Alias de nom pour l'accent grave combinant, seul accent de ton sans alias dans l'index
# (audit du 27/09, § 3) : décision du 27/09, « grave combinant ».
GRAVE = (["accent grave combinant", "combining grave accent"],
         "decisions.md du 2026-09-27 (« grave combinant ») ; nom Unicode U+0300")


def norm(text: str) -> str:
    return "".join(c for c in unicodedata.normalize("NFD", text) if unicodedata.category(c) != "Mn").lower()


def git(site: str, *args: str) -> str:
    return subprocess.run(["git", "-C", site, *args], check=True, capture_output=True,
                          encoding="utf-8").stdout


def noms_du_referentiel(nom: str) -> list[str]:
    """« Manding-malinké (Maninkakan) » → [« manding-malinké », « maninkakan »]."""
    parties = [nom]
    m = re.match(r"^(.*?)\s*\((.*)\)\s*$", nom)
    if m:
        parties = [m.group(1), m.group(2)]
    alias = []
    for partie in parties:
        mots = [mot for mot in re.split(r"\s+", partie.strip()) if norm(mot) not in MOTS_RETIRES]
        if mots:
            alias.append(" ".join(mots).lower())
    return alias


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--site", required=True, help="checkout git du dépôt du site")
    parser.add_argument("--rev", required=True, help="révision du référentiel Afrique")
    args = parser.parse_args()

    index = json.loads(INDEX.read_text(encoding="utf-8"))["characters"]
    rev = git(args.site, "rev-parse", "--short", args.rev).strip()
    fichiers = [f for f in git(args.site, "ls-tree", "--name-only", rev, "data/afrique/").split()
                if f.endswith(".json") and not f.endswith("index.json")]

    langues: dict[str, dict] = {}
    for fichier in sorted(fichiers):
        for langue in json.loads(git(args.site, "show", f"{rev}:{fichier}"))["langues"]:
            caracteres = []
            for c in langue.get("caracteres", []):
                caracteres.append(c["char"])
                if "majuscule" in c:
                    caracteres.append(c["majuscule"]["char"])
            langues[langue["id"]] = {"langue": langue, "caracteres": caracteres}

    groupes, absents = [], {}
    for code, item in sorted(langues.items()):
        langue = item["langue"]
        retenus, ecartes = [], []
        for ch in item["caracteres"]:
            if ch in retenus or ch in ecartes:
                continue
            (retenus if ch in index else ecartes).append(ch)
        if ecartes:
            absents[code] = ecartes
        if not retenus:
            continue
        alias = [{"alias": a, "source": f"site {rev} : data/afrique, nom « {langue['nom']} »"}
                 for a in noms_du_referentiel(langue["nom"])]
        vus = {norm(a["alias"]) for a in alias}
        for a, source in ALIAS_AJOUTES.get(code, []):
            if norm(a) not in vus:
                alias.append({"alias": a, "source": source})
                vus.add(norm(a))
        groupes.append({
            "id": code,
            "type": "langue",
            "nom": langue["nom"],
            "source": langue["source"],
            "provisoire": bool(langue.get("provisoire")),
            "caracteres": retenus,
            "alias": alias,
        })

    for mark, (alias, source) in TONS.items():
        if mark not in index:
            absents.setdefault("tons", []).append(mark)
            continue
        groupes.append({
            "id": f"ton-{ord(mark):04X}",
            "type": "signe",
            "nom": index[mark].get("unicodeNameFr") or f"U+{ord(mark):04X}",
            "source": {"ref": source},
            "provisoire": False,
            "caracteres": [mark],
            "alias": [{"alias": a, "source": source} for a in ["ton", "tone", *alias]],
        })
    groupes.append({
        "id": "grave-combinant",
        "type": "signe",
        "nom": index[chr(0x0300)].get("unicodeNameFr", ""),
        "source": {"ref": GRAVE[1]},
        "provisoire": False,
        "caracteres": [chr(0x0300)],
        "alias": [{"alias": a, "source": GRAVE[1]} for a in GRAVE[0]],
    })

    sortie = {
        "meta": {
            "role": "Recherche par langue de la recherche de caractère (CharacterSearch).",
            "genereePar": "scripts/build-language-search.py",
            "referentiel": f"AZERTYGlobal/website, révision {rev}, data/afrique/*.json",
            "index": "src/character-index.json (seuls ses caractères sont retenus)",
            "provisoire": "true quand le référentiel du site marque la langue « provisoire » "
                          "(source Wikipédia ou jeu repris d'une autre variété).",
        },
        "groupes": groupes,
        "horsIndex": absents,
    }
    texte = json.dumps(sortie, ensure_ascii=False, indent=1) + "\n"
    OUTPUT.write_text(texte, encoding="utf-8", newline="\n")
    print(f"{sum(g['type'] == 'langue' for g in groupes)} langues, "
          f"{sum(g['type'] == 'signe' for g in groupes)} signes, révision {rev} → {OUTPUT.name}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
