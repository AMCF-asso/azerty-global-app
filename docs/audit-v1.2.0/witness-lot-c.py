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
        "        !AppChannel.IsSober(AppChannel.Current);",
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


# Mutations attendues A ZERO ROUGE : le grisage des reglages et l'entree de menu
# grisee vivent dans des fenetres et un menu que la suite ne peut pas instancier.
EXPECT_ZERO = {9, 10}


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
