import sys
from collections import Counter
from PIL import Image
for path in sys.argv[1:]:
    im = Image.open(path)
    print(f"\n{path}\n  mode={im.mode} size={im.size}")
    if im.mode != "RGBA":
        im2 = im.convert("RGBA")
    else:
        im2 = im
    a = Counter(p[3] for p in im2.getdata())
    print("  alpha :", "  ".join(f"{k}:{v}" for k, v in a.most_common(6)))
    # couleurs des pixels opaques vs transparents
    px = im2.load()
    w, h = im2.size
    op = Counter(); tr = Counter()
    for y in range(h):
        for x in range(w):
            r, g, b, al = px[x, y]
            (op if al == 255 else tr)[(r, g, b)] += 1
    print("  opaques  :", "  ".join(f"#{r:02X}{g:02X}{b:02X}:{n}" for (r,g,b), n in op.most_common(4)))
    print("  alpha<255:", "  ".join(f"#{r:02X}{g:02X}{b:02X}:{n}" for (r,g,b), n in tr.most_common(4)))
