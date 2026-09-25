# Longueur des méthodes (accolades équilibrées) des fichiers de fenêtres ; liste celles > SEUIL lignes.
# Compte aussi les appels S(<entier>) (nombres magiques de mise en page) par fichier.
import re, pathlib, sys

SRC = pathlib.Path(r"D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src")
FILES = ["SettingsWindow.cs", "OnboardingWindow.cs", "AboutWindow.cs", "PauseDurationDialog.cs",
         "LayoutConflictWindow.cs", "MaintainableLayersWindow.cs"]
SEUIL = int(sys.argv[1]) if len(sys.argv) > 1 else 50
sig = re.compile(r"^\s*(?:public|private|internal|protected)\s+(?:static\s+)?[\w<>\[\],\.\s\(\)\?]*?\b(\w+)\s*\([^;]*$")

for f in FILES:
    lines = (SRC / f).read_text(encoding="utf-8").splitlines()
    out = []
    i = 0
    while i < len(lines):
        m = sig.match(lines[i])
        if m and "=>" not in lines[i]:
            name = m.group(1)
            # trouver la première accolade ouvrante
            j = i
            while j < len(lines) and "{" not in lines[j]:
                if lines[j].rstrip().endswith(";") or "=>" in lines[j]:
                    break
                j += 1
            if j < len(lines) and "{" in lines[j]:
                depth = 0; k = j
                while k < len(lines):
                    depth += lines[k].count("{") - lines[k].count("}")
                    if depth == 0:
                        break
                    k += 1
                n = k - i + 1
                if n > SEUIL:
                    out.append((n, name, i + 1, k + 1))
                i = k + 1
                continue
        i += 1
    text = "\n".join(lines)
    s_calls = len(re.findall(r"\bS\((?:-?\d+)\)", text))
    print(f"=== {f}  ({len(lines)} l.)  appels S(<littéral>) : {s_calls}")
    for n, name, a, b in sorted(out, reverse=True):
        print(f"  {n:4d} l.  {name}  ({a}-{b})")
