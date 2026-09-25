import json, collections, pathlib
p = pathlib.Path(__file__).resolve().parent.parent / "constats-visuel.json"
data = json.loads(p.read_text(encoding="utf-8"))
keys = ["id","zone","fichiers","axe","etiquette","gravite","titre","constat","preuve","proposition","gain","risque","cible","lecon_v2","confiance"]
allowed = {"axe": {"exec","eco","lisi"}, "etiquette": {"delete","stdlib","native","yagni","shrink","perf","clarte"},
           "gravite": {"majeur","moyen","mineur"}, "cible": {"1.4.0","v2","les deux"}, "confiance": {"élevée","moyenne","faible"}}
for c in data:
    missing = [k for k in keys if k not in c]
    if missing: print(c.get("id"), "manque", missing)
    for k, vals in allowed.items():
        if c[k] not in vals: print(c["id"], k, "invalide:", c[k])
    if set(c["gain"]) != {"lignes","exec","nature"}: print(c["id"], "gain mal formé")
print("constats:", len(data))
print("gravité:", dict(collections.Counter(c["gravite"] for c in data)))
print("axe:", dict(collections.Counter(c["axe"] for c in data)))
print("lignes (somme):", sum(c["gain"]["lignes"] for c in data))
