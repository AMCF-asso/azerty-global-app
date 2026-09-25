import re, pathlib, collections, sys
root = pathlib.Path(r"D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src")
app = (root / "Win32.cs").read_text(encoding="utf-8-sig")
eng = (root / "TypingEngine.Windows/Win32.cs").read_text(encoding="utf-8-sig")

ext = re.compile(r"extern\s+[\w<>\[\]?.]+\s+(\w+)\s*\(")
const = re.compile(r"(?:public|internal)\s+const\s+\w+\s+(\w+)\s*=")
struct = re.compile(r"(?:public|internal)\s+struct\s+(\w+)")
deleg = re.compile(r"(?:public|internal)\s+delegate\s+[\w<>]+\s+(\w+)\s*\(")

def names(rx, s):
    return [m.group(1) for m in rx.finditer(s)]

for label, rx in [("extern", ext), ("const", const), ("struct", struct), ("delegate", deleg)]:
    a, e = names(rx, app), names(rx, eng)
    common = sorted(set(a) & set(e))
    print(f"{label}: app={len(a)} (uniques {len(set(a))}) engine={len(e)} common={len(common)}")
    print("  common:", ", ".join(common))

# P/Invoke outside Win32.cs in the app zone (DllImport in other files)
print()
files = [p for p in root.glob("*.cs")]
cnt = collections.Counter()
for p in files:
    t = p.read_text(encoding="utf-8-sig")
    n = len(re.findall(r"\[DllImport\(|\[LibraryImport\(", t))
    if n and p.name != "Win32.cs":
        cnt[p.name] = n
print("DllImport hors Win32.cs (racine src):", dict(cnt), "total", sum(cnt.values()))

# Usage count of each app Win32 extern across the app project (src/*.cs, excluding tests/engine)
allsrc = "\n".join(p.read_text(encoding="utf-8-sig") for p in files if p.name != "Win32.cs")
unused = []
for n in sorted(set(names(ext, app))):
    if not re.search(r"Win32\." + re.escape(n) + r"\b", allsrc):
        unused.append(n)
print("extern de src/Win32.cs jamais appeles via Win32.X dans src/*.cs:", len(unused), unused)
unusedc = []
for n in sorted(set(names(const, app))):
    if not re.search(r"\b" + re.escape(n) + r"\b", allsrc):
        unusedc.append(n)
print("const de src/Win32.cs jamais citees dans src/*.cs:", len(unusedc), unusedc)
uns = []
for n in sorted(set(names(struct, app))):
    if not re.search(r"\b" + re.escape(n) + r"\b", allsrc):
        uns.append(n)
print("struct de src/Win32.cs jamais citees dans src/*.cs:", len(uns), uns)
