"""Témoin de mutation des gardes du socle de thème (CH0, refonte graphique v1.2.0).

Un garde-fou de ce dépôt n'est recevable qu'avec la preuve qu'il rougit. Les gardes de
`ThemeTests` portent sur des couleurs, et une couleur fausse produit toujours une couleur
valide : rien dans le compilateur, l'analyseur ou la relecture visuelle ne les attrape. Ce
script mute `src/Theme.cs` sur trois axes, lance la suite après chaque mutation, restaure, et
n'accepte le résultat que si chaque mutation a fait tomber au moins un test.

Les trois mutations, choisies pour être exactement les erreurs que l'histoire du dépôt a
réellement produites :

  1. un octet de trop sur un jeton — la dérive silencieuse d'une couleur ;
  2. les octets d'un jeton saisis dans l'ordre de GDI plutôt que dans celui de la charte —
     c'est ainsi qu'est né l'orange #D47800, qui n'est que le bleu #0078D4 à octets inversés ;
  3. une nuance inventée, absente de la charte.

⚠️ Ce script écrit dans `src/Theme.cs` et le restaure ensuite. Il refuse de démarrer si le
fichier porte déjà des modifications non commitées, faute de quoi une interruption laisserait
un état mutant indistinguable d'un travail en cours.

Usage : python docs/refonte-ui/witness-theme-ch0.py
"""
import pathlib
import re
import subprocess
import sys

for stream in (sys.stdout, sys.stderr):
    reconfigure = getattr(stream, "reconfigure", None)
    if reconfigure is not None:
        try:
            reconfigure(encoding="utf-8", errors="backslashreplace")
        except (OSError, ValueError):
            pass

REPO = pathlib.Path(__file__).resolve().parents[2]
THEME = REPO / "src" / "Theme.cs"
SUITE = REPO / "src" / "AZERTYGlobal.Tests" / "AZERTYGlobal.Tests.csproj"

# Configuration : sur ce poste, Smart App Control mure les binaires Debug fraîchement
# compilés (mesuré le 2026-08-28, l'inverse du 2026-08-24). Release passe. Le verdict est
# par binaire et change avec le temps : si les deux sont murés, la CI du dépôt est la voie.
CONFIGURATION = "Release"

MUTATIONS = [
    (
        "un octet de trop sur action-fond clair",
        "        ActionFill: Rgb(0xE3, 0xE9, 0xFC),",
        "        ActionFill: Rgb(0xE3, 0xE9, 0xFB),",
    ),
    (
        "octets de l'action claire saisis dans l'ordre de GDI",
        "        Action: Rgb(0x1A, 0x3E, 0xF2),\n        Success: Rgb(0x18, 0x63, 0x39),",
        "        Action: Rgb(0xF2, 0x3E, 0x1A),\n        Success: Rgb(0x18, 0x63, 0x39),",
    ),
    (
        "nuance inventee en bordure sombre",
        "        Border: Rgb(0x45, 0x3D, 0x30),\n        Action: Rgb(0x8F, 0xA6, 0xFF),",
        "        Border: Rgb(0x50, 0x48, 0x3A),\n        Action: Rgb(0x8F, 0xA6, 0xFF),",
    ),
]


def suite_verdict():
    """Lance la suite et rend (echecs, reussis, lignes d'echec).

    Toutes les lignes d'echec sont rendues, jamais la premiere seule : une mutation qui fait
    tomber le bon garde et un plantage collateral se lirait sinon a l'envers (lecon du
    2026-08-20, lot F).
    """
    run = subprocess.run(
        ["dotnet", "test", str(SUITE), "-c", CONFIGURATION, "--nologo"],
        cwd=REPO, capture_output=True, text=True, encoding="utf-8", errors="replace")
    out = (run.stdout or "") + (run.stderr or "")

    failed = passed = None
    tombes = []
    for line in out.splitlines():
        stripped = line.strip()
        if stripped.startswith("Failed ") or stripped.startswith("  Failed "):
            tombes.append(stripped)
        if "Failed:" in line and "Passed:" in line and "Total:" in line:
            # Regex plutot qu'un decoupage sur les virgules : le premier compteur est colle
            # au verdict (« Passed!  - Failed:     0, ... »), donc le premier morceau ne
            # commence pas par « Failed: ».
            compteurs = re.search(r"Failed:\s*(\d+).*?Passed:\s*(\d+)", line)
            if compteurs is not None:
                failed = int(compteurs.group(1))
                passed = int(compteurs.group(2))

    if failed is None:
        print(out[-2000:])
        sys.exit("compteurs illisibles — la suite n'a probablement pas tourne")

    return failed, passed, sorted(set(tombes))


def main():
    dirty = subprocess.run(["git", "status", "--porcelain", "--", "src/Theme.cs"],
                           cwd=REPO, capture_output=True, text=True).stdout.strip()
    if dirty:
        sys.exit("src/Theme.cs porte des modifications non commitees — commiter avant, "
                 "sinon une interruption laisserait un mutant indistinguable du travail.")

    original = THEME.read_bytes()
    print(f"Configuration : {CONFIGURATION}")
    print("Reference — suite non mutee :")
    failed, passed, _ = suite_verdict()
    print(f"  {passed} reussis, {failed} echecs")
    if failed != 0:
        sys.exit("la suite est deja rouge : le temoin ne prouverait rien")
    reference = passed

    resultats = []
    try:
        for label, old, new in MUTATIONS:
            text = original.decode("utf-8")
            if text.count(old) != 1:
                sys.exit(f"ancre absente ou multiple pour la mutation « {label} »")
            THEME.write_bytes(text.replace(old, new).encode("utf-8"))

            print(f"\nMutation — {label}")
            failed, passed, tombes = suite_verdict()
            print(f"  {passed} reussis, {failed} echecs")
            for ligne in tombes:
                print(f"    {ligne}")
            resultats.append((label, failed))
    finally:
        THEME.write_bytes(original)

    print("\nRestauration — suite non mutee :")
    failed, passed, _ = suite_verdict()
    print(f"  {passed} reussis, {failed} echecs")

    print("\nVerdict :")
    ok = failed == 0 and passed == reference
    if not ok:
        print("  ECHEC — la restauration ne retrouve pas l'etat de reference")
    for label, echecs in resultats:
        verdict = "rouge" if echecs > 0 else "VERT — le garde ne mord pas"
        print(f"  {label} : {verdict} ({echecs} echecs)")
        ok = ok and echecs > 0

    print("\n" + ("TEMOIN CONCLUANT" if ok else "TEMOIN NON CONCLUANT"))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
