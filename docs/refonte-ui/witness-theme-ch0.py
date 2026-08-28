"""Témoin de mutation des gardes du socle de thème (CH0, refonte graphique v1.2.0).

Un garde-fou de ce dépôt n'est recevable qu'avec la preuve qu'il rougit. Les gardes de
`ThemeTests` et `ThemeControlsTests` portent sur des couleurs et des états, et une couleur
fausse produit toujours une couleur valide : ni le compilateur, ni les analyseurs, ni une
relecture visuelle ne les attrapent. Ce script mute les sources, lance la suite après chaque
mutation, restaure, et n'accepte le résultat que si chaque mutation a fait tomber au moins un
test — et que la restauration retrouve exactement le compte de départ.

Les mutations sont les erreurs que l'histoire du dépôt a réellement produites, ou celles que
la charte interdit nommément :

  1. un octet de trop sur un jeton — la dérive silencieuse d'une couleur ;
  2. les octets d'un jeton saisis dans l'ordre de GDI plutôt que dans celui de la charte —
     c'est ainsi qu'est né l'orange #D47800, qui n'est que le bleu #0078D4 à octets inversés ;
  3. une nuance inventée dans la palette ;
  4. la bordure décorative promue en contour de contrôle, ce qui fait disparaître un bouton
     secondaire sur le fond de la fenêtre ;
  5. la précédence de l'état inactif cassée, si bien qu'un contrôle désactivé s'allume au
     passage du curseur ;
  6. un lien qui change de couleur au survol — la demande exacte qui a fait naître l'orange ;
  7. une couleur inventée hors palette dans une primitive de contrôle.

⚠️ Ce script écrit dans les sources et les restaure ensuite. Il refuse de démarrer si l'un des
fichiers qu'il mute porte des modifications non commitées, faute de quoi une interruption
laisserait un état mutant indistinguable d'un travail en cours.

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
SUITE = REPO / "src" / "AZERTYGlobal.Tests" / "AZERTYGlobal.Tests.csproj"

THEME = "src/Theme.cs"
CONTROLS = "src/ThemeControls.cs"

# Configuration : sur ce poste, Smart App Control mure les binaires Debug fraîchement
# compilés (mesuré le 2026-08-28, l'inverse du 2026-08-24). Release passe. Le verdict est
# par binaire et bascule dans le temps : si les deux sont murés, la CI du dépôt est la voie.
CONFIGURATION = "Release"

MUTATIONS = [
    (
        "un octet de trop sur action-fond clair",
        THEME,
        "        ActionFill: Rgb(0xE3, 0xE9, 0xFC),",
        "        ActionFill: Rgb(0xE3, 0xE9, 0xFB),",
    ),
    (
        "octets de l'action claire saisis dans l'ordre de GDI",
        THEME,
        "        Action: Rgb(0x1A, 0x3E, 0xF2),\n        Success: Rgb(0x18, 0x63, 0x39),",
        "        Action: Rgb(0xF2, 0x3E, 0x1A),\n        Success: Rgb(0x18, 0x63, 0x39),",
    ),
    (
        "nuance inventee en bordure sombre",
        THEME,
        "        Border: Rgb(0x45, 0x3D, 0x30),\n        Action: Rgb(0x8F, 0xA6, 0xFF),",
        "        Border: Rgb(0x50, 0x48, 0x3A),\n        Action: Rgb(0x8F, 0xA6, 0xFF),",
    ),
    (
        "bordure decorative promue en contour de bouton secondaire",
        CONTROLS,
        """        if (kind == ButtonKind.Primary)
        {
            return state.HasFlag(ControlState.Hovered)
                ? new ControlPaint(p.Action, p.OnAction, 2, p.OnAction)
                : new ControlPaint(p.Action, p.Action, 1, p.OnAction);
        }

        return state.HasFlag(ControlState.Hovered)
            ? new ControlPaint(p.ActionFill, p.TextSecondary, 1, p.OnActionFill)
            : new ControlPaint(p.Surface, p.TextSecondary, 1, p.Ink);""",
        """        if (kind == ButtonKind.Primary)
        {
            return state.HasFlag(ControlState.Hovered)
                ? new ControlPaint(p.Action, p.OnAction, 2, p.OnAction)
                : new ControlPaint(p.Action, p.Action, 1, p.OnAction);
        }

        return state.HasFlag(ControlState.Hovered)
            ? new ControlPaint(p.ActionFill, p.TextSecondary, 1, p.OnActionFill)
            : new ControlPaint(p.Surface, p.Border, 1, p.Ink);""",
    ),
    (
        "precedence de l'etat inactif cassee",
        CONTROLS,
        """        if (state.HasFlag(ControlState.Disabled))
            return new ControlPaint(p.Paper, p.Disabled, 1, p.Disabled);

        if (state.HasFlag(ControlState.Pressed))
            return new ControlPaint(p.ActionFill, p.Action, 2, p.OnActionFill);""",
        """        if (state.HasFlag(ControlState.Pressed))
            return new ControlPaint(p.ActionFill, p.Action, 2, p.OnActionFill);

        if (state.HasFlag(ControlState.Disabled))
            return new ControlPaint(p.Paper, p.Disabled, 1, p.Disabled);""",
    ),
    (
        "lien qui change de couleur au survol",
        CONTROLS,
        """    internal static uint LinkColor(ControlState state, Palette p) =>
        state.HasFlag(ControlState.Disabled) ? p.Disabled : p.Action;""",
        """    internal static uint LinkColor(ControlState state, Palette p) =>
        state.HasFlag(ControlState.Disabled) ? p.Disabled
        : state.HasFlag(ControlState.Hovered) ? p.Success : p.Action;""",
    ),
    (
        "couleur inventee dans une primitive de controle",
        CONTROLS,
        """    internal static uint LabelColor(ControlState state, Palette p) =>
        state.HasFlag(ControlState.Disabled) ? p.Disabled : p.Ink;""",
        """    internal static uint LabelColor(ControlState state, Palette p) =>
        state.HasFlag(ControlState.Disabled) ? p.Disabled : Theme.Rgb(0x33, 0x33, 0x33);""",
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
        if stripped.startswith("Failed ") or stripped.startswith("error CS"):
            tombes.append(stripped.split(" [")[0])
        if "Failed:" in line and "Passed:" in line and "Total:" in line:
            # Regex plutot qu'un decoupage sur les virgules : le premier compteur est colle
            # au verdict (« Passed!  - Failed:     0, ... »), donc le premier morceau ne
            # commence pas par « Failed: ».
            compteurs = re.search(r"Failed:\s*(\d+).*?Passed:\s*(\d+)", line)
            if compteurs is not None:
                failed = int(compteurs.group(1))
                passed = int(compteurs.group(2))

    if failed is None:
        # Une mutation qui ne compile pas fait aussi rougir la suite, mais pour la mauvaise
        # raison : on le dit plutot que de la compter comme une preuve.
        return None, None, sorted(set(tombes)) or ["compilation ou execution impossible"]

    return failed, passed, sorted(set(tombes))


def main():
    fichiers = sorted({relative for _, relative, _, _ in MUTATIONS})
    dirty = subprocess.run(["git", "status", "--porcelain", "--", *fichiers],
                           cwd=REPO, capture_output=True, text=True).stdout.strip()
    if dirty:
        sys.exit("modifications non commitees sur les sources a muter — commiter avant, sinon "
                 f"une interruption laisserait un mutant indistinguable du travail :\n{dirty}")

    originaux = {relative: (REPO / relative).read_bytes() for relative in fichiers}

    print(f"Configuration : {CONFIGURATION}")
    print("Reference — sources non mutees :")
    failed, passed, _ = suite_verdict()
    print(f"  {passed} reussis, {failed} echecs")
    if failed != 0:
        sys.exit("la suite est deja rouge : le temoin ne prouverait rien")
    reference = passed

    resultats = []
    try:
        for label, relative, old, new in MUTATIONS:
            path = REPO / relative
            text = originaux[relative].decode("utf-8")
            if text.count(old) != 1:
                sys.exit(f"ancre absente ou multiple pour la mutation « {label} » "
                         f"({text.count(old)} occurrences dans {relative})")
            path.write_bytes(text.replace(old, new).encode("utf-8"))

            print(f"\nMutation — {label}  [{relative}]")
            failed, passed, tombes = suite_verdict()
            if failed is None:
                print("  la suite n'a pas pu rendre de compteurs")
            else:
                print(f"  {passed} reussis, {failed} echecs")
            for ligne in tombes:
                print(f"    {ligne}")
            resultats.append((label, failed))

            path.write_bytes(originaux[relative])
    finally:
        for relative, data in originaux.items():
            (REPO / relative).write_bytes(data)

    print("\nRestauration — sources non mutees :")
    failed, passed, _ = suite_verdict()
    print(f"  {passed} reussis, {failed} echecs")

    print("\nVerdict :")
    ok = failed == 0 and passed == reference
    if not ok:
        print("  ECHEC — la restauration ne retrouve pas l'etat de reference")
    for label, echecs in resultats:
        if echecs is None:
            verdict = "INDECIS — la suite n'a pas tourne"
            ok = False
        elif echecs > 0:
            verdict = f"rouge ({echecs} echecs)"
        else:
            verdict = "VERT — le garde ne mord pas"
            ok = False
        print(f"  {label} : {verdict}")

    print("\n" + ("TEMOIN CONCLUANT" if ok else "TEMOIN NON CONCLUANT"))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
