import re, pathlib, sys
p = pathlib.Path(sys.argv[1])
lines = p.read_text(encoding="utf-8-sig").splitlines()
hdr = re.compile(r"^    (?:(?:private|public|internal|protected|static|readonly|override|sealed|async)\s+)+[\w<>\[\]?,.() ]+?\s+(\w+)\s*(\(|=>|\{|$)")
starts = []
for i, l in enumerate(lines, 1):
    m = hdr.match(l)
    if m and not l.strip().startswith("//") and "const " not in l and not re.match(r"^\s+(private|public|internal)\s+(readonly\s+)?[\w<>?.,\[\] ]+\s+_\w+", l):
        starts.append((i, m.group(1)))
# longueur approx : jusqu'au membre suivant
res = []
for k, (ln, name) in enumerate(starts):
    end = starts[k + 1][0] - 1 if k + 1 < len(starts) else len(lines)
    res.append((end - ln + 1, ln, name))
for n, ln, name in sorted(res, reverse=True)[: int(sys.argv[2]) if len(sys.argv) > 2 else 25]:
    print(f"{n:5d}  L{ln:<5d} {name}")
print("membres:", len(starts), "lignes:", len(lines))
