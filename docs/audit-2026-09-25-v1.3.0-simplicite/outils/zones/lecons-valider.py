"""Valide constats-lecons.json contre la grille (champs, valeurs admises) et compte."""
import json
from collections import Counter
from pathlib import Path

p = Path(__file__).with_name("constats-lecons.json")
data = json.loads(p.read_text(encoding="utf-8"))
required = ["id", "zone", "fichiers", "axe", "etiquette", "gravite", "titre", "constat", "preuve",
            "proposition", "gain", "risque", "cible", "lecon_v2", "confiance"]
allowed = {
    "axe": {"exec", "eco", "lisi"},
    "etiquette": {"delete", "stdlib", "native", "yagni", "shrink", "perf", "clarte"},
    "gravite": {"majeur", "moyen", "mineur"},
    "cible": {"1.4.0", "v2", "les deux"},
    "confiance": {"élevée", "moyenne", "faible"},
}
errors = []
for c in data:
    for k in required:
        if k not in c:
            errors.append(f"{c.get('id')}: champ manquant {k}")
    for k, vals in allowed.items():
        if c.get(k) not in vals:
            errors.append(f"{c['id']}: {k}={c.get(k)!r} hors grille")
    g = c.get("gain", {})
    if set(g) != {"lignes", "exec", "nature"} or g.get("nature") not in {"preuve", "estimation"}:
        errors.append(f"{c['id']}: gain mal formé")
    if c.get("zone") != "lecons" or not c["id"].startswith("L-"):
        errors.append(f"{c['id']}: zone/id")
print("erreurs :", errors or "aucune")
print("constats :", len(data))
print("par gravité :", dict(Counter(c["gravite"] for c in data)))
print("par axe :", dict(Counter(c["axe"] for c in data)))
print("par étiquette :", dict(Counter(c["etiquette"] for c in data)))
print("somme des gains en lignes :", sum(c["gain"]["lignes"] for c in data))
print("lecon_v2 non nulles :", sum(1 for c in data if c["lecon_v2"]))
