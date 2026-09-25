"""Fusionne constats-*.json + verdicts.json -> donnees/constats.json, annexe markdown et page HTML.

verdicts.json : {"<id>": {"verdict": "CONFIRME|NUANCE|REFUTE", "texte": "...",
                          "corrige": {"gravite": ..., "lignes": ..., "titre": ..., "confiance": ...}}}
"""
import json, pathlib, collections

AUDIT = pathlib.Path(__file__).resolve().parent.parent
ORD = {"majeur": 0, "moyen": 1, "mineur": 2}
ZONES = {"moteur": "Moteur de frappe", "app": "Application", "lecons": "Leçons et Défi",
         "fenetres": "Fenêtres", "visuel": "Clavier visuel et recherche", "transversal": "Transversal"}

# Deux dispositions : scratchpad (fichiers à la racine) ou docs/ (donnees/zones/, donnees/verdicts.json).
ZONES_DIR = AUDIT / "donnees" / "zones"
if not ZONES_DIR.exists():
    ZONES_DIR = AUDIT
tous = []
for f in sorted(ZONES_DIR.glob("constats-*.json")):
    tous.extend(json.loads(f.read_text(encoding="utf-8")))

vf = AUDIT / "donnees" / "verdicts.json"
if not vf.exists():
    vf = AUDIT / "verdicts.json"
verdicts = json.loads(vf.read_text(encoding="utf-8")) if vf.exists() else {}
for c in tous:
    v = verdicts.get(c["id"])
    if not v:
        continue
    if "suite" in v:
        c["suite"] = v["suite"]
    if "verdict" not in v:
        continue
    c["verdict"] = v["verdict"]
    c["verdict_texte"] = v.get("texte")
    corr = v.get("corrige") or {}
    for k in ("gravite", "titre", "confiance", "cible"):
        if k in corr:
            c[k + "_initial"] = c.get(k)
            c[k] = corr[k]
    if "lignes" in corr:
        c.setdefault("gain", {})
        c["gain_initial"] = dict(c["gain"])
        c["gain"]["lignes"] = corr["lignes"]

inconnus = sorted(set(verdicts) - {c["id"] for c in tous})
if inconnus:
    print("ids de verdict inconnus :", inconnus)

tous.sort(key=lambda c: (ORD.get(c.get("gravite"), 9), c["id"]))
out = AUDIT / "donnees"
out.mkdir(exist_ok=True)
(out / "constats.json").write_text(json.dumps(tous, ensure_ascii=False, indent=1) + "\n", encoding="utf-8", newline="\n")

# Annexe markdown : un tableau par zone
lignes = ["# Annexe — liste des constats", "",
          "Générée par `outils/construire.py` depuis `donnees/constats.json`. Verdict : contre-expertise second-avis (majeurs et constats de comportement).", ""]
for z, nom in ZONES.items():
    cz = [c for c in tous if c.get("zone") == z]
    if not cz:
        continue
    lignes += [f"## {nom} ({len(cz)})", "", "| Id | Gravité | Axe | Étiquette | Cible | Gain (l.) | Verdict | Constat | Fichiers |", "|---|---|---|---|---|---|---|---|---|"]
    for c in cz:
        g = (c.get("gain") or {}).get("lignes")
        fich = "<br>".join(f"`{x}`" for x in (c.get("fichiers") or [])[:3])
        titre = c.get("titre", "").replace("|", "\\|")
        lignes.append(f"| {c['id']} | {c.get('gravite')} | {c.get('axe')} | `{c.get('etiquette')}` | {c.get('cible')} | {g if g not in (None, 0) else '—'} | {c.get('verdict', '—').lower()} | {titre} | {fich} |")
    lignes.append("")
(AUDIT / "annexe-constats.md").write_text("\n".join(lignes), encoding="utf-8", newline="\n")

# Page
gab = (AUDIT / "page" / "gabarit.html").read_text(encoding="utf-8")
synth_f = AUDIT / "page" / "synthese.html"
synth = synth_f.read_text(encoding="utf-8") if synth_f.exists() else ""
data = json.dumps(tous, ensure_ascii=False).replace("</", "<\\/")
page = gab.replace("<!--SYNTHESE-->", synth).replace("/*DONNEES*/", data)
(AUDIT / "page" / "audit-simplicite-130.html").write_text(page, encoding="utf-8", newline="\n")

cpt = collections.Counter(c.get("gravite") for c in tous)
vc = collections.Counter(c.get("verdict", "-") for c in tous)
print(f"{len(tous)} constats ; gravité {dict(cpt)} ; verdicts {dict(vc)} ; page {len(page)//1024} Ko")
