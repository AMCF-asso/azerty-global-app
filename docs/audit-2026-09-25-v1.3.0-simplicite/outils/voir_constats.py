"""Affiche en entier les constats demandés (ids en arguments)."""
import json, pathlib, sys
sys.stdout.reconfigure(encoding="utf-8")
AUDIT = pathlib.Path(__file__).resolve().parent.parent
tous = {c["id"]: c for c in json.loads((AUDIT / "donnees" / "constats.json").read_text(encoding="utf-8"))}
for i in sys.argv[1:]:
    c = tous[i]
    print(f"### {i} — {c['titre']}")
    for k in ("fichiers", "constat", "preuve", "proposition", "risque", "verdict_texte"):
        print(f"{k}: {c.get(k)}")
    print()
