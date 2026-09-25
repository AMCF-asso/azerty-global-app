"""Liste les constats majeurs après contre-expertise (donnees/constats.json)."""
import json, pathlib
AUDIT = pathlib.Path(__file__).resolve().parent.parent
tous = json.loads((AUDIT / "donnees" / "constats.json").read_text(encoding="utf-8"))
for c in tous:
    if c["gravite"] == "majeur" or c.get("gravite_initial") == "majeur":
        g = (c.get("gain") or {}).get("lignes")
        print(f"{c['id']}\t{c['gravite']}\t(init {c.get('gravite_initial', '=')})\t{c.get('verdict', '-')}\t{g}\t{c['titre'][:90]}")
