"""Vérifie les livrables de l'audit sans exécuter l'application ni ses tests."""
from pathlib import Path
import hashlib
import json
import re
import subprocess

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
issues = []
links = 0
for name in ("rapport.md", "recette-vm.md"):
    data = (HERE / name).read_bytes()
    if data.startswith(b"\xef\xbb\xbf") or b"\r" in data:
        issues.append(f"{name}: BOM ou fin de ligne CR")
    content = data.decode("utf-8")
    for target in re.findall(r"\]\(<(D:/[^>]+)>\)", content):
        match = re.match(r"^(.*?):([0-9]+)$", target)
        path = Path(match[1] if match else target)
        links += 1
        if not path.is_file():
            issues.append(f"Lien absent: {target}")
        elif match and int(match[2]) > len(path.read_text(encoding="utf-8-sig").splitlines()):
            issues.append(f"Ligne hors fichier: {target}")
    if "Document de travail" in content or "revue en cours" in content.lower() or "à confirmer par la revue contradictoire" in content.lower():
        issues.append(f"{name}: passage encore provisoire")

inventory = json.loads((HERE / "inventaire.json").read_text(encoding="utf-8"))
for item in inventory["files"]:
    path = REPO / item["path"]
    if item.get("missing") or not path.is_file():
        issues.append(f"Source absente: {item['path']}")
    elif hashlib.sha256(path.read_bytes()).hexdigest() != item["sha256"]:
        issues.append(f"Source modifiée après inventaire: {item['path']}")

head = subprocess.check_output(["git", "-c", "safe.directory=" + REPO.as_posix(), "rev-parse", "HEAD"], cwd=REPO).decode().strip()
if head != inventory["head"]:
    issues.append("Révision différente de l'inventaire")
json.loads((HERE / "controles-statiques.json").read_text(encoding="utf-8"))
print(json.dumps({"links_checked": links, "source_files_unchanged": len(inventory["files"]), "head": head, "issues": issues}, ensure_ascii=False, indent=2))
raise SystemExit(bool(issues))
