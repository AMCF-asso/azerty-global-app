import json, sys
p = r"D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src/AZERTY Global 2026.json"
d = json.load(open(p, encoding="utf-8"))
for row in d["rows"]:
    for k in row["keys"]:
        print(k.get("scancode"), k.get("position"), repr(k.get("base")), repr(k.get("shift")), repr(k.get("alt_gr")), repr(k.get("caps")), repr(k.get("caps_shift")))
dk = d.get("dead_keys", {})
print("dead keys:", len(dk))
for name, v in list(dk.items())[:40]:
    t = v.get("table", {})
    print(name, len(t), list(t.items())[:4])
