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
        "        if (!CollectionEnabled) return true;\n\n        string tempPath",
        "        string tempPath",
    ),
    (
        "3. CollectionEnabled est inversee",
        "src/UsageStats.cs", LF,
        "    internal static bool CollectionEnabled => !AppChannel.CurrentIsSober;",
        "    internal static bool CollectionEnabled => AppChannel.CurrentIsSober;",
    ),
    (
        "4. La fenetre n'annonce plus l'etat (attendu : 0 rouge)",
        "src/UsageStatsWindow.cs", LF,
        "        bool collectionOff = !UsageStats.CollectionEnabled;",
        "        bool collectionOff = false;",
    ),
]


def run_suite():
    """Rend (nombre de tests rouges, noms des tests rouges)."""
    out = subprocess.run(
        ["dotnet", "test", "src/AZERTYGlobal.Tests", "--nologo", "-v", "minimal"],
        cwd=REPO, capture_output=True, text=True, encoding="utf-8", errors="replace",
    ).stdout
    failed = sorted({
        line.strip().removeprefix("Failed ").split("(")[0].split("[")[0].strip()
        for line in out.splitlines() if line.strip().startswith("Failed ")
    })
    total = 0
    for line in out.splitlines():
        if "Failed:" in line and "Passed:" in line:
            total = int(line.split("Failed:")[1].split(",")[0].strip())
    return total, failed


def main() -> int:
    originals = {rel: (REPO / rel).read_bytes() for _, rel, _, _, _ in MUTATIONS}
    print("Empreintes d'origine :")
    for rel, data in originals.items():
        print("  {} {}".format(rel, hashlib.sha256(data).hexdigest()[:16]))

    try:
        for label, rel, eol, old, new in MUTATIONS:
            path = REPO / rel
            data = originals[rel]
            old_b = old.replace("\n", eol).encode("utf-8")
            new_b = new.replace("\n", eol).encode("utf-8")
            if data.count(old_b) != 1:
                print("\n{}\n  ECHEC : {} occurrence(s) de l'ancre".format(label, data.count(old_b)))
                continue
            path.write_bytes(data.replace(old_b, new_b))
            count, failed = run_suite()
            print("\n{}\n  {} test(s) rouge(s)".format(label, count))
            for name in failed:
                print("    - " + name.split(".")[-1])
            path.write_bytes(data)
    finally:
        for rel, data in originals.items():
            path = REPO / rel
            path.write_bytes(data)
            state = "identique" if path.read_bytes() == data else "DIVERGENT"
            print("\nRestaure {} : {}".format(rel, state))

    print("\nControle final, suite non mutee :")
    count, _ = run_suite()
    print("  {} test(s) rouge(s)".format(count))
    return 0


if __name__ == "__main__":
    sys.exit(main())
