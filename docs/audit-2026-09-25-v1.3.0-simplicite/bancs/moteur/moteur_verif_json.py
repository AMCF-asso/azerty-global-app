import json, collections
p = r"C:/Users/antoi/AppData/Local/Temp/claude/D--My-files/3f079c8a-fda9-4162-b849-8be427b39360/scratchpad/audit/constats-moteur.json"
d = json.load(open(p, encoding="utf-8"))
keys = ["id", "zone", "fichiers", "axe", "etiquette", "gravite", "titre", "constat", "preuve",
        "proposition", "gain", "risque", "cible", "lecon_v2", "confiance"]
for c in d:
    missing = [k for k in keys if k not in c]
    assert not missing, (c["id"], missing)
    assert c["axe"] in ("exec", "eco", "lisi"), c["id"]
    assert c["etiquette"] in ("delete", "stdlib", "native", "yagni", "shrink", "perf", "clarte"), c["id"]
    assert c["gravite"] in ("majeur", "moyen", "mineur"), c["id"]
    assert c["cible"] in ("1.4.0", "v2", "les deux"), c["id"]
    assert c["confiance"] in ("élevée", "moyenne", "faible"), c["id"]
    assert set(c["gain"]) == {"lignes", "exec", "nature"}, c["id"]
print(len(d), "constats valides")
print(collections.Counter(c["gravite"] for c in d))
print("lignes nettes :", sum(c["gain"]["lignes"] for c in d))
