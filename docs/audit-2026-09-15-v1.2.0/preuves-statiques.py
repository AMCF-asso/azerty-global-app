"""Inventaire reproductible : lit les sources, n'exécute ni l'app ni ses tests.

Écrit uniquement inventaire.json à côté de ce fichier. Aucun build, réseau,
registre Windows ou fichier de configuration utilisateur n'est consulté.
"""
from pathlib import Path
from collections import Counter
from datetime import datetime, timezone
import hashlib
import json
import subprocess
import xml.etree.ElementTree as ET

OUT = Path(__file__).resolve().parent
REPO = OUT.parent.parent


def git(*args):
    return subprocess.check_output(
        ["git", "-c", "safe.directory=" + REPO.as_posix(), *args],
        cwd=REPO,
    ).decode("utf-8")


def group(name):
    if name.startswith("src/"):
        if ".Tests/" in name or "/TestSupport/" in name:
            return "tests_application"
        return "application"
    if name.startswith("scripts/"):
        return "scripts_livraison"
    if name.startswith("msix/"):
        return "paquet_store"
    if name.startswith(".github/workflows/"):
        return "ci"
    return None


files = []
syntax_checks = []
dependencies = []
for name in git("ls-files", "-z").split("\0"):
    category = group(name)
    if not name or category is None:
        continue
    path = REPO / name
    if not path.is_file():
        files.append({"path": name, "group": category, "missing": True})
        continue
    data = path.read_bytes()
    item = {
        "path": name, "group": category,
        "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest(),
    }
    if path.suffix.lower() in {".cs", ".csproj", ".json", ".xml", ".yml", ".ps1", ".py", ".md", ".manifest"}:
        item["lines"] = len(data.decode("utf-8-sig").splitlines())
    files.append(item)
    if category == "application" and path.suffix in {".json", ".csproj", ".manifest"} or name == "msix/AppxManifest.xml":
        try:
            parsed = json.loads(data) if path.suffix == ".json" else ET.fromstring(data)
            syntax_checks.append({"path": name, "status": "OK", "check": "syntaxe uniquement"})
        except (ValueError, ET.ParseError) as error:
            syntax_checks.append({"path": name, "status": "ECHEC", "error": str(error)})
    if path.suffix == ".csproj":
        tree = ET.fromstring(data)
        dependencies.append({
            "path": name,
            "target": tree.findtext(".//TargetFramework"),
            "version": tree.findtext(".//Version"),
            "packages_explicit": [e.attrib for e in tree.findall(".//PackageReference")],
            "resources": [e.attrib for e in tree.findall(".//EmbeddedResource")],
        })

canonical = REPO.parent / "website" / "data" / "AZERTY Global.json"
snapshot = REPO / "src" / "AZERTY Global 2026.json"
resource_match = {
    "canonical": canonical.as_posix(),
    "snapshot": snapshot.relative_to(REPO).as_posix(),
    "bytes_identical": canonical.read_bytes() == snapshot.read_bytes(),
    "json_identical": json.loads(canonical.read_bytes()) == json.loads(snapshot.read_bytes()),
    "canonical_sha256": hashlib.sha256(canonical.read_bytes()).hexdigest(),
    "snapshot_sha256": hashlib.sha256(snapshot.read_bytes()).hexdigest(),
}
additional_resources = []
for name in ("character-index.json", "lessons.json"):
    origin = REPO.parent / "website" / "tester" / name
    destination = REPO / "src" / name
    additional_resources.append({
        "canonical": origin.as_posix(), "snapshot": destination.relative_to(REPO).as_posix(),
        "bytes_identical": origin.read_bytes() == destination.read_bytes(),
        "canonical_sha256": hashlib.sha256(origin.read_bytes()).hexdigest(),
        "snapshot_sha256": hashlib.sha256(destination.read_bytes()).hexdigest(),
    })
character_index = json.loads((REPO / "src/character-index.json").read_bytes())
corpus = json.loads((REPO / "src/defi-corpus.json").read_bytes())
extracts = corpus["extracts"]
data_invariants = {
    "index_total_matches_entries": character_index["totalCharacters"] == len(character_index["characters"]),
    "corpus_count_matches_entries": corpus["count"] == len(extracts),
    "corpus_unique_ids": len({entry["id"] for entry in extracts}) == len(extracts),
    "corpus_nonempty_texts": all(isinstance(entry["text"], str) and entry["text"].strip() for entry in extracts),
    "sequence_french_pools_nonempty": {
        category: any(entry.get("lang") == "fr" and category in entry.get("cats", []) for entry in extracts)
        for category in ("caps", "point", "at", "prog", "accents")
    },
}
runtime_cs = [f for f in files if f["group"] == "application" and f["path"].endswith(".cs")]
test_cs = [f for f in files if f["group"] == "tests_application" and f["path"].endswith(".cs")]
result = {
    "captured_utc": datetime.now(timezone.utc).isoformat(),
    "head": git("rev-parse", "HEAD").strip(),
    "branch": git("branch", "--show-current").strip(),
    "method": "Lecture et analyse statique. Aucun test applicatif, build, DLL ou EXE exécuté.",
    "counts": dict(Counter(f["group"] for f in files)),
    "runtime_cs_files": len(runtime_cs),
    "runtime_cs_lines": sum(f.get("lines", 0) for f in runtime_cs),
    "test_cs_files": len(test_cs),
    "test_cs_lines": sum(f.get("lines", 0) for f in test_cs),
    "syntax_checks": syntax_checks,
    "layout_resource_match": resource_match,
    "additional_resource_matches": additional_resources,
    "data_invariants": data_invariants,
    "projects": dependencies,
    "files": files,
}
(OUT / "inventaire.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
print(json.dumps({k: result[k] for k in ["head", "branch", "counts", "runtime_cs_files", "runtime_cs_lines", "test_cs_files", "test_cs_lines", "syntax_checks", "layout_resource_match", "additional_resource_matches", "data_invariants"]}, ensure_ascii=False, indent=2))
