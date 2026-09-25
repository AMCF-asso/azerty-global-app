import json, subprocess
base = "C:/Users/antoi/AppData/Local/Temp/claude/D--My-files/3f079c8a-fda9-4162-b849-8be427b39360/scratchpad/audit/"
repo = "D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store"
c = json.load(open(base + "metriques.json", encoding="utf-8"))["code_mort"]["candidats"]
for i, r in enumerate(c):
    out = subprocess.run(["git", "grep", "-n", "-w", r["nom"], "f0a98ba", "--"], cwd=repo, capture_output=True, text=True, encoding="utf-8", errors="replace").stdout.strip().splitlines()
    decl = f'f0a98ba:{r["fichier"]}:{r["ligne"]}:'
    autres = [l for l in out if not l.startswith(decl)]
    # retirer les lignes de commentaire C#
    def is_comment(l):
        parts = l.split(":", 3)
        code = parts[3].strip() if len(parts) > 3 else ""
        return code.startswith("//") or code.startswith("*") or code.startswith("///")
    hors_com = [l for l in autres if not is_comment(l)]
    if hors_com:
        print("=====", i, r["nom"], f'{r["fichier"]}:{r["ligne"]}')
        for l in hors_com[:8]:
            print("   ", l[:220])
print("fin")
