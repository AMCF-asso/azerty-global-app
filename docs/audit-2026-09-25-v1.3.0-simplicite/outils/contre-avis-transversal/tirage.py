import json, random, subprocess, sys
base = "C:/Users/antoi/AppData/Local/Temp/claude/D--My-files/3f079c8a-fda9-4162-b849-8be427b39360/scratchpad/audit/"
repo = "D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store"
m = json.load(open(base + "metriques.json", encoding="utf-8"))
c = m["code_mort"]["candidats"]
print("total", len(c))
for i, r in enumerate(c):
    print(i, r["nom"], r["categorie"], f'{r["fichier"]}:{r["ligne"]}', r["lignes"])
reste = list(range(20, len(c)))
random.seed(20260925)
tir = sorted(random.sample(reste, 15))
print("TIRAGE", tir)
for i in tir:
    r = c[i]
    out = subprocess.run(["git", "grep", "-n", "-w", r["nom"], "f0a98ba", "--"], cwd=repo, capture_output=True, text=True, encoding="utf-8", errors="replace").stdout
    sub = subprocess.run(["git", "grep", "-n", "-c", r["nom"], "f0a98ba", "--"], cwd=repo, capture_output=True, text=True, encoding="utf-8", errors="replace").stdout
    print("=====", i, r["nom"], f'{r["fichier"]}:{r["ligne"]}')
    print("-- mot entier:")
    print(out.strip())
    print("-- sous-chaine (fichiers:compte):")
    print(sub.strip())
