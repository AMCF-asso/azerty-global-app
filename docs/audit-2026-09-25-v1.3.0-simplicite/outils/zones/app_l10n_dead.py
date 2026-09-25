import re, pathlib, collections
root = pathlib.Path(r"D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src")
loc = sorted((root / "Localization").glob("*.cs"))
member = re.compile(r"^\s*(?:public|internal)\s+static\s+[\w<>\[\]?,. ]+?\s+(\w+)\s*(?:=>|\(|\{)", re.M)

defs = {}  # name -> (file, line)
per_file = collections.Counter()
for p in loc:
    txt = p.read_text(encoding="utf-8-sig")
    for m in member.finditer(txt):
        name = m.group(1)
        line = txt.count("\n", 0, m.start()) + 1
        defs[name] = (p.name, line)
        per_file[p.name] += 1

def collect(paths):
    return "\n".join(pp.read_text(encoding="utf-8-sig") for pp in paths)

prod_files = [p for p in root.glob("*.cs")]
prod = collect(prod_files)
test_files = [p for p in (root / "AZERTYGlobal.Tests").glob("*.cs")]
tests = collect(test_files)
loc_txt = {p.name: p.read_text(encoding="utf-8-sig") for p in loc}

dead, test_only, loc_only = [], [], []
for name, (f, line) in sorted(defs.items(), key=lambda kv: (kv[1][0], kv[1][1])):
    if name in ("Language", "IsEnglish", "DisplayCulture", "FormatDate"):
        pass
    rx = re.compile(r"\bL\." + re.escape(name) + r"\b")
    in_prod = bool(rx.search(prod))
    # usage inside localization files (sans prefixe L.) hors de sa propre definition
    rx_bare = re.compile(r"(?<![\w.])" + re.escape(name) + r"\b")
    inner = 0
    for fn, t in loc_txt.items():
        inner += len(rx_bare.findall(t))
    inner -= 1  # la definition
    in_tests = bool(rx.search(tests))
    if not in_prod and inner <= 0:
        (test_only if in_tests else dead).append((name, f, line))
    elif not in_prod and inner > 0:
        loc_only.append((name, f, line, inner))

print("membres L definis:", len(defs), dict(per_file))
print("\nMORTS (aucun usage prod ni interne ni test):", len(dead))
for d in dead: print("  ", d)
print("\nTESTS SEULEMENT:", len(test_only))
for d in test_only: print("  ", d)
print("\nUTILISES SEULEMENT DANS Localization/ (composition):", len(loc_only))
for d in loc_only: print("  ", d)
