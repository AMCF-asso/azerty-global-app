"""Temoins de mutation du lot D v1.2.0 : statistiques d'usage eteintes sur le canal sobre.

Meme methode et memes precautions que witness-lot-b.py, a lire d'abord.

La mutation 3 rougit 12 tests, dont 7 anterieurs a ce lot : c'est le temoin qui prouve que
l'ancienne suite exerce bien le chemin ou la collecte est active, et pas seulement le nouveau.

La mutation 4 est attendue A ZERO ROUGE : l'etat affiche par la fenetre de statistiques ne
peut pas etre instancie dans la suite. Ce que la fenetre annonce a l'utilisateur n'est donc
prouve que par le smoke test du lot G.

Fichier en ASCII pur comme les autres scripts de ce dossier (mesure).

Rejouable : python docs/audit-v1.2.0/witness-lot-d.py
"""
import hashlib
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]

LF = "\n"

MUTATIONS = [
    (
        "1. EnsureLoaded relit le fichier malgre la collecte eteinte",
        "src/UsageStats.cs", LF,
        "        if (!CollectionEnabled) return;\n\n        try\n        {",
        "        try\n        {",
    ),
    (
        "2. SaveLocked ecrit malgre la collecte eteinte",
        "src/UsageStats.cs", LF,
        "        if (!CollectionEnabled) return true;\n",
        "",
    ),
    (
        "3. CollectionEnabled est inversee",
        "src/UsageStats.cs", LF,
        "    internal static bool CollectionEnabled =>\n"
        "        PolicyManager.UsageStatsEnabled(PolicyManager.Current.UsageStats, AppChannel.Current);",
        "    internal static bool CollectionEnabled =>\n"
        "        !PolicyManager.UsageStatsEnabled(PolicyManager.Current.UsageStats, AppChannel.Current);",
    ),
    (
        "4. La fenetre n'annonce plus l'etat (attendu : 0 rouge)",
        "src/UsageStatsWindow.cs", LF,
        "        bool collectionOff = !UsageStats.CollectionEnabled;",
        "        bool collectionOff = false;",
    ),
]


# Mutation attendue A ZERO ROUGE : l'etat affiche par la fenetre de statistiques
# ne peut pas etre instancie dans la suite.
EXPECT_ZERO = {4}


class ApplicationControlBlocked(RuntimeError):
    """Application Control refuse l'assembly fraichement reconstruite (0x800711C7).

    Tous les tests du projet tombent alors en FileLoadException avant la moindre
    assertion : le compte de rouges ne mesure plus le garde, il mesure le blocage
    du poste. Mesure du 2026-09-21 : 320 rouges sur une mutation attendue a zero,
    pour le seul changement d'un appel ShowWindow. Le temoin s'arrete au lieu de
    conclure ; la mesure se refait plus tard, ou se lit dans la CI.
    """


SUITES = (
    "src/TypingEngine.Core.Tests/TypingEngine.Core.Tests.csproj",
    "src/TypingEngine.Windows.Tests/TypingEngine.Windows.Tests.csproj",
    "src/AZERTYGlobal.Tests/AZERTYGlobal.Tests.csproj",
)


def run_suite():
    """Rend (nombre de tests rouges, noms des tests rouges) sur LES TROIS suites.

    Ce script ne lancait que AZERTYGlobal.Tests, si bien que les tests des deux
    projets TypingEngine restaient invisibles a toute mutation : 175 sur 501 au
    moment de l'audit du 2026-09-20, soit 35 pour cent. Une mutation de src/
    peut rougir dans l'un d'eux et le temoin l'annoncait verte.

    Une suite qui ne rend aucun compteur n'a pas compile : c'est compte comme un
    rouge nomme, jamais comme un zero.
    """
    total = 0
    failed = set()
    for project in SUITES:
        out = subprocess.run(
            ["dotnet", "test", project, "--nologo", "-v", "minimal"],
            cwd=REPO, capture_output=True, text=True, encoding="utf-8", errors="replace",
        ).stdout
        if "Could not load file or assembly" in out or "0x800711C7" in out:
            raise ApplicationControlBlocked(
                "Application Control refuse l'assembly reconstruite de " + project
            )
        failed.update(
            line.strip().removeprefix("Failed ").split("(")[0].split("[")[0].strip()
            for line in out.splitlines() if line.strip().startswith("Failed ")
        )
        counted = False
        for line in out.splitlines():
            if "Failed:" in line and "Passed:" in line:
                total += int(line.split("Failed:")[1].split(",")[0].strip())
                counted = True
        if not counted:
            failed.add("COMPILATION-OU-DECOUVERTE-ECHOUEE " + project)
            total += 1
    return total, sorted(failed)


def main() -> int:
    originals = {rel: (REPO / rel).read_bytes() for _, rel, _, _, _ in MUTATIONS}
    print("Empreintes d'origine :")
    for rel, data in originals.items():
        print("  {} {}".format(rel, hashlib.sha256(data).hexdigest()[:16]))

    problems = []
    try:
      try:
        for index, mutation in enumerate(MUTATIONS, start=1):
            label, rel, eol, old, new = mutation
            path = REPO / rel
            data = originals[rel]
            old_b = old.replace("\n", eol).encode("utf-8")
            new_b = new.replace("\n", eol).encode("utf-8")
            if data.count(old_b) != 1:
                print("\n{}\n  ECHEC : {} occurrence(s) de l'ancre".format(label, data.count(old_b)))
                problems.append("{} : ancre absente ou ambigue, mutation non jouee".format(label))
                continue
            path.write_bytes(data.replace(old_b, new_b))
            count, failed = run_suite()
            print("\n{}\n  {} test(s) rouge(s)".format(label, count))
            for name in failed:
                print("    - " + name.split(".")[-1])
            path.write_bytes(data)
            if index in EXPECT_ZERO:
                if count != 0:
                    problems.append("{} : attendu 0 rouge, obtenu {}".format(label, count))
            elif count == 0:
                problems.append("{} : aucun rouge, le garde n'est pas eprouve".format(label))
      except ApplicationControlBlocked as blocked:
        print("\nINTERROMPU : {}".format(blocked))
        problems.append("{} : mesure impossible, rien n'est prouve ni infirme".format(blocked))
    finally:
        for rel, data in originals.items():
            path = REPO / rel
            path.write_bytes(data)
            state = "identique" if path.read_bytes() == data else "DIVERGENT"
            print("\nRestaure {} : {}".format(rel, state))
            if state != "identique":
                problems.append("{} : restauration DIVERGENTE".format(rel))

    print("\nControle final, suite non mutee :")
    try:
        count, _ = run_suite()
        print("  {} test(s) rouge(s)".format(count))
        if count != 0:
            problems.append("suite non mutee : {} rouge(s), la mesure ne vaut rien".format(count))
    except ApplicationControlBlocked as blocked:
        print("  {}".format(blocked))
        problems.append("controle final : {}".format(blocked))

    if problems:
        print("\nTEMOIN NON CONCLUANT :")
        for line in problems:
            print("  - " + line)
        return 1
    print("\nTemoin conforme : chaque mutation a rendu ce qu'elle devait.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
