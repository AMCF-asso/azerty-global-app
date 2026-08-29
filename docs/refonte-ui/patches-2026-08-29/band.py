import sys
from collections import Counter
from PIL import Image
im = Image.open(sys.argv[1]).convert("RGB")
w, h = im.size
px = im.load()
print(f"{w}x{h}")
for y in range(0, h, 8):
    row = Counter(px[x, y] for x in range(w))
    top = "  ".join(f"#{r:02X}{g:02X}{b:02X}:{n}" for (r, g, b), n in row.most_common(3))
    print(f"y={y:4d}  {top}")
