import json
m = json.load(open("C:/Users/antoi/AppData/Local/Temp/claude/D--My-files/3f079c8a-fda9-4162-b849-8be427b39360/scratchpad/audit/metriques.json", encoding="utf-8"))
pf = m["par_fichier_production"]
five = ["src/LearningModule.cs", "src/TrayApplication.cs", "src/LessonsWindow.cs", "src/SettingsWindow.cs", "src/OnboardingWindow.cs"]
for key in ("total", "code", "commentaire"):
    tot = sum(v["lignes"][key] for v in pf.values())
    s = sum(pf[f]["lignes"][key] for f in five)
    print(key, s, tot, round(100 * s / tot, 1))
