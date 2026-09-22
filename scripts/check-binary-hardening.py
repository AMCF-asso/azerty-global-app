"""Exécuter la distribution autonome BinSkim et refuser une analyse incomplète."""
from pathlib import Path
import argparse
import hashlib
import json
import subprocess
import sys
import tempfile
import urllib.request
import zipfile

VERSION = "4.4.9.11"
SHA256 = "69678989cbc273b5b50fcf98fb0fd978e1e35a3f844acb24254b31f0ce90c447"
URL = f"https://api.nuget.org/v3-flatcontainer/microsoft.codeanalysis.binskim/{VERSION}/microsoft.codeanalysis.binskim.{VERSION}.nupkg"
ROOT = Path(__file__).resolve().parents[1]


def validate_report(report):
    """Une absence d'erreur ne suffit pas : exiger une analyse terminée et des résultats."""
    runs = report.get("runs", [])
    if not runs:
        raise ValueError("SARIF sans analyse")
    for run in runs:
        invocations = run.get("invocations", [])
        if not invocations or any(i.get("executionSuccessful") is not True for i in invocations):
            raise ValueError("Analyse BinSkim incomplète")
        results = run.get("results", [])
        if not results or not any(r.get("kind") == "pass" for r in results):
            raise ValueError("Aucun contrôle effectivement réussi")
        failures = [r.get("ruleId", "?") for r in results if r.get("kind") == "fail" or r.get("level") == "error"]
        if failures:
            raise ValueError("Contrôles en échec : " + ", ".join(failures))


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", type=Path, help="Distribution officielle déjà téléchargée (empreinte vérifiée)")
    parser.add_argument("--output", type=Path, default=ROOT / "binskim-sarif")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="azg-binskim-") as temporary:
        scratch = Path(temporary)
        if args.package:
            package = args.package.read_bytes()
        else:
            with urllib.request.urlopen(URL, timeout=90) as response:
                package = response.read()
        if hashlib.sha256(package).hexdigest() != SHA256:
            raise ValueError("Empreinte de la distribution BinSkim inattendue")
        archive = scratch / "binskim.zip"
        archive.write_bytes(package)
        with zipfile.ZipFile(archive) as z:
            for name in z.namelist():
                if not name.startswith("tools/net9.0/win-x64/") or name.endswith("/"):
                    continue
                target = (scratch / name).resolve()
                if not target.is_relative_to(scratch.resolve()):
                    raise ValueError("Chemin ZIP invalide")
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(z.read(name))
        tool = scratch / "tools/net9.0/win-x64/BinSkim.exe"
        errors = []
        for arch in ("x64", "arm64"):
            binary = ROOT / f"src/bin/Release/net8.0-windows10.0.17763.0/win-{arch}/publish/AZERTY Global.exe"
            if not binary.is_file() or not binary.with_suffix(".pdb").is_file():
                raise ValueError(f"Publication {arch} ou son PDB absent")
            report = args.output / f"binskim-{arch}.sarif"
            # Le rapport doit provenir de CET appel, jamais d'une exécution antérieure.
            if report.exists():
                report.unlink()
            process = subprocess.run([str(tool), "analyze", str(binary), "--output", str(report),
                                      "--disable-telemetry", "--kind", "Fail;Pass;Review;Open;NotApplicable"],
                                     stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=180)
            (args.output / f"binskim-{arch}.log").write_bytes(process.stdout)
            try:
                if not report.is_file():
                    raise ValueError("Aucun rapport produit")
                validate_report(json.loads(report.read_text(encoding="utf-8-sig")))
                if process.returncode != 0:
                    raise ValueError(f"Code de sortie {process.returncode}")
                print(f"{arch} : analyse complète, aucun contrôle en échec")
            except (ValueError, KeyError) as error:
                errors.append(f"{arch} : {error}")
        if errors:
            raise ValueError(" ; ".join(errors))


if __name__ == "__main__":
    main()
