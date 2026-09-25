"""Agrège les constats-*.json : comptes par zone/gravité/axe, liste des majeurs."""
import json, sys, pathlib, collections

AUDIT = pathlib.Path(__file__).resolve().parent.parent
mode = sys.argv[1] if len(sys.argv) > 1 else "resume"

tous = []
for f in sorted(AUDIT.glob("constats-*.json")):
    try:
        data = json.loads(f.read_text(encoding="utf-8"))
    except Exception as e:
        print(f"ERREUR {f.name}: {e}")
        continue
    for c in data:
        c["_fichier"] = f.name
    tous.extend(data)

if mode == "resume":
    print(f"{len(tous)} constats")
    par = collections.Counter((c.get("zone"), c.get("gravite")) for c in tous)
    for z in sorted({c.get("zone") for c in tous}):
        print(f"  {z}: " + ", ".join(f"{g} {par[(z, g)]}" for g in ("majeur", "moyen", "mineur")))
    print("axes:", dict(collections.Counter(c.get("axe") for c in tous)))
    print("etiquettes:", dict(collections.Counter(c.get("etiquette") for c in tous)))
    print("cibles:", dict(collections.Counter(c.get("cible") for c in tous)))
    print("confiance:", dict(collections.Counter(c.get("confiance") for c in tous)))
    print("somme lignes:", sum((c.get("gain") or {}).get("lignes") or 0 for c in tous))
elif mode == "majeurs":
    zones = set(sys.argv[2].split(",")) if len(sys.argv) > 2 else None
    for c in tous:
        if c.get("gravite") != "majeur" or (zones and c.get("zone") not in zones):
            continue
        print(f"### {c['id']} — {c.get('titre')}")
        print(f"fichiers: {', '.join(c.get('fichiers') or [])}")
        print(f"constat: {c.get('constat')}")
        print(f"preuve: {c.get('preuve')}")
        print(f"proposition: {c.get('proposition')}")
        print(f"gain: {c.get('gain')} | confiance: {c.get('confiance')}")
        print()
elif mode == "ligne":
    for c in tous:
        g = (c.get("gain") or {}).get("lignes")
        print(f"{c['id']}\t{c.get('gravite')}\t{c.get('axe')}\t{c.get('etiquette')}\t{c.get('cible')}\t{c.get('confiance')}\t{g}\t{c.get('titre')}")
