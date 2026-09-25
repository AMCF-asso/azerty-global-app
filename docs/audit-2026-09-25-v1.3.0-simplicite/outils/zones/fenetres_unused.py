# Liste les membres privés (const, champs, méthodes) déclarés dans un fichier C#
# et le nombre d'occurrences du nom (mot entier) dans ce fichier.
# Un compte de 1 = déclaration seule ; pour un champ, create/destroy seuls donnent 2-3.
import re, sys, pathlib

SRC = pathlib.Path(r"D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src")
FILES = ["SettingsWindow.cs", "OnboardingWindow.cs", "AboutWindow.cs", "PauseDurationDialog.cs",
         "LayoutConflictWindow.cs", "MaintainableLayersWindow.cs"]

decl = re.compile(r"^\s*(?:private|internal)\s+(?:static\s+)?(?:readonly\s+)?(?:const\s+)?[\w<>\[\]\.,\s\(\)\?]+?\s+(\w+)\s*(?:=|;|\(|=>)")

for f in FILES:
    text = (SRC / f).read_text(encoding="utf-8")
    names = []
    for i, line in enumerate(text.splitlines(), 1):
        m = decl.match(line)
        if m:
            names.append((m.group(1), i))
    print(f"=== {f}")
    for name, line in names:
        n = len(re.findall(r"\b" + re.escape(name) + r"\b", text))
        if n <= 3:
            print(f"  {n}  {name}  (l.{line})")
