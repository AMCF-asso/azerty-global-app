import json
m = json.load(open("C:/Users/antoi/AppData/Local/Temp/claude/D--My-files/3f079c8a-fda9-4162-b849-8be427b39360/scratchpad/audit/metriques.json", encoding="utf-8"))
pf = m["par_fichier_production"]
k = next(iter(pf))
print(k, pf[k])
five = ["src/LearningModule.cs", "src/TrayApplication.cs", "src/LessonsWindow.cs", "src/SettingsWindow.cs", "src/OnboardingWindow.cs"]
for key in pf[k]:
    if isinstance(pf[k][key], (int, float)):
        tot = sum(v.get(key, 0) for v in pf.values() if isinstance(v.get(key, 0), (int, float)))
        s = sum(pf[f].get(key, 0) for f in five)
        if tot:
            print(key, s, tot, round(100 * s / tot, 1))
