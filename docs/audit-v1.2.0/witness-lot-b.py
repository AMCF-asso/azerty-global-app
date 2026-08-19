"""Temoins de mutation du lot B v1.2.0 : comportement sobre du canal AMCF.

Chaque garde-fou du lot est casse une fois, et le script note quels tests rougissent.
Un test qui reste vert sur la mutation qu'il est cense attraper ne prouve rien : c'est le
motif documente par .claude/rules/app-repo-guard-blind-spots.md, ou un test de l'activateur
COM est reste vert pendant toute l'absence de la declaration qu'il pretendait garder.

Les mutations 5 et 6 sont attendues A ZERO ROUGE, et c'est le point : elles documentent
l'angle mort du lot B. Les deux liens Discord vivent dans des fenetres que la suite ne peut
pas instancier, donc leur extinction n'est prouvee que par le smoke test du lot G.

Ce fichier est en ASCII pur comme les trois autres scripts de ce dossier (mesure), et
restaure toujours l'etat d'origine, y compris en cas d'interruption : les octets exacts sont
gardes en memoire et reecrits dans un finally, avec controle d'identite SHA-256.

/!\ Ne jamais reecrire ce fichier depuis un heredoc Bash : les antislashs doubles y arrivent
ecrases, et "CRLF" devient un vrai saut de ligne. Mesure deux fois le 2026-08-19.

Rejouable : python docs/audit-v1.2.0/witness-lot-b.py
"""
import hashlib
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]

CRLF = "\r\n"
LF = "\n"

# (libelle, chemin relatif, fins de ligne du fichier, ancre, mutation)
MUTATIONS = [
    (
        "1. AppChannel.IsSober devient tout ce qui n'est pas Store",
        "src/AppChannel.cs", LF,
        "        channel == DistributionChannel.Amcf;",
        "        channel != DistributionChannel.Store;",
    ),
    (
        "2. Le sous-menu sobre garde Soutenir le projet",
        "src/TrayApplication.cs", CRLF,
        "            ? new[] { IDM_FEEDBACK, IDM_BUG }",
        "            ? new[] { IDM_SUPPORT, IDM_FEEDBACK, IDM_BUG }",
    ),
    (
        "3. Noter sur le Store revient sur tous les canaux",
        "src/TrayApplication.cs", CRLF,
        "            ? Array.Empty<int>()\n            : new[] { IDM_RATE_STORE };",
        "            ? new[] { IDM_RATE_STORE }\n            : new[] { IDM_RATE_STORE };",
    ),
    (
        "4. La regle du partage retombe sur suis-je package",
        "src/ReviewSharePrompt.cs", LF,
        "        if (s.Channel != DistributionChannel.Store) return false;",
        "        if (s.Channel == DistributionChannel.Unpackaged) return false;",
    ),
    (
        "5. Le lien Discord des statistiques redevient inconditionnel (attendu : 0 rouge)",
        "src/UsageStatsWindow.cs", LF,
        "        if (!AppChannel.CurrentIsSober) discordStyle |= Win32.WS_VISIBLE;",
        "        discordStyle |= Win32.WS_VISIBLE;",
    ),
    (
        "6. Le lien Discord de l'accueil redevient inconditionnel (attendu : 0 rouge)",
        "src/OnboardingWindow.cs", CRLF,
        "        Win32.ShowWindow(_hWndLinkDiscord, AppChannel.CurrentIsSober ? 0 : step3Vis);",
        "        Win32.ShowWindow(_hWndLinkDiscord, step3Vis);",
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
