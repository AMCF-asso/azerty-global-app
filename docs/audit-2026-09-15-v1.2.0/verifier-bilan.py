"""Vérifier les liens du bilan et l'absence de dérive depuis les validations."""
from pathlib import Path
import datetime as dt
import hashlib
import json
import re
import sys

sys.stdout.reconfigure(encoding="utf-8")
HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
proof = json.loads((HERE / "verification-corrections.json").read_text(encoding="utf-8"))
assert sum(int(c["passed"]) for c in proof["tests_dotnet"].values()) == 493
assert all(int(c["failed"]) == 0 and int(c["total"]) == int(c["passed"])
           for c in proof["tests_dotnet"].values())
python_log = (HERE / "scripts-complets.log").read_text(encoding="utf-8")
assert re.search(r"Ran 116 tests\b", python_log) and "\nOK\n" in python_log
for entry in proof["sources"] + proof["native_outputs"]:
    assert hashlib.sha256((ROOT / entry["path"]).read_bytes()).hexdigest() == entry["sha256"], entry["path"]

links = set()
artifacts = []
for name in ("corrections.md", "recette-vm.md"):
    path = HERE / name
    raw = path.read_bytes()
    assert not raw.startswith(b"\xef\xbb\xbf") and b"\r\n" not in raw
    content = raw.decode("utf-8")
    assert all(line == line.rstrip(" \t") for line in content.splitlines())
    for target in re.findall(r"\]\(<([^>]+)>\)", content):
        match = re.fullmatch(r"(.*?)(?::(\d+))?", target)
        file = Path(match[1])
        assert file.is_absolute() and file.is_file(), target
        if match[2]:
            assert 1 <= int(match[2]) <= len(file.read_text(encoding="utf-8-sig").splitlines()), target
        links.add(target)
    artifacts.append({"path": name, "sha256": hashlib.sha256(raw).hexdigest()})
result = {
    "captured_utc": dt.datetime.now(dt.timezone.utc).isoformat(),
    "tests_passed": 609,
    "source_hashes_verified": len(proof["sources"]),
    "native_hashes_verified": len(proof["native_outputs"]),
    "local_links_verified": len(links),
    "artifacts": artifacts,
}
(HERE / "verification-bilan.json").write_text(
    json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
print(json.dumps(result, ensure_ascii=False, indent=2))

