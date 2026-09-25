import json, collections, pathlib
p = pathlib.Path(r"C:/Users/antoi/AppData/Local/Temp/claude/D--My-files/3f079c8a-fda9-4162-b849-8be427b39360/scratchpad/audit/constats-app.json")
data = json.loads(p.read_text(encoding="utf-8"))
keys = ["id","zone","fichiers","axe","etiquette","gravite","titre","constat","preuve","proposition","gain","risque","cible","lecon_v2","confiance"]
enums = {"axe": {"exec","eco","lisi"}, "etiquette": {"delete","stdlib","native","yagni","shrink","perf","clarte"},
         "gravite": {"majeur","moyen","mineur"}, "cible": {"1.4.0","v2","les deux"}, "confiance": {"élevée","moyenne","faible"}}
for c in data:
    missing = [k for k in keys if k not in c]
    assert not missing, (c.get("id"), missing)
    for k, allowed in enums.items():
        assert c[k] in allowed, (c["id"], k, c[k])
    assert set(c["gain"]) == {"lignes","exec","nature"}, c["id"]
    assert c["gain"]["nature"] in {"preuve","estimation"}, c["id"]
print("constats:", len(data))
print("par gravite:", dict(collections.Counter(c["gravite"] for c in data)))
print("par axe:", dict(collections.Counter(c["axe"] for c in data)))
print("gain lignes total:", sum(c["gain"]["lignes"] for c in data))
