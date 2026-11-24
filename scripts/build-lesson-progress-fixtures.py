"""Génère les progressions témoins de src/AZERTYGlobal.Tests/Fixtures : le fichier
lessons-progress.json qu'une ancienne version de l'app écrit quand tous ses exercices sont
réussis, recalculé depuis les sources de cette version dans l'historique git.

Pourquoi : la mise à jour vers la 1.3.0 doit garder ces réussites (audit de régression du
10/11/2026, RA2). Les empreintes sont calculées ici, en Python, sans passer par le code de
l'app : les tests (ReprisePublieeTests) comparent donc l'app à une source indépendante.

Trois témoins :
- 039733c, français : sources du 27/07/2026 (lessons.json identique à celui de la 1.0.0,
  ed7c731) ; consignes de l'Initiation en dur, sans anglais ;
- 452aab0, français et anglais : sources du 15/08/2026, base de la 1.2.0.
La 1.1.0 publiée (23/07/2026) a été compilée hors dépôt : aucun commit ne porte son numéro.

Les témoins sont figés. À relancer seulement pour les réparer.

Usage (depuis la racine du dépôt) :
  python scripts/build-lesson-progress-fixtures.py [--history <autre arbre git>]
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT_DIR = ROOT / "src" / "AZERTYGlobal.Tests" / "Fixtures"
INIT = "initiation/premiers-pas"

CS_STRING = r'"((?:[^"\\]|\\.)*)"'
INIT_EXERCISE_RE = re.compile(r"InitiationExercise\(\s*(\d+)\s*,\s*(?:L\.Lessons_Init\d+|" + CS_STRING + r")\s*,\s*"
                              + CS_STRING + r"\s*\)")
INIT_LABEL_RE = re.compile(r"Lessons_Init(\d)\s*=>\s*T\(\s*" + CS_STRING + r"\s*,\s*" + CS_STRING + r"\s*\)", re.S)

# (commit, langue) de chaque témoin.
FIXTURES = (("039733c", "fr"), ("452aab0", "fr"), ("452aab0", "en"))


def show(history: Path, commit: str, path: str) -> str | None:
    result = subprocess.run(["git", "-C", str(history), "show", f"{commit}:{path}"],
                            capture_output=True, text=True, encoding="utf-8")
    return result.stdout if result.returncode == 0 else None


def cs_unescape(s: str) -> str:
    return json.loads('"' + s + '"')


def legacy_hash(kind: str, instruction: str, content: str) -> str:
    """LessonCatalogLoader.ComputeExerciseHash des versions d'avant le 09/11/2026."""
    normalized = "\u001f".join((kind, instruction, content.replace("\r\n", "\n")))
    return hashlib.sha256(normalized.encode("utf-8")).hexdigest()


def exercises(history: Path, commit: str, language: str) -> list[tuple[str, str, str, str]]:
    """(clé, type, consigne affichée dans la langue, texte) de chaque exercice de la version."""
    out = []
    catalog = show(history, commit, "src/LessonCatalog.cs") or ""
    labels = {}
    for m in INIT_LABEL_RE.finditer(show(history, commit, "src/Localization/L.Lessons.cs") or ""):
        labels[int(m.group(1))] = cs_unescape(m.group(3 if language == "en" else 2))
    for m in INIT_EXERCISE_RE.finditer(catalog):
        rank = int(m.group(1))
        instruction = cs_unescape(m.group(2)) if m.group(2) is not None else labels[rank]
        out.append((f"{INIT}/{rank}", "practice", instruction, cs_unescape(m.group(3))))

    doc = json.loads((show(history, commit, "src/lessons.json") or "").lstrip("﻿"))
    for module in doc["modules"]:
        for lesson in module["lessons"]:
            for i, ex in enumerate(lesson["exercises"]):
                # PickText : l'anglais seulement s'il existe.
                instruction = ex.get("instructionEn") if language == "en" and ex.get("instructionEn") else ex["instruction"]
                out.append((f"{module['id']}/{lesson['id']}/{i}", ex["type"], instruction, ex["content"]))
    return out


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--history", type=Path, default=ROOT, help="arbre git dont lire l'historique")
    history = parser.parse_args().history.resolve()

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for commit, language in FIXTURES:
        items = exercises(history, commit, language)
        entries = {}
        for key, kind, instruction, content in items:
            digest = legacy_hash(kind, instruction, content)
            entries[f"{key}/{digest}"] = {
                "hash": digest,
                "completed": True,
                "successfulAttempts": 2,
                "bestWpm": 30,
                "bestAccuracyPercent": 95,
                "bestSeconds": 12.5,
                "lastCompletedUtc": "2026-08-20T10:00:00.0000000+00:00",
                "hintsUsed": 0,
            }
        last_module, last_lesson, last_rank = items[-1][0].split("/")
        data = {
            "version": 1,
            "lastModuleId": last_module,
            "lastLessonId": last_lesson,
            "lastExerciseIndex": int(last_rank),
            "onboardingSyncedMaxStep": sum(1 for key, *_ in items if key.startswith(INIT + "/")),
            "exercises": dict(sorted(entries.items())),
        }
        path = OUT_DIR / f"lessons-progress-{commit}-{language}.json"
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
        print(f"{path.relative_to(ROOT)} : {len(entries)} exercices réussis")
    return 0


if __name__ == "__main__":
    sys.exit(main())
