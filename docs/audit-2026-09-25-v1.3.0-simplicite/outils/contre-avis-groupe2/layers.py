import json, collections
p = "D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/character-index.json"
d = json.load(open(p, encoding="utf-8"))
print(type(d), len(d))
first = next(iter(d.items())) if isinstance(d, dict) else d[0]
print(str(first)[:600])
cnt = collections.Counter()
cntrec = collections.Counter()
dkact = collections.Counter()
def walk_methods(entry):
    ms = entry.get("methods") if isinstance(entry, dict) else None
    return ms or []
print(list(d.keys())); items = d["characters"].items()
for k, e in items:
    if not isinstance(e, dict): continue
    for m in walk_methods(e):
        t = m.get("type"); l = m.get("layer")
        cnt[(t, l)] += 1
        if m.get("recommended") is True: cntrec[(t, l)] += 1
        if m.get("dkActivationLayer") is not None: dkact[m.get("dkActivationLayer")] += 1
print("ALL:"); [print(" ", k, v) for k, v in sorted(cnt.items(), key=lambda x: -x[1])]
print("RECOMMENDED:"); [print(" ", k, v) for k, v in sorted(cntrec.items(), key=lambda x: -x[1])]
print("DKACT:", dkact)
