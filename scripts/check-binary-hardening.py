"""Exécuter la distribution autonome BinSkim et refuser une analyse incomplète."""
from pathlib import Path
import argparse
import hashlib
import json
import re
import string
import struct
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


# Dérogations nominatives arbitrées par Antoine le 2026-09-22 (audit Store 1.3.0).
# Une alerte n'est acceptée que si sa règle figure ici, si son niveau est `warning` et
# si sa condition est vérifiée sur le binaire analysé ; sinon elle reste bloquante.
# BA2025 (CET) n'y figure pas : /CETCOMPAT est posé sur x64 dans le csproj.
# À réexaminer à la migration vers .NET 10 (fin de support de .NET 8 : 2026-11-10).
MICROSOFT_NATIVE_LIBRARIES = frozenset({
    "bootstrapper.GuardCF.obj",
    "Runtime.ServerGC.GuardCF.lib",
    "Runtime.VxsortEnabled.GuardCF.lib",
    "System.Globalization.Native.Aot.GuardCF.lib",
    "eventpipe-disabled.GuardCF.lib",
    "LIBCMT.lib",
    "libvcruntime.lib",
})
DEROGATIONS = {
    "BA2024": "Spectre : les seuls objets sans /Qspectre sont ceux, précompilés, du runtime NativeAOT et du CRT Microsoft ; le dépôt ne compile aucun C/C++.",
    "BA2026": "SDL : chaque objet C/C++ compté par l'éditeur de liens appartient au runtime NativeAOT ou au CRT Microsoft ; le dépôt ne compile aucun C/C++.",
    "BA2027": "SourceLink : le PDB natif produit par ILC 8 n'en porte pas ; dette de diagnostic, pas une mitigation d'exploitation.",
    "BA6006": "LTCG : recommandation d'optimisation ; les bibliothèques précompilées du runtime ne sont pas en /GL.",
}
SPECTRE_LINE = re.compile(r"^([^,]+),(c|cxx),[0-9.]+ \((.+)\)$")


def spectre_modules(result):
    """Lire la liste des modules sans /Qspectre fournie par BA2024 : [(bibliothèque, nb d'objets)]."""
    arguments = result.get("message", {}).get("arguments", [])
    if len(arguments) < 2:
        raise ValueError("BA2024 sans liste de modules")
    lines = [line for line in arguments[1].splitlines()[1:] if line.strip()]
    modules = []
    for line in lines:
        match = SPECTRE_LINE.match(line.strip())
        if not match:
            raise ValueError("Ligne BA2024 illisible : " + line[:80])
        modules.append((match.group(1), len([o for o in match.group(3).split(",") if o])))
    if not modules:
        raise ValueError("BA2024 sans module")
    return modules


def microsoft_only_objects(results, cxx_objects):
    """Vrai si BA2024 prouve que tous les objets C/C++ du binaire sont ceux de Microsoft."""
    if not isinstance(cxx_objects, int) or cxx_objects <= 0:
        return False
    for result in results:
        if result.get("ruleId") != "BA2024" or result.get("kind", "fail") != "fail":
            continue
        modules = spectre_modules(result)
        return (all(library in MICROSOFT_NATIVE_LIBRARIES for library, _ in modules)
                and sum(count for _, count in modules) == cxx_objects)
    return False


def effective_level(result, rules):
    """Niveau SARIF effectif (§3.27.10) : celui du résultat ; `none` hors `fail` ; sinon
    celui de sa règle, sinon `warning`."""
    if "level" in result:
        return result["level"]
    if result.get("kind", "fail") != "fail":
        return "none"
    rule = rules.get(result.get("ruleId"), {})
    return rule.get("defaultConfiguration", {}).get("level", "warning")


def derogation_holds(result, results, context, rules=None):
    """Condition propre à chaque règle dérogée ; rien n'est accepté sans contexte."""
    rule = result.get("ruleId")
    if context is None or rule not in DEROGATIONS or effective_level(result, rules or {}) != "warning":
        return False
    if rule in ("BA2024", "BA2026"):
        return microsoft_only_objects(results, context.get("cxx_objects"))
    return True


