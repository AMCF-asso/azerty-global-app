"""Chiffres consolidés après contre-expertise."""
import json, pathlib, collections
AUDIT = pathlib.Path(__file__).resolve().parent.parent
t = json.loads((AUDIT / "donnees" / "constats.json").read_text(encoding="utf-8"))
C = collections.Counter
print("gravite", dict(C(c["gravite"] for c in t)))
print("axe", dict(C(c["axe"] for c in t)))
print("etiquette", dict(C(c["etiquette"] for c in t)))
print("cible", dict(C(c["cible"] for c in t)))
print("verdict", dict(C(c.get("verdict", "-") for c in t)))
print("par zone x gravite")
for z in ("moteur", "app", "lecons", "fenetres", "visuel", "transversal"):
    cz = [c for c in t if c["zone"] == z]
    print(" ", z, len(cz), dict(C(c["gravite"] for c in cz)))
neg = sum((c.get("gain") or {}).get("lignes") or 0 for c in t if ((c.get("gain") or {}).get("lignes") or 0) < 0)
print("somme brute des gains negatifs (non additive)", neg)
dele = [c for c in t if c["etiquette"] == "delete"]
print("delete", len(dele), sum((c.get("gain") or {}).get("lignes") or 0 for c in dele), [c["id"] for c in dele])
print("lecon_v2 renseignee", sum(1 for c in t if c.get("lecon_v2")))
