"""Temoins de mutation de la 1.3.0 : les correctifs de l'audit du 2026-09-20.

Chaque correctif est casse une fois, et le script note quels tests rougissent. Un
test qui reste vert sur la mutation qu'il est cense attraper ne prouve rien :
c'est le motif documente par .claude/rules/app-repo-guard-blind-spots.md.

Couverture : AG130-06 (trou de transition de suspension), AG130-08 (a) et (b)
(ecart 5 : AltGr emis nu, touche morte en attente perdue), AG130-09 (perte
silencieuse a l'emission), AG130-11 (a) et (b) (forme des cles inconnues, mutex
et fichier temporaire). AG130-07, 10, 40 et 42 ont leurs propres temoins joues a
la correction ; ils entreront ici quand une session les rejouera.

Trois differences avec les temoins du lot v1.2.0, toutes nees de mesures :

1. Les TROIS suites sont lancees. Les temoins des lots B, C et D ne lancaient que
   AZERTYGlobal.Tests, soit 175 tests sur 501 invisibles a toute mutation
   (AG130-14 (a)).
2. La fin de ligne de chaque ancre est DEDUITE, pas declaree. KeyMapper.cs,
   ConfigManager.cs et Program.cs ont des fins de ligne melangees : une ancre
   ecrite en CRLF pour un fichier majoritairement CRLF peut tomber sur une zone
   en LF, et le script annoncerait une ancre absente.
3. Un refus d'Application Control n'est pas un rouge. Mesure du 2026-09-21 : une
   mutation attendue a zero a rendu 320 rouges, tous en FileLoadException sur
   l'assembly reconstruite. Le temoin s'interrompt et le dit.

Le script restaure toujours l'etat d'origine, y compris en cas d'interruption :
les octets exacts sont gardes en memoire et reecrits dans un finally, avec
controle d'identite SHA-256.

Sortie 0 seulement si chaque mutation a rendu ce qu'elle devait. Fichier en ASCII
pur comme les autres temoins du depot (mesure).

Rejouable : python docs/audit-2026-09-20-v1.3.0/witness-v130.py
"""
import hashlib
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]

# (label, fichier, ancre, remplacement)
MUTATIONS = [
    (
        "1. AG130-06 : la transition perd la branche du motif",
        "src/TrayApplication.cs",
        "        if (suspendNow && currentReason != appliedReason) return SuspensionTransition.ReasonChanged;\n",
        "",
    ),
    (
        "2. AG130-06 : les raccourcis restent armes sous anti-cheat",
        "src/TrayApplication.cs",
        "        if (suspendedForCompatibility)\n"
        "            return reason == CompatibilitySuspendReason.UserOverride;\n",
        "        if (suspendedForCompatibility)\n"
        "            return true;\n",
    ),
    (
        "3. AG130-08 (a) : AltGr redevient un modificateur nu, sans scan code ni bit etendu",
        "src/TypingEngine.Windows/KeyMapper.cs",
        "        var (scan, extended) = ModifierScanCode(vk);\n"
        "        return MakeVkInput(vk, scan, keyUp, extended);\n",
        "        return MakeVkInput(vk, 0, keyUp);\n",
    ),
    (
        "4. AG130-08 (b) : la touche morte en attente n'est plus arbitree",
        "src/TypingEngine.Windows/KeyMapper.cs",
        "            ArbitratePendingDeadKeyWhileSuspended();\n"
        "            TrackPassThroughKey(scanCode, flags, isKeyDown);\n",
        "            TrackPassThroughKey(scanCode, flags, isKeyDown);\n",
    ),
    (
        "5. AG130-09 : un lot entierement refuse redevient muet",
        "src/TypingEngine.Windows/KeyMapper.cs",
        "        if (sent == 0) RecordEmissionLoss(inputs.Length);\n"
        "        else _emissionLossStreak = 0;\n",
        "",
    ),
    (
        "6. AG130-11 (a) : la cle inconnue est de nouveau reserialisee en chaine",
        "src/ConfigManager.cs",
        "                    val.WriteTo(writer);\n",
        "                    writer.WriteStringValue(val.ToString());\n",
    ),
    (
        "7. AG130-11 (b) : le second mutex redevient local a la session",
        "src/Program.cs",
        '                $"Global\\\\{ProductIdentity.SingleInstanceMutexName}.{qualifier}");\n',
        '                $"Local\\\\{ProductIdentity.SingleInstanceMutexName}.{qualifier}");\n',
    ),
    (
        "8. AG130-11 (b) : le fichier temporaire perd son PID",
        "src/UsageStats.cs",
        '        string tempPath = $"{_statsPath}.{Environment.ProcessId}.tmp";\n',
        '        string tempPath = $"{_statsPath}.tmp";\n',
    ),
]

# Aucune mutation de ce lot n'est attendue a zero rouge : les huit portent sur du
# code que la suite atteint. Une entree ici serait un angle mort a documenter.
EXPECT_ZERO = set()


class ApplicationControlBlocked(RuntimeError):
    """Application Control refuse l'assembly fraichement reconstruite (0x800711C7).

    Tous les tests du projet tombent alors en FileLoadException avant la moindre
    assertion : le compte de rouges ne mesure plus le garde, il mesure le blocage
    du poste.
    """


SUITES = (
    "src/TypingEngine.Core.Tests/TypingEngine.Core.Tests.csproj",
    "src/TypingEngine.Windows.Tests/TypingEngine.Windows.Tests.csproj",
    "src/AZERTYGlobal.Tests/AZERTYGlobal.Tests.csproj",
)


def encode_anchor(data, text):
    """Rend les octets de `text` dans la fin de ligne qui le trouve UNE fois.

    Rend None si aucune des deux formes n'apparait exactement une fois : le
    script le compte alors comme une ancre morte, jamais comme un zero rouge.
    """
    candidates = []
    for eol in ("\r\n", "\n"):
        blob = text.replace("\n", eol).encode("utf-8")
        if data.count(blob) == 1:
            candidates.append((eol, blob))
    if len(candidates) != 1:
        return None
    return candidates[0]


def run_suite():
    """Rend (nombre de tests rouges, noms des tests rouges) sur les trois suites."""
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
    originals = {rel: (REPO / rel).read_bytes() for _, rel, _, _ in MUTATIONS}
    print("Empreintes d'origine :")
    for rel, data in originals.items():
        print("  {} {}".format(rel, hashlib.sha256(data).hexdigest()[:16]))

    problems = []
    try:
        try:
            for index, (label, rel, old, new) in enumerate(MUTATIONS, start=1):
                path = REPO / rel
                data = originals[rel]
                found = encode_anchor(data, old)
                if found is None:
                    print("\n{}\n  ECHEC : ancre absente ou ambigue".format(label))
                    problems.append("{} : ancre absente ou ambigue, mutation non jouee".format(label))
                    continue
                eol, old_b = found
                new_b = new.replace("\n", eol).encode("utf-8")
                path.write_bytes(data.replace(old_b, new_b))
                count, failed = run_suite()
                print("\n{}\n  {} test(s) rouge(s)  [fin de ligne {}]".format(
                    label, count, "CRLF" if eol == "\r\n" else "LF"))
                for name in failed:
                    print("    - " + name.split(".")[-1])
                path.write_bytes(data)
                if index in EXPECT_ZERO:
                    if count != 0:
                        problems.append("{} : attendu 0 rouge, obtenu {}".format(label, count))
                elif count == 0:
                    problems.append("{} : aucun rouge, le correctif n'est pas eprouve".format(label))
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
