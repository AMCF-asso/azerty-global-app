"""Cases de RECETTE-1.3.0.md à rejouer entre deux bundles : commits, fichiers touchés, cases.

Usage, à la racine du dépôt :
  python docs/recette-2026-09-27-v1.3.0/cases_a_rejouer.py <commit du bundle recetté> <commit du bundle final>

Sortie : un tableau Markdown « commit → fichiers → cases », puis la liste des cases à rejouer.
R01-R03 et R81-R83 sont toujours rejouées : elles portent sur le bundle lui-même.
Un fichier de src/ qu'aucune règle ne couvre donne « à juger » : la table ne se devine pas.
"""
import re
import subprocess
import sys

TOUJOURS = ["R01", "R02", "R03", "R81", "R82", "R83"]

# Motif de chemin (regex, cherchée dans le chemin) → cases. Premier motif trouvé, pas le seul :
# un fichier peut relever de plusieurs zones.
REGLES = [
    (r"^(docs|store-analytics|entreprise|scripts)/|\.md$|^LICENCE$", []),
    (r"^\.github/|\.csproj$|^msix/AppxManifest\.xml$|app\.manifest$", ["R04", "R06", "R131"]),
    (r"Onboarding|L\.Onboarding|AutoStart", ["R08", "R09", "R10", "R11", "R101", "R04", "R06", "R27"]),
    (r"LearningModule|Tutori|L\.Learning", ["R102", "R103", "R17", "R27", "R12", "R13", "R14", "R15"]),
    (r"LessonsWindow|Lesson[A-Z]|LessonsHome|GestureMastery|Gesture\.cs|Paper|Tablature|MiniKeyboard|KeyNotation|"
     r"L\.Lessons|L\.Notation|L\.Gesture|lessons\.json|LearningSessionTracker|PhysicalTextInputBuffer",
     ["R104", "R105", "R106", "R107", "R108", "R109", "R110", "R111", "R123", "R51", "R52", "R53", "R53b", "R53c"]),
    (r"CharacterSearch|CharacterIndex|LanguageSearch|language-search|L\.Search|ClipboardText|DisplayGlyph",
     ["R112", "R33", "R34", "R124"]),
    (r"character-index\.json", ["R112", "R33", "R107", "R124"]),
    (r"DailyChallenge|Challenge|L\.Challenge|UsageStatsWindow|L\.Stats", ["R116", "R117", "R04", "R06", "R54"]),
    (r"defi-corpus|TrainingReminders", ["R116", "R117"]),
    (r"AccessibleName", ["R69", "R53c", "R102"]),
    (r"ToastActivation", ["R44", "R121"]),
    (r"SettingsWindow|SettingsScroll|L\.Settings", ["R118", "R119", "R45", "R46", "R04", "R06", "R64b", "R67"]),
    (r"KeyboardRenderer|VirtualKeyboard|L\.Keyboard", ["R120", "R138", "R62"]),
    (r"TrayApplication|L\.Tray|Pause|L\.Notifications", ["R121", "R122", "R57", "R57b", "R116", "R04", "R06"]),
    (r"GameRegistry|Compat|RemoteAccess|LayoutConflict", ["R126", "R93", "R30", "R118"]),
    (r"ReviewPrompt|StoreReview|ReviewShare", ["R41", "R44", "R04", "R06"]),
    (r"ConfigManager|FileQuarantine|UsageStats\.cs|LessonProgressStore|RelaunchSignal|Program\.cs",
     ["R07", "R74", "R75", "R131", "R32", "R04", "R06"]),
    (r"RelaunchSignal|RestartRegistration", ["R125"]),
    (r"AZERTY Global 2026\.json|LayoutLoader|AzertyGlobalWindowsTypingHost|Engine|Hook|Remap|Caps|"
     r"TextInsertion|ToggleNotification",
     ["R28", "R29", "R30", "R31", "R32", "R133", "R134", "R135", "R138", "R141"]),
    (r"MaintainableLayers|LayerIndicator|L\.Layers", ["R58", "R31", "R96"]),
    (r"AboutWindow|L\.About|LinkBehavior|ProductIdentity", ["R56", "R60"]),
    (r"ProductIdentity", ["R142"]),
    (r"NativeWindow|WindowSizing|DarkTheme|LightTheme|LightButton|GdiHelpers|AppFonts|Fonts/|Win32\.cs|"
     r"TypeRamp|PaintBuffer",
     ["R60", "R61", "R64b", "R67", "R77"]),
    (r"Localization/", ["R62", "R64c"]),
    (r"^src/AZERTYGlobal\.Tests/", []),
]


def git(*args):
    return subprocess.run(["git", "-c", "core.quotepath=false", *args], capture_output=True, text=True,
                          encoding="utf-8", check=True).stdout


def cases_pour(path):
    trouve, hors = [], True
    for motif, cases in REGLES:
        if re.search(motif, path):
            hors = False
            trouve += [c for c in cases if c not in trouve]
    if hors and path.startswith("src/"):
        return None
    return trouve


def cle(c):
    m = re.match(r"R(\d+)([a-z]?)", c)
    return (int(m.group(1)), m.group(2)) if m else (9999, c)


def main():
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    a, b = sys.argv[1], sys.argv[2]
    commits = git("log", "--reverse", "--no-merges", "--format=%h%x09%s", f"{a}..{b}").splitlines()
    print(f"# Cases à rejouer — `{a}` → `{b}`\n")
    print(f"{len(commits)} commit(s) hors fusions. Toujours rejouées : {', '.join(TOUJOURS)}.\n")
    print("| Commit | Fichiers | Cases |")
    print("|---|---|---|")
    total, a_juger = set(TOUJOURS), []
    for line in commits:
        h, sujet = line.split("\t", 1)
        fichiers = [f for f in git("show", "--name-only", "--format=", h).splitlines() if f]
        cases = []
        for f in fichiers:
            c = cases_pour(f)
            if c is None:
                a_juger.append(f"{h} {f}")
                continue
            cases += [x for x in c if x not in cases]
        total.update(cases)
        noms = ", ".join(f"`{f.split('/')[-1]}`" for f in fichiers[:4]) + (f" (+{len(fichiers) - 4})" if len(fichiers) > 4 else "")
        texte = ", ".join(sorted(cases, key=cle)) if cases else "aucune (docs ou tests)"
        print(f"| `{h}` {sujet[:60]} | {noms} | {texte} |")
    print(f"\n**À rejouer ({len(total)})** : {', '.join(sorted(total, key=cle))}.")
    if a_juger:
        print("\n**À juger à la main** (fichier de src/ sans règle) :")
        for x in a_juger:
            print(f"- {x}")


if __name__ == "__main__":
    main()