def vc_feature_counts(binary):
    """Compteurs IMAGE_DEBUG_TYPE_VC_FEATURE écrits par l'éditeur de liens (lus par BA2026)."""
    data = Path(binary).read_bytes()
    pe = struct.unpack_from("<I", data, 0x3C)[0]
    if data[pe:pe + 4] != b"PE\0\0":
        raise ValueError("En-tête PE introuvable")
    sections_count = struct.unpack_from("<H", data, pe + 6)[0]
    optional_size = struct.unpack_from("<H", data, pe + 20)[0]
    optional = pe + 24
    if struct.unpack_from("<H", data, optional)[0] != 0x20B:
        raise ValueError("PE32+ attendu")
    debug_rva, debug_size = struct.unpack_from("<II", data, optional + 112 + 6 * 8)
    sections = []
    for i in range(sections_count):
        offset = optional + optional_size + 40 * i
        virtual_size, virtual_address, raw_size, raw_pointer = struct.unpack_from("<IIII", data, offset + 8)
        sections.append((virtual_address, max(virtual_size, raw_size), raw_pointer))

    def file_offset(rva):
        for address, size, pointer in sections:
            if address <= rva < address + size:
                return rva - address + pointer
        raise ValueError("RVA hors sections")

    start = file_offset(debug_rva)
    for entry in range(debug_size // 28):
        kind, size, _, pointer = struct.unpack_from("<IIII", data, start + 28 * entry + 12)
        if kind == 12 and size >= 20:
            pre_vc11, cxx, gs, sdl, guard_n = struct.unpack_from("<IIIII", data, pointer)
            return {"pre_vc11": pre_vc11, "cxx": cxx, "gs": gs, "sdl": sdl, "guard_n": guard_n}
    raise ValueError("Entrée VC_FEATURE absente")


def validate_report(report, context=None):
    """Une absence d'erreur ne suffit pas : exiger une analyse terminée et des résultats.

    Renvoie les dérogations appliquées, pour qu'elles soient imprimées et conservées."""
    runs = report.get("runs", [])
    if not runs:
        raise ValueError("SARIF sans analyse")
    accepted = []
    for run in runs:
        invocations = run.get("invocations", [])
        if not invocations or any(i.get("executionSuccessful") is not True for i in invocations):
            raise ValueError("Analyse BinSkim incomplète")
        results = run.get("results", [])
        if not results or not any(r.get("kind") == "pass" for r in results):
            raise ValueError("Aucun contrôle effectivement réussi")
        rules = {rule["id"]: rule for rule in run.get("tool", {}).get("driver", {}).get("rules", [])}
        failed = [r for r in results if r.get("kind", "fail") == "fail" or effective_level(r, rules) == "error"]
        blocking = [r for r in failed if not derogation_holds(r, results, context, rules)]
        if blocking:
            raise ValueError("Contrôles en échec : " + ", ".join(r.get("ruleId", "?") for r in blocking))
        if any(not r.get("message", {}).get("text", "").strip() for r in results):
            raise ValueError("Message SARIF textuel absent")
        accepted += [{"rule": r["ruleId"], "justification": DEROGATIONS[r["ruleId"]]} for r in failed]
    return accepted


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
                counts = vc_feature_counts(binary)
                accepted = validate_report(data, {"cxx_objects": counts["cxx"]})
                (args.output / f"derogations-{arch}.json").write_text(
                    json.dumps({"binskim": VERSION, "vc_feature": counts, "accepted": accepted},
                               ensure_ascii=False, indent=2), encoding="utf-8")
                # BinSkim sort en 1 dès qu'un résultat fail existe, même dérogé : le code
                # n'est admis que si chaque échec a été accepté ci-dessus.
                if process.returncode not in (0, 1) or (process.returncode == 1 and not accepted):
                    raise ValueError(f"Code de sortie {process.returncode}")
                for item in accepted:
                    print(f"{arch} : {item['rule']} dérogé — {item['justification']}")
                print(f"{arch} : analyse complète, aucun contrôle bloquant ({len(accepted)} dérogation(s))")
            except (ValueError, KeyError) as error:
                errors.append(f"{arch} : {error}")
        if errors:
            raise ValueError(" ; ".join(errors))


if __name__ == "__main__":
    main()
