"""Mesure a quel point le commit 452aab0^ correspond au binaire 1.1.0.0 reellement
expedie sur le Microsoft Store.

Toute la base du diff v1.1.0 -> v1.2.0 repose sur l'hypothese que 452aab0^
(instantane du 2026-07-27) est proche de la 1.1.0 publiee le 2026-07-23. Le depot
ne porte aucun tag v1.1.0 et le package a ete produit hors depot : cette
hypothese doit etre MESUREE, pas supposee.

Methode : extraire les chaines UTF-16LE du binaire AOT installe, puis y chercher
les chaines litterales de Localization/ prises a deux points de l'histoire.
Attendu si l'hypothese tient : taux eleve pour 452aab0^, et zero chaine
exclusive a HEAD deja presente dans le binaire.

Rejouable : python witness-baseline.py
"""
import re
import subprocess
import sys
import pathlib

REPO = pathlib.Path(__file__).resolve().parents[2]
EXE = pathlib.Path(r"C:\Program Files\WindowsApps"
                   r"\AZERTYGlobal.AZERTYGlobal_1.1.0.0_x64__w9kghr08zmhbg"
                   r"\AZERTY Global.exe")

BACKSLASH = chr(92)


def utf16_strings(path, minlen=8):
    """Chaines UTF-16LE lisibles du binaire AOT."""
    raw = path.read_bytes()
    out = set()
    pattern = rb'(?:[\x20-\x7e\xa0-\xff]\x00){%d,}' % minlen
    for m in re.finditer(pattern, raw):
        try:
            out.add(m.group().decode('utf-16-le'))
        except UnicodeDecodeError:
            pass
    return out


def loc_strings(rev):
    """Chaines litterales simples des fichiers Localization/ a une revision donnee."""
    listing = subprocess.run(
        ['git', 'ls-tree', '-r', '--name-only', rev, 'src/Localization/'],
        cwd=REPO, capture_output=True, text=True, check=True).stdout.splitlines()
    vals = set()
    for f in listing:
        if not f.endswith('.cs'):
            continue
        blob = subprocess.run(['git', 'show', f'{rev}:{f}'],
                              cwd=REPO, capture_output=True, check=True).stdout
        src = blob.decode('utf-8')
        # chaines a guillemets, non verbatim, non interpolees, 12 caracteres minimum
        for m in re.finditer(r'(?<!@)(?<!\$)"((?:[^"\n]){12,}?)"', src):
            s = m.group(1)
            if BACKSLASH in s or '{' in s:
                continue
            vals.add(s)
    return vals


def main():
    if not EXE.exists():
        sys.exit("ECHEC : binaire 1.1.0.0 introuvable -> %s" % EXE)

    blob = utf16_strings(EXE)
    print("Binaire 1.1.0.0 : %d octets, %d chaines UTF-16 distinctes de 8+ caracteres"
          % (EXE.stat().st_size, len(blob)))

    # Temoin de l'extracteur : une chaine dont on sait qu'elle est dans le binaire,
    # et une dont on sait qu'elle n'y est pas. Sans ce couple, un taux de 0 %
    # serait indistinguable d'un extracteur casse.
    must_find = "AZERTY Global"
    must_not_find = "chaine temoin absente volontairement xyzzy"
    ok_pos = any(must_find in s for s in blob)
    ok_neg = not any(must_not_find in s for s in blob)
    print("TEMOIN extracteur : trouve %r -> %s | rejette une chaine inventee -> %s"
          % (must_find, ok_pos, ok_neg))
    if not (ok_pos and ok_neg):
        sys.exit("ECHEC : l'extracteur de chaines n'est pas fiable, tout taux ci-dessous mentirait")

    def rate(label, rev):
        vals = loc_strings(rev)
        hit = {v for v in vals if any(v in s for s in blob)}
        pct = 100.0 * len(hit) / len(vals) if vals else 0.0
        print("\n%s (%s)" % (label, rev))
        print("  chaines Localization extraites   : %d" % len(vals))
        print("  presentes dans le binaire 1.1.0.0 : %d  (%.1f %%)" % (len(hit), pct))
        return vals, hit

    base_vals, base_hit = rate("BASE presumee", "452aab0^")
    head_vals, head_hit = rate("CIBLE v1.2.0", "HEAD")

    only_head = head_vals - base_vals
    only_head_hit = {v for v in only_head if any(v in s for s in blob)}
    print("\nChaines EXCLUSIVES a HEAD (ajoutees apres la base) : %d" % len(only_head))
    print("  dont deja presentes dans le binaire 1.1.0.0 : %d" % len(only_head_hit))
    if only_head_hit:
        print("  ATTENTION : deja expediees en 1.1.0, la base les compte donc a tort comme neuves :")
        for v in sorted(only_head_hit)[:25]:
            print("    - %s" % v[:110])

    only_base = base_vals - head_vals
    print("\nChaines de la base SUPPRIMEES a HEAD : %d" % len(only_base))
    for v in sorted(only_base)[:30]:
        mark = "expediee" if any(v in s for s in blob) else "jamais expediee"
        print("    - [%s] %s" % (mark, v[:100]))

    missing = sorted(base_vals - base_hit)
    print("\nChaines de la base ABSENTES du binaire 1.1.0.0 : %d" % len(missing))
    print("  (un nombre eleve invalide l'hypothese de base)")
    for v in missing[:30]:
        print("    - %s" % v[:110])


if __name__ == '__main__':
    main()
