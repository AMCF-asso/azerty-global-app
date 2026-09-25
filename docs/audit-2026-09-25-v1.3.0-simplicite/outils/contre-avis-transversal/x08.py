import json
m = json.load(open("C:/Users/antoi/AppData/Local/Temp/claude/D--My-files/3f079c8a-fda9-4162-b849-8be427b39360/scratchpad/audit/metriques.json", encoding="utf-8"))
t = m.get("types_top15_lignes")
print(json.dumps(t[:6], ensure_ascii=False)[:1500] if isinstance(t, list) else str(t)[:1500])
pf = m.get("par_fichier") or m.get("fichiers") or {}
print(list(m.keys())[:60])
