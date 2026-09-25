"""Usage de chaque membre de TypingEngine.Windows.Win32 et doublons avec AZERTYGlobal.Win32 (src/Win32.cs)."""
import os, re

ROOT = r"D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src"
ENGINE_W32 = os.path.join(ROOT, "TypingEngine.Windows", "Win32.cs")
APP_W32 = os.path.join(ROOT, "Win32.cs")

decl = re.compile(r"public\s+(?:static\s+extern\s+|const\s+)?(?:[\w\.\[\]<>?]+\s+)+?(\w+)\s*[\(=;{]")
struct = re.compile(r"public\s+(?:struct|delegate\s+\w+)\s+(\w+)")

def members(path):
    names = []
    for line in open(path, encoding="utf-8-sig"):
        m = struct.search(line)
        if m:
            names.append(m.group(1)); continue
        m = decl.search(line)
        if m and "class" not in line:
            names.append(m.group(1))
    return names

files = []
for d, _, fs in os.walk(ROOT):
    if "\\bin" in d or "/bin" in d or "\\obj" in d or "/obj" in d:
        continue
    for f in fs:
        if f.endswith(".cs"):
            files.append(os.path.join(d, f))

texts = {f: open(f, encoding="utf-8-sig", errors="replace").read() for f in files}

eng = members(ENGINE_W32)
app = members(APP_W32)
print("Membres de TypingEngine.Windows.Win32 :", len(eng))
unused = []
for n in eng:
    users = []
    for f, t in texts.items():
        if os.path.samefile(f, ENGINE_W32):
            continue
        rel = os.path.relpath(f, ROOT)
        # usage qualifié (Win32.X) côté moteur/tests, ou X seul dans une struct voisine
        if re.search(r"\bWin32\." + n + r"\b", t) or (rel.startswith("TypingEngine") and re.search(r"\b" + n + r"\b", t)):
            users.append(rel)
    eng_users = [u for u in users if u.startswith("TypingEngine.Windows" + os.sep) or u.startswith("TestSupport")]
    print(f"  {n:34} utilisateurs moteur: {len(eng_users):2}  total: {len(users):2}")
    if not eng_users:
        unused.append(n)
print("Sans utilisateur dans TypingEngine.Windows :", unused)

dups = sorted(set(eng) & set(app))
print("Doublons de nom avec src/Win32.cs (AZERTYGlobal.Win32) :", len(dups), dups)
print("Membres de src/Win32.cs :", len(app))
