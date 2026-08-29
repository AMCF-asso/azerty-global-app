import sys
from collections import Counter
from PIL import Image

path = sys.argv[1]
im = Image.open(path).convert("RGB")
w, h = im.size
print(f"{path}  {w}x{h}")
c = Counter(im.getdata())
print("Top 8 couleurs :")
for rgb, n in c.most_common(8):
    print(f"  #{rgb[0]:02X}{rgb[1]:02X}{rgb[2]:02X}  {n:7d}  {100*n/(w*h):5.1f} %")
