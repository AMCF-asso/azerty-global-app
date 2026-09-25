import sys, re, subprocess, collections
sys.path.insert(0, "C:/Users/antoi/AppData/Local/Temp/claude/D--My-files/3f079c8a-fda9-4162-b849-8be427b39360/scratchpad/audit/outils")
import cslex
repo = "D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store"
files = subprocess.run(["git", "ls-tree", "-r", "--name-only", "f0a98ba", "--", "src"], cwd=repo, capture_output=True, text=True, encoding="utf-8").stdout.splitlines()
prod = [f for f in files if f.endswith(".cs") and "Tests" not in f and "TestSupport" not in f]
c = collections.Counter()
for f in prod:
    t = subprocess.run(["git", "show", f"f0a98ba:{f}"], cwd=repo, capture_output=True, text=True, encoding="utf-8", errors="replace").stdout
    ns = cslex.lex(t)["ns"]
    for m in re.finditer(r"\bcatch\b\s*(\(([^)]*)\))?\s*(when)?", ns):
        typ = (m.group(2) or "").strip()
        if m.group(3):
            k = "filtre when"
        elif not m.group(1):
            k = "nu"
        elif re.fullmatch(r"(System\.)?Exception(\s+\w+)?", typ):
            k = "Exception"
        else:
            k = "type:" + typ
        c[k] += 1
print(len(prod), "fichiers prod")
print(sum(c.values()), dict(c))
