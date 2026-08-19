"""Temoins de mutation du lot C v1.2.0 : couche de politiques d'entreprise.

Chaque garde-fou du lot est casse une fois, et le script note quels tests rougissent. Un
test qui reste vert sur la mutation qu'il est cense attraper ne prouve rien : c'est le motif
documente par .claude/rules/app-repo-guard-blind-spots.md.

Les mutations 9 et 10 sont attendues A ZERO ROUGE, et c'est le point : elles documentent
l'angle mort du lot C. Le grisage des reglages et l'entree de menu grisee vivent dans des
fenetres et un menu que la suite ne peut pas instancier ; ils n'ont d'autre preuve que le
smoke test du lot G.

La mutation 8 vise le P/Invoke lui-meme, que les tests atteignent en lisant des valeurs
publiees par Windows sous HKLM : sans elle, la lecture registre reelle resterait la seule
piece de cette couche sans temoin, les tests n'ayant pas le droit d'ecrire une politique.

Ce fichier est en ASCII pur comme les autres scripts de ce dossier (mesure), et restaure
toujours l'etat d'origine, y compris en cas d'interruption : les octets exacts sont gardes
en memoire et reecrits dans un finally, avec controle d'identite SHA-256.

/!\\ Ne jamais reecrire ce fichier depuis un heredoc Bash : les antislashs doubles y arrivent
ecrases, et "CRLF" devient un vrai saut de ligne. Mesure deux fois le 2026-08-19.

Rejouable : python docs/audit-v1.2.0/witness-lot-c.py
"""
import hashlib
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]

CRLF = "\r\n"
LF = "\n"

# (libelle, chemin relatif, fins de ligne de la zone, ancre, mutation)
MUTATIONS = [
    (
        "1. La politique des liens externes est ignoree, seul le canal decide",
        "src/PolicyManager.cs", LF,
        "    internal static bool ExternalLinksEnabled(bool? policy, DistributionChannel channel) =>\n"
        "        policy ?? !AppChannel.IsSober(channel);",
        "    internal static bool ExternalLinksEnabled(bool? policy, DistributionChannel channel) =>\n"
        "        !AppChannel.IsSober(channel);",
    ),
    (
        "2. La fenetre de bienvenue est uniformisee : 1 imposerait la case",
        "src/PolicyManager.cs", LF,
        "        policy == false ? false : userSetting;",
        "        policy ?? userSetting;",
    ),
    (
        "3. Une clef absente est lue comme un zero",
        "src/PolicyManager.cs", LF,
        "            _ => null,",
        "            _ => false,",
    ),
    (
        "4. La racine des strategies disparait du chemin",
        "src/PolicyManager.cs", LF,
        "    internal static string KeyPath => PoliciesRoot + ProductIdentity.Namespace;",
        "    internal static string KeyPath => ProductIdentity.Namespace;",
    ),
    (
        "5. N'importe quelle langue est acceptee",
        "src/PolicyManager.cs", LF,
        '        if (lang is "fr" or "en")\n            return lang;',
        '        if (lang is not null)\n            return lang;',
    ),
    (
        "6. La porte unique de la langue perd son garde",
        "src/ConfigManager.cs", CRLF,
        "        if (PolicyManager.LanguageIsManagedNow) return;",
        "        if (PolicyManager.LanguageIsManagedNow) { }",
    ),
    (
        "7. Les statistiques retombent sur le seul defaut de canal",
        "src/UsageStats.cs", LF,
        "        PolicyManager.UsageStatsEnabled(PolicyManager.Current.UsageStats, AppChannel.Current);",
        "        !AppChannel.CurrentIsSober;",
    ),
    (
        "8. Le lecteur registre ne rend jamais de REG_DWORD",
        "src/PolicyManager.cs", LF,
        "            return dword;",
        "            return null;",
    ),
    (
        "9. Le reglage sous politique n'est plus grise (attendu : 0 rouge)",
        "src/SettingsWindow.cs", LF,
        "            Win32.EnableWindow(_hWndChkNotifications, false);",
        "            Win32.EnableWindow(_hWndChkNotifications, true);",
    ),
    (
        "10. L'entree de langue du menu n'est plus grisee (attendu : 0 rouge)",
        "src/TrayApplication.cs", CRLF,
        "        uint languageFlags = PolicyManager.LanguageIsManagedNow ? MF_STRING | MF_GRAYED : MF_STRING;",
        "        uint languageFlags = MF_STRING;",
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
