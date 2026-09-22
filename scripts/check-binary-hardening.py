"""Exécuter la distribution autonome BinSkim et refuser une analyse incomplète."""
from pathlib import Path
import argparse
import hashlib
import json
import string
import subprocess
import sys
import tempfile
import urllib.request
import zipfile

VERSION = "4.4.9.11"
SHA256 = "69678989cbc273b5b50fcf98fb0fd978e1e35a3f844acb24254b31f0ce90c447"
URL = f"https://api.nuget.org/v3-flatcontainer/microsoft.codeanalysis.binskim/{VERSION}/microsoft.codeanalysis.binskim.{VERSION}.nupkg"
ROOT = Path(__file__).resolve().parents[1]


def inline_result_messages(report):
    """GitHub exige message.text ; résoudre les modèles SARIF sans changer les verdicts."""
    for run in report.get("runs", []):
        driver = run.get("tool", {}).get("driver", {})
        rules = {rule["id"]: rule for rule in driver.get("rules", [])}
        for result in run.get("results", []):
            message = result.get("message", {})
            if message.get("text", "").strip():
                continue
            rule = rules.get(result.get("ruleId"), {})
            template = rule.get("messageStrings", {}).get(message.get("id"))
            if template is None:
                template = driver.get("globalMessageStrings", {}).get(message.get("id"))
            # BinSkim 4.4.9.11 omet ce modèle commun dans BA4002 (ELF/Mach-O).
            # Réutiliser uniquement le texte unanime déjà fourni par ses autres règles.
            if template is None and (result.get("ruleId"), message.get("id")) == ("BA4002", "NotApplicable_InvalidMetadata"):
                common = {rule.get("messageStrings", {}).get(message["id"], {}).get("text")
                          for rule in rules.values()}
                common.discard(None)
                if len(common) == 1:
                    template = {"text": common.pop()}
            if not template or not template.get("text"):
                raise ValueError("Modèle SARIF introuvable : " + str(message.get("id")))
            text = template["text"]
            for _, field, spec, conversion in string.Formatter().parse(text):
                if field is not None and (not field.isdecimal() or spec or conversion):
                    raise ValueError("Format SARIF non pris en charge : " + text)
            try:
                message["text"] = text.format(*message.get("arguments", []))
            except (IndexError, ValueError) as error:
                raise ValueError("Arguments SARIF incompatibles") from error


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
        failures = [r.get("ruleId", "?") for r in results if r.get("kind", "fail") == "fail" or r.get("level") == "error"]
        if failures:
            raise ValueError("Contrôles en échec : " + ", ".join(failures))
        if any(not r.get("message", {}).get("text", "").strip() for r in results):
            raise ValueError("Message SARIF textuel absent")


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
            raw_report = report.with_suffix(".raw.json")
            # Le rapport doit provenir de CET appel, jamais d'une exécution antérieure.
            if report.exists():
                report.unlink()
            if raw_report.exists():
                raw_report.unlink()
            process = subprocess.run([str(tool), "analyze", str(binary), "--output", str(raw_report),
                                      "--disable-telemetry",
                                      "--kind", "Fail;Pass;Review;Open;NotApplicable"],
                                     stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=180)
            (args.output / f"binskim-{arch}.log").write_bytes(process.stdout)
            try:
                if not raw_report.is_file():
                    raise ValueError("Aucun rapport produit")
                data = json.loads(raw_report.read_text(encoding="utf-8-sig"))
                inline_result_messages(data)
                # Aucun fichier uploadable si la conversion a échoué. Les vrais
                # verdicts fail restent uploadables et sont refusés juste après.
                report.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
                validate_report(data)
                if process.returncode != 0:
                    raise ValueError(f"Code de sortie {process.returncode}")
                print(f"{arch} : analyse complète, aucun contrôle en échec")
            except (ValueError, KeyError) as error:
                errors.append(f"{arch} : {error}")
        if errors:
            raise ValueError(" ; ".join(errors))


if __name__ == "__main__":
    main()
