"""Delta REEL du texte visible entre la 1.1.0 expediee et la v1.2.0 du depot.

Le depot ne contient aucune revision egale a la 1.1.0 : `452aab0^` est anterieur
a elle (ni Localization/, ni StoreReview.cs, ni ProductIdentity.cs). Le seul
separateur fiable est donc le binaire 1.1.0.0 installe depuis le Store.

Une chaine de HEAD absente du binaire est neuve OU reformulee. Une chaine
presente etait deja expediee en 1.1.0 et n'appartient pas au delta 1.2.0.

Rejouable : python delta-textes.py
"""
import re
import subprocess
import sys
import pathlib
import json

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[1]
EXE = pathlib.Path(r"C:\Program Files\WindowsApps"
                   r"\AZERTYGlobal.AZERTYGlobal_1.1.0.0_x64__w9kghr08zmhbg"
                   r"\AZERTY Global.exe")
BACKSLASH = chr(92)


def utf16_blob(path, minlen=6):
    raw = path.read_bytes()
    out = set()
    for m in re.finditer(rb'(?:[\x20-\x7e\xa0-\xff]\x00){%d,}' % minlen, raw):
        try:
            out.add(m.group().decode('utf-16-le'))
        except UnicodeDecodeError:
            pass
    return out


def head_loc_entries():
    """(fichier, ligne, chaine) pour chaque litteral de src/Localization/ a HEAD."""
    files = subprocess.run(
        ['git', 'ls-tree', '-r', '--name-only', 'HEAD', 'src/Localization/'],
        cwd=REPO, capture_output=True, text=True, check=True).stdout.splitlines()
    out = []
    for f in files:
        if not f.endswith('.cs'):
            continue
        src = subprocess.run(['git', 'show', 'HEAD:%s' % f],
                             cwd=REPO, capture_output=True, check=True
                             ).stdout.decode('utf-8')
        for lineno, line in enumerate(src.splitlines(), 1):
            for m in re.finditer(r'(?<!@)(?<!\$)"((?:[^"\n]){12,}?)"', line):
                s = m.group(1)
                if BACKSLASH in s or '{' in s:
                    continue
                out.append((f, lineno, s))
    return out


if not EXE.exists():
    sys.exit("ECHEC : binaire 1.1.0.0 introuvable")

blob = utf16_blob(EXE)
haystack = "\n".join(blob)
if "AZERTY Global" not in haystack:
    sys.exit("ECHEC : temoin absent, extracteur non fiable")
if "temoin invente xyzzy quux" in haystack:
    sys.exit("ECHEC : temoin negatif trouve, extracteur non fiable")

entries = head_loc_entries()
shipped, new = [], []
for f, ln, s in entries:
    (shipped if s in haystack else new).append((f, ln, s))

uniq_new = sorted({s for _, _, s in new})
uniq_shipped = sorted({s for _, _, s in shipped})
print("Litteraux Localization/ a HEAD : %d occurrences, %d chaines distinctes"
      % (len(entries), len({s for _, _, s in entries})))
print("  deja dans le binaire 1.1.0.0 (hors delta) : %d distinctes" % len(uniq_shipped))
print("  ABSENTES du binaire 1.1.0.0 (delta v1.2.0) : %d distinctes" % len(uniq_new))

with open(HERE / 'delta-textes.md', 'w', encoding='utf-8') as fh:
    fh.write("# Delta du texte visible - 1.1.0 expediee vers v1.2.0 du depot\n\n")
    fh.write("Genere par `delta-textes.py`. Reference 1.1.0 : les chaines UTF-16 du binaire\n")
    fh.write("`AZERTYGlobal.AZERTYGlobal_1.1.0.0_x64__w9kghr08zmhbg\AZERTY Global.exe`,\n")
    fh.write("et non un commit : le depot n'en contient aucun qui soit egal a la 1.1.0.\n\n")
    fh.write("Chaine absente du binaire = neuve ou reformulee en v1.2.0.\n")
    fh.write("Une chaine construite a l'execution (interpolation, concatenation) peut\n")
    fh.write("apparaitre a tort comme neuve : ces cas sont a lever a la main.\n\n")
    fh.write("| # | Fichier | Ligne | Chaine |\n|---|---|---|---|\n")
    for i, (f, ln, s) in enumerate(sorted(new, key=lambda t: (t[0], t[1])), 1):
        cell = s.replace('|', BACKSLASH + '|')
        fh.write("| %d | `%s` | %d | %s |\n" % (i, f.split('/')[-1], ln, cell))

with open(HERE / 'delta-textes.json', 'w', encoding='utf-8') as fh:
    json.dump({'shipped_1_1_0': uniq_shipped, 'delta_1_2_0': uniq_new},
              fh, ensure_ascii=False, indent=1)

print("\nEcrit : delta-textes.md et delta-textes.json")
print("\n--- delta, 60 premieres ---")
for f, ln, s in sorted(new, key=lambda t: (t[0], t[1]))[:60]:
    print("  %-22s :%-5d %s" % (f.split('/')[-1], ln, s[:96]))
