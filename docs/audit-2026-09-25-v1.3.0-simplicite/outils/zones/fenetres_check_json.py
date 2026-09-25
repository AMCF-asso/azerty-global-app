# Valide constats-fenetres.json contre les champs de la grille et résume les gravités.
import json, pathlib, collections

p = pathlib.Path(__file__).parent / "constats-fenetres.json"
data = json.loads(p.read_text(encoding="utf-8"))
required = {"id", "zone", "fichiers", "axe", "etiquette", "gravite", "titre", "constat", "preuve",
            "proposition", "gain", "risque", "cible", "lecon_v2", "confiance"}
enums = {"axe": {"exec", "eco", "lisi"},
         "etiquette": {"delete", "stdlib", "native", "yagni", "shrink", "perf", "clarte"},
         "gravite": {"majeur", "moyen", "mineur"},
         "cible": {"1.4.0", "v2", "les deux"},
         "confiance": {"élevée", "moyenne", "faible"}}
problems = []
for c in data:
    missing = required - c.keys()
    if missing:
        problems.append((c.get("id"), "manque", missing))
    for k, allowed in enums.items():
        if c.get(k) not in allowed:
            problems.append((c["id"], k, c.get(k)))
    g = c["gain"]
    if set(g) != {"lignes", "exec", "nature"} or g["nature"] not in {"preuve", "estimation"}:
        problems.append((c["id"], "gain", g))
print("constats:", len(data))
print("gravités:", dict(collections.Counter(c["gravite"] for c in data)))
print("problèmes:", problems or "aucun")
