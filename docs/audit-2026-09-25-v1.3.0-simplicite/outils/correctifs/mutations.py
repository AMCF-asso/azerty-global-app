"""Témoins de mutation des correctifs du 25/09 : chaque mutation rétablit l'ancien comportement,
lance les deux classes de test, puis restaure le fichier à l'octet près (vérifié par SHA-256).
Règle du dépôt : lister tous les tests tombés, et s'arrêter si Application Control bloque."""
import hashlib, pathlib, re, subprocess, sys

sys.stdout.reconfigure(encoding="utf-8")
DEPOT = pathlib.Path("D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store")
PROJET = DEPOT / "src/AZERTYGlobal.Tests/AZERTYGlobal.Tests.csproj"
FILTRE = "FullyQualifiedName~SearchResultListTests|FullyQualifiedName~UsageStatsReadFailureTests"

MUTATIONS = [
    ("V-07 clic sans séparateurs", "src/CharacterSearch.cs",
     [(b"        int y = searchAreaH + 1;\r\n        for (int k = 0;", b"        int y = searchAreaH;\r\n        for (int k = 0;"),
      (b"            y += h + 1;\r\n", b"            y += h;\r\n")]),
    ("V-08 pied plafonné", "src/CharacterSearch.cs",
     [(b"        if (total > shown) return L.Search_ResultCountCapped(shown, total);\r\n",
       b"        if (false) return L.Search_ResultCountCapped(shown, total);\r\n")]),
    ("V-06 mots français en dur", "src/CharacterSearch.cs",
     [(b"if (token == L.Settings_ShortcutModifier2)", b"if (token == \"Maj\")"),
      (b"token == L.Search_ThenWord)", b"token == \"puis\")")]),
    ("A-11 garde retirée", "src/UsageStats.cs",
     [(b"        if (_readFailed) return true;\r\n", b"        if (false && _readFailed) return true;\r\n")]),
]


def sha(b):
    return hashlib.sha256(b).hexdigest()


for nom, rel, remplacements in MUTATIONS:
    p = DEPOT / rel
    origine = p.read_bytes()
    mute = origine
    for a, b in remplacements:
        if mute.count(a) != 1:
            sys.exit(f"ARRÊT {nom} : ancre {a!r} trouvée {mute.count(a)} fois")
        mute = mute.replace(a, b)
    try:
        p.write_bytes(mute)
        r = subprocess.run(["dotnet", "test", str(PROJET), "-c", "Release", "--filter", FILTRE],
                           capture_output=True, text=True, encoding="utf-8", errors="replace")
        sortie = r.stdout + r.stderr
    finally:
        p.write_bytes(origine)
        assert sha(p.read_bytes()) == sha(origine), f"restauration ratée : {rel}"
    if "Could not load file or assembly" in sortie or "0x800711C7" in sortie:
        print(f"{nom} : mesure impossible (Application Control), ni prouvée ni infirmée")
        continue
    tombes = sorted(set(re.findall(r"^\s*Failed (AZERTYGlobal\.Tests\.[^\s\[]+(?:\([^)]*\))?)", sortie, re.M)))
    bilan = re.findall(r"(Failed!|Passed!)\s+- Failed:\s+(\d+), Passed:\s+(\d+)", sortie)
    print(f"{nom} : {bilan} ; tombés ({len(tombes)}) :")
    for t in tombes:
        print("   ", t)
print("Fichiers restaurés à l'octet près.")
