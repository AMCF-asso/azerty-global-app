"""Génère src/lesson-instruction-history.json : toutes les consignes (FR et EN) qu'un
exercice a portées dans l'historique git, par clé « module/leçon/rang ».

Pourquoi : jusqu'au 09/11/2026, l'empreinte d'un exercice incluait sa consigne traduite
(LessonCatalogLoader.ComputeLegacyExerciseHash). Passer l'app en anglais cachait donc toute
la progression française (audit UX du 09/11, L1). L'empreinte ne dépend plus que du type et
du texte à taper ; cette table, figée, permet de reconnaître les clés écrites par les
versions précédentes (1.0 à 1.2 publiées, 1.3.0 de test). Une ancienne clé n'est reprise
que si son empreinte se recalcule avec le texte actuel : un texte modifié invalide toujours
la progression, comme avant.

La table est figée : modifier une consigne aujourd'hui ne change plus l'empreinte, il n'y a
rien à régénérer. Relancer seulement pour réparer la table elle-même.

Usage : python scripts/build-lesson-instruction-history.py   (depuis la racine du dépôt)
"""
from __future__ import annotations

import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "src" / "lesson-instruction-history.json"
INIT_MODULE, INIT_LESSON = "initiation", "premiers-pas"

# Chaînes C# : "..." avec échappements simples ; T("fr", "en") éventuellement sur plusieurs lignes.
CS_STRING = r'"((?:[^"\\]|\\.)*)"'
INIT_RE = re.compile(r"Lessons_Init(\d)\s*=>\s*T\(\s*" + CS_STRING + r"\s*,\s*" + CS_STRING + r"\s*\)", re.S)


def git(*args: str) -> str:
    return subprocess.run(["git", "-C", str(ROOT), *args], check=True, capture_output=True,
                          text=True, encoding="utf-8").stdout


def blobs(path: str) -> list[str]:
    """Contenu du fichier à chaque commit (toutes branches) qui l'a touché, plus l'arbre de travail."""
    shas = [s for s in git("log", "--all", "--format=%H", "--", path).split() if s]
    seen, out = set(), []
    for sha in shas:
        try:
            text = git("show", f"{sha}:{path}")
        except subprocess.CalledProcessError:
            continue  # fichier supprimé dans ce commit
        if text not in seen:
            seen.add(text)
            out.append(text)
    work = ROOT / path
    if work.exists():
        text = work.read_text(encoding="utf-8-sig")
        if text not in seen:
            out.append(text)
    return out


def cs_unescape(s: str) -> str:
    return json.loads('"' + s + '"')


def main() -> int:
    table: dict[str, set[str]] = {}

    for text in blobs("src/lessons.json"):
        try:
            doc = json.loads(text.lstrip("﻿"))
        except json.JSONDecodeError:
            continue
        for module in doc.get("modules", []):
            for lesson in module.get("lessons", []):
                for i, ex in enumerate(lesson.get("exercises", [])):
                    key = f"{module['id']}/{lesson['id']}/{i}"
                    bag = table.setdefault(key, set())
                    for field in ("instruction", "instructionEn"):
                        if ex.get(field):
                            bag.add(ex[field])

    for path in ("src/Localization/L.Lessons.cs",):
        for text in blobs(path):
            for m in INIT_RE.finditer(text):
                key = f"{INIT_MODULE}/{INIT_LESSON}/{m.group(1)}"
                bag = table.setdefault(key, set())
                bag.add(cs_unescape(m.group(2)))
                bag.add(cs_unescape(m.group(3)))

    if not table:
        print("aucune consigne trouvée", file=sys.stderr)
        return 1
    data = {key: sorted(values) for key, values in sorted(table.items())}
    OUT.write_text(json.dumps(data, ensure_ascii=False, indent=1) + "\n", encoding="utf-8", newline="\n")
    print(f"{OUT.relative_to(ROOT)} : {len(data)} exercices, {sum(map(len, data.values()))} consignes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
