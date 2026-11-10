"""Génère les deux tables de reprise de la progression des Leçons, depuis l'historique git :

- src/lesson-instruction-history.json : toutes les consignes (FR et EN) qu'un exercice a
  portées, par clé « module/leçon/rang » ;
- src/lesson-content-history.json : les anciennes formes d'un exercice actuel, chacune
  avec sa clé et son texte à taper : même clé et texte changé depuis, ou clé disparue de
  la même leçon dont le texte est celui de l'exercice actuel (exercice changé de rang).

Pourquoi : jusqu'au 09/11/2026, l'empreinte d'un exercice incluait sa consigne traduite
(LessonCatalogLoader.ComputeLegacyExerciseHash). Passer l'app en anglais cachait donc toute
la progression française (audit UX du 09/11, L1). L'empreinte ne dépend plus que du type et
du texte à taper ; la première table permet de reconnaître les clés écrites par les
versions précédentes (1.0 à 1.2 publiées, 1.3.0 de test), avec tous leurs scores, tant que
le texte n'a pas changé.

La seconde table (audit de régression du 10/11, RA2) couvre les exercices dont le texte a
changé depuis une version publiée : une réussite enregistrée sous une ancienne forme est
reprise comme « réussi », sans ses scores. Un exercice disparu dont le texte n'est repris
par aucun exercice de sa leçon n'a rien à reprendre : il est seulement listé.

Avant le 15/08/2026 (452aab0), les consignes de l'Initiation étaient écrites en dur dans
LessonCatalog.cs (« Tape É… », tutoiement) : elles entrent aussi dans la première table.

Les deux tables sont figées. À relancer pour réparer une table, ou après avoir changé le
texte d'un exercice déjà publié : son ancien texte entre alors dans la seconde.

Usage (depuis la racine du dépôt) :
  python scripts/build-lesson-instruction-history.py
  python scripts/build-lesson-instruction-history.py --report
      liste en plus chaque ancien texte avec les commits qui le portent
  python scripts/build-lesson-instruction-history.py --history <autre arbre git>
      lit l'historique dans un autre arbre (copie de travail hors git) ; les fichiers
      actuels et les tables restent ceux de ce dossier
"""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "src" / "lesson-instruction-history.json"
OUT_CONTENT = ROOT / "src" / "lesson-content-history.json"
INIT_MODULE, INIT_LESSON = "initiation", "premiers-pas"

# Chaînes C# : "..." avec échappements simples ; T("fr", "en") éventuellement sur plusieurs lignes.
CS_STRING = r'"((?:[^"\\]|\\.)*)"'
INIT_RE = re.compile(r"Lessons_Init(\d)\s*=>\s*T\(\s*" + CS_STRING + r"\s*,\s*" + CS_STRING + r"\s*\)", re.S)
# Textes de l'Initiation : dans LessonCatalog.cs jusqu'au 26/09/2026 (cb3843d), puis dans
# TutorialSteps.cs, un par rang, dans l'ordre du tableau All.
# La consigne y est d'abord une chaîne en dur (groupe 2), puis L.Lessons_InitN.
INIT_TEXT_RE = re.compile(r"InitiationExercise\(\s*(\d+)\s*,\s*(?:L\.Lessons_Init\d+|" + CS_STRING + r")\s*,\s*"
                          + CS_STRING + r"\s*\)")
STEPS_BLOCK_RE = re.compile(r"Step\[\]\s+All\s*=\s*\{(.*?)\n\s*\};", re.S)
STEP_RE = re.compile(r"new\(\s*" + CS_STRING + r"\s*,\s*KeepCapsLock")

HISTORY = ROOT


def git(*args: str) -> str:
    return subprocess.run(["git", "-C", str(HISTORY), *args], check=True, capture_output=True,
                          text=True, encoding="utf-8").stdout


def versions(path: str) -> list[tuple[str, str]]:
    """(commit, contenu) du fichier à chaque commit (toutes branches) qui l'a touché, du plus
    récent au plus ancien, puis l'arbre de travail (commit « travail »)."""
    out = []
    for line in git("log", "--all", "--format=%h %ad", "--date=short", "--", path).splitlines():
        if not line.strip():
            continue
        sha = line.split()[0]
        try:
            out.append((line.strip(), git("show", f"{sha}:{path}")))
        except subprocess.CalledProcessError:
            continue  # fichier supprimé dans ce commit
    work = ROOT / path
    if work.exists():
        out.append(("travail", work.read_text(encoding="utf-8-sig")))
    return out


def blobs(path: str) -> list[str]:
    """Contenus distincts du fichier dans l'historique, plus l'arbre de travail."""
    seen, out = set(), []
    for _, text in versions(path):
        if text not in seen:
            seen.add(text)
            out.append(text)
    return out


def cs_unescape(s: str) -> str:
    return json.loads('"' + s + '"')


def normalize(content: str) -> str:
    """Comme LessonExercise : l'empreinte se calcule sur des fins de ligne « \\n »."""
    return content.replace("\r\n", "\n")


