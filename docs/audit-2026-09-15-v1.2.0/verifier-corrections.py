"""Conserver les preuves fraîches des corrections, sans lancer ni installer l'application."""
from pathlib import Path
import datetime as dt
import hashlib
import json
import os
import shutil
import struct
import subprocess
import sys
import xml.etree.ElementTree as ET

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent
GIT = ["git", "-c", f"safe.directory={ROOT.as_posix()}"]
DOTNET = shutil.which("dotnet") or r"C:\Program Files\dotnet\dotnet.exe"
report = {"captured_utc": dt.datetime.now(dt.timezone.utc).isoformat(), "checks": []}


def run(label, args, expected=(0,)):
    result = subprocess.run(args, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=180)
    (OUT / (label + ".log")).write_text(result.stdout + result.stderr, encoding="utf-8", newline="\n")
    report["checks"].append({"check": label, "command": args, "exit": result.returncode})
    print(label, "exit", result.returncode, "\n" + "\n".join((result.stdout + result.stderr).splitlines()[-4:]))
    assert result.returncode in expected, label
    return result.stdout


run("diff-corrections", GIT + ["-c", "core.whitespace=cr-at-eol", "diff", "--check"])
run("layout-corrections", [sys.executable, "scripts/validate-layout.py"])
run("identite-corrections", [sys.executable, "scripts/list-identity-literals.py"])
run("documents-corrections", [sys.executable, "scripts/check-doc-versions.py", "--release"], expected=(0, 1))
run("scripts-complets", [sys.executable, "-m", "unittest", "discover", "-s", "scripts/tests", "-q"])
run("environnement-corrections", [DOTNET, "--info"])
for arch in ("x64", "arm64"):
    run("compilation-native-" + arch,
        [DOTNET, "publish", "src/AZERTYGlobal.csproj", "-c", "Release", "-r", "win-" + arch, "--no-restore"])

report["tests_dotnet"] = {}
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
for name in ("core-complet", "windows-complet", "application-complet"):
    trx = ET.parse(OUT / "test-results" / (name + ".trx"))
    counts = trx.find(".//t:Counters", ns).attrib
    assert int(counts["failed"]) == 0 and int(counts["passed"]) > 0, (name, counts)
    report["tests_dotnet"][name] = counts

baseline = json.loads((OUT / "inventaire.json").read_text(encoding="utf-8"))
report["audit_baseline"] = baseline["head"]
report["changed_from_audit"] = []
for file in baseline["files"]:
    raw = (ROOT / file["path"]).read_bytes()
    if hashlib.sha256(raw).hexdigest() != file["sha256"]:
        report["changed_from_audit"].append(file["path"])
report["unchanged_from_audit"] = len(baseline["files"]) - len(report["changed_from_audit"])

tracked = subprocess.check_output(GIT + ["diff", "--name-only", "-z"], cwd=ROOT).decode("utf-8").split("\0")
untracked = subprocess.check_output(GIT + ["ls-files", "--others", "--exclude-standard", "-z", "--", "src", "scripts"], cwd=ROOT).decode("utf-8").split("\0")
report["sources"] = []
for path in sorted(set(tracked + untracked) - {""}):
    raw = (ROOT / path).read_bytes()
    raw.decode("utf-8")
    record = {"path": path, "sha256": hashlib.sha256(raw).hexdigest(),
              "crlf": raw.count(b"\r\n"), "lf": raw.count(b"\n") - raw.count(b"\r\n"),
              "bom": raw.startswith(b"\xef\xbb\xbf")}
    if path in tracked:
        old = subprocess.check_output(GIT + ["show", "HEAD:" + path], cwd=ROOT)
        assert record["bom"] == old.startswith(b"\xef\xbb\xbf"), path
        if b"\r\n" not in old:
            assert record["crlf"] == 0, path
        elif old.count(b"\n") == old.count(b"\r\n"):
            assert record["lf"] == 0, path
    elif path.endswith(".cs"):
        assert not record["bom"] and record["crlf"] == 0, path
    assert all(line.rstrip(b"\r\n").rstrip(b" \t") == line.rstrip(b"\r\n") for line in raw.splitlines(keepends=True)), path
    report["sources"].append(record)

report["native_outputs"] = []
for arch, expected_machine in (("x64", 0x8664), ("arm64", 0xAA64)):
    path = ROOT / f"src/bin/Release/net8.0-windows10.0.17763.0/win-{arch}/publish/AZERTY Global.exe"
    raw = path.read_bytes()
    pe = struct.unpack_from("<I", raw, 0x3C)[0]
    assert raw[pe:pe + 4] == b"PE\0\0"
    machine = struct.unpack_from("<H", raw, pe + 4)[0]
    assert machine == expected_machine
    report["native_outputs"].append({"path": path.relative_to(ROOT).as_posix(), "architecture": arch,
                                     "machine": hex(machine), "bytes": len(raw),
                                     "sha256": hashlib.sha256(raw).hexdigest()})

assets = json.loads((ROOT / "src/obj/project.assets.json").read_text(encoding="utf-8"))
report["resolved_product_libraries"] = list(assets["libraries"])
(OUT / "verification-corrections.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
print("Preuves enregistrées ; tests .NET :", {name: counts["passed"] for name, counts in report["tests_dotnet"].items()})

