"""Mesure CRLF / LF / BOM / non-ASCII des fichiers passés en argument (règle 1 du dépôt app)."""
import pathlib, sys
racine = pathlib.Path("D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store")
for rel in sys.argv[1:]:
    b = (racine / rel).read_bytes()
    c = b.count(b"\r\n")
    print(f"{rel}: CRLF {c}, LF {b.count(b'\n') - c}, BOM {b.startswith(b'\xef\xbb\xbf')}, nonASCII {sum(1 for x in b if x > 127)}")
