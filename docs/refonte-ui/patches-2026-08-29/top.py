import sys, glob
from collections import Counter
from PIL import Image
for path in sorted(glob.glob(sys.argv[1])):
    im = Image.open(path).convert("RGB")
    w, h = im.size
    c = Counter(im.getdata())
    name = path.split("/")[-1]
    top = "  ".join(f"#{r:02X}{g:02X}{b:02X} {100*n/(w*h):4.1f}%" for (r,g,b), n in c.most_common(4))
    print(f"{name:34s} {top}")