def lesson_texts(text: str) -> dict[str, str]:
    """Texte à taper de chaque exercice d'un lessons.json, par clé."""
    try:
        doc = json.loads(text.lstrip("﻿"))
    except json.JSONDecodeError:
        return {}
    out = {}
    for module in doc.get("modules", []):
        for lesson in module.get("lessons", []):
            for i, ex in enumerate(lesson.get("exercises", [])):
                if isinstance(ex.get("content"), str):
                    out[f"{module['id']}/{lesson['id']}/{i}"] = normalize(ex["content"])
    return out


def initiation_texts(text: str) -> dict[str, str]:
    """Texte à taper de chaque exercice de l'Initiation, lu dans LessonCatalog.cs (textes en
    dur) ou dans TutorialSteps.cs (tableau All)."""
    out = {}
    for m in INIT_TEXT_RE.finditer(text):
        out[f"{INIT_MODULE}/{INIT_LESSON}/{int(m.group(1))}"] = normalize(cs_unescape(m.group(3)))
    block = STEPS_BLOCK_RE.search(text)
    if block:
        for i, m in enumerate(STEP_RE.finditer(block.group(1))):
            out[f"{INIT_MODULE}/{INIT_LESSON}/{i}"] = normalize(cs_unescape(m.group(1)))
    return out


def build_instructions() -> dict[str, list[str]]:
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

    # Consignes en dur de l'Initiation, avant leur passage dans L.Lessons.cs.
    for text in blobs("src/LessonCatalog.cs"):
        for m in INIT_TEXT_RE.finditer(text):
            if m.group(2) is not None:
                key = f"{INIT_MODULE}/{INIT_LESSON}/{int(m.group(1))}"
                table.setdefault(key, set()).add(cs_unescape(m.group(2)))

    return {key: sorted(values) for key, values in sorted(table.items())}


def build_contents(report: bool) -> dict[str, list[dict[str, str]]]:
    """Anciennes formes (clé, texte) de chaque exercice actuel, lues dans l'historique."""
    sources = (
        ("src/lessons.json", lesson_texts),
        ("src/LessonCatalog.cs", initiation_texts),
        ("src/TutorialSteps.cs", initiation_texts),
    )
    current: dict[str, str] = {}
    # clé -> texte -> commits qui le portent (du plus récent au plus ancien)
    seen: dict[str, dict[str, list[str]]] = {}
    for path, parse in sources:
        for commit, text in versions(path):
            texts = parse(text)
            if commit == "travail":
                current.update(texts)
                continue
            for key, content in texts.items():
                seen.setdefault(key, {}).setdefault(content, []).append(commit)

    table: dict[str, list[dict[str, str]]] = {}
    gone: dict[str, list[str]] = {}
    for key in sorted(seen):
        if key not in current:
            # Clé disparue : reprise par l'exercice de la même leçon qui porte son texte.
            lesson = key.rsplit("/", 1)[0] + "/"
            heirs = [k for k in sorted(current) if k.startswith(lesson) and current[k] in seen[key]]
            gone[key] = heirs
            for heir in heirs:
                table.setdefault(heir, []).append({"key": key, "content": current[heir]})
            continue
        for content in sorted(seen[key]):
            if content != current[key]:
                table.setdefault(key, []).append({"key": key, "content": content})
    table = {key: sorted(forms, key=lambda f: (f["key"], f["content"])) for key, forms in sorted(table.items())}

    if report:
        def span(commits: list[str]) -> str:
            ordered = sorted(commits, key=lambda c: c.split()[-1])
            return f"{len(ordered)} commits, de {ordered[0]} à {ordered[-1]}"

        for key, forms in table.items():
            print(f"\n{key}\n  actuel : {current[key]!r}")
            for form in forms:
                print(f"  ancienne forme : {form['key']} {form['content']!r}\n    {span(seen[form['key']][form['content']])}")
        for key, heirs in gone.items():
            print(f"\n{key}\n  disparu du catalogue actuel ; repris par : {', '.join(heirs) or 'aucun exercice'}")
            for content, commits in seen[key].items():
                print(f"  texte : {content!r}\n    {span(commits)}")
        print()
    return table


def write(path: Path, data: dict[str, list], what: str) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=1) + "\n", encoding="utf-8", newline="\n")
    print(f"{path.relative_to(ROOT)} : {len(data)} exercices, {sum(map(len, data.values()))} {what}")


def main() -> int:
    global HISTORY
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--history", type=Path, default=ROOT, help="arbre git dont lire l'historique")
    parser.add_argument("--report", action="store_true", help="liste chaque ancien texte et ses commits")
    args = parser.parse_args()
    HISTORY = args.history.resolve()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")

    instructions = build_instructions()
    if not instructions:
        print("aucune consigne trouvée", file=sys.stderr)
        return 1
    contents = build_contents(args.report)
    write(OUT, instructions, "consignes")
    write(OUT_CONTENT, contents, "anciennes formes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
