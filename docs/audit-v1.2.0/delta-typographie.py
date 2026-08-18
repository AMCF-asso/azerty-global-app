"""Separe, dans le delta de 123 chaines, ce qui est du texte NEUF de ce qui est la
MEME phrase retypographiee (insecable, apostrophe courbe, tirets).

Sans cette passe, un simple passage de l'espace ordinaire a l'insecable gonfle le
delta et fait croire a des fonctions nouvelles la ou rien n'a change pour
l'utilisateur.

Rejouable : python delta-typographie.py
"""
import io
import json
import re
import sys
import pathlib

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
HERE = pathlib.Path(__file__).resolve().parent
EXE = pathlib.Path(r"C:\Program Files\WindowsApps"
                   r"\AZERTYGlobal.AZERTYGlobal_1.1.0.0_x64__w9kghr08zmhbg"
                   "/AZERTY Global.exe")

TRANS = {
    0x00a0: ' ',   # espace insecable
    0x202f: ' ',   # insecable fine
    0x2019: "'",   # apostrophe courbe
    0x2018: "'",
    0x201c: '"', 0x201d: '"',
    0x2011: '-',   # tiret insecable
    0x2013: '-', 0x2014: '-',
    0x2026: '...',
}


def norm(s):
    return re.sub(r'\s+', ' ', s.translate(TRANS)).strip().casefold()


raw = EXE.read_bytes()
blob = set()
for m in re.finditer(rb'(?:[\x20-\x7e\xa0-\xff]\x00){6,}', raw):
    try:
        blob.add(m.group().decode('utf-16-le'))
    except UnicodeDecodeError:
        pass
hay_exact = "\n".join(blob)
hay_norm = "\n".join(norm(s) for s in blob)

# Temoin : une chaine dont la seule difference est typographique doit passer en
# normalise et echouer en exact. Sans ce couple, un taux de 0 ne se distingue pas
# d'un normalisateur casse.
probe = None
for s in blob:
    if '\u00a0' in s or '\u2019' in s:
        probe = s
        break
if probe is None:
    sys.exit("ECHEC : aucune chaine typographiee dans le binaire, temoin impossible")
mutated = probe.replace('\u00a0', ' ').replace('\u2019', "'")
assert mutated != probe, "temoin invalide : la mutation n'a rien change"
print("TEMOIN normalisateur")
print("  chaine mutee retrouvee en EXACT     : %s   (attendu False)" % (mutated in hay_exact))
print("  chaine mutee retrouvee en NORMALISE : %s   (attendu True)" % (norm(mutated) in hay_norm))
if mutated in hay_exact or norm(mutated) not in hay_norm:
    sys.exit("ECHEC : le normalisateur ne fait pas ce qu'on croit, tout chiffre ci-dessous mentirait")

delta = json.load(open(HERE / 'delta-textes.json', encoding='utf-8'))['delta_1_2_0']
retypo, vraiment_neuf = [], []
for s in delta:
    (retypo if norm(s) in hay_norm else vraiment_neuf).append(s)

print("\nDelta brut mesure contre le binaire 1.1.0.0 : %d chaines" % len(delta))
print("  meme phrase, typographie changee seulement : %d" % len(retypo))
print("  texte reellement neuf ou reformule         : %d" % len(vraiment_neuf))

with open(HERE / 'delta-textes-affine.md', 'w', encoding='utf-8') as fh:
    fh.write("# Delta du texte visible, affine par la typographie\n\n")
    fh.write("Reference 1.1.0 : chaines UTF-16 du binaire Store installe.\n")
    fh.write("Denominateur : 382 litteraux distincts de `src/Localization/` a HEAD,\n")
    fh.write("dont 259 deja presents dans le binaire 1.1.0.0.\n\n")
    fh.write("## Texte reellement neuf ou reformule (%d)\n\n" % len(vraiment_neuf))
    for s in sorted(vraiment_neuf):
        fh.write("- %s\n" % s)
    fh.write("\n## Meme phrase, typographie seule (%d)\n\n" % len(retypo))
    fh.write("Aucune consequence fonctionnelle, mais chaque ligne est policee par\n")
    fh.write("`FrenchTypographyTests` : a verifier au build, pas au smoke test.\n\n")
    for s in sorted(retypo):
        fh.write("- %s\n" % s)

print("\nEcrit : delta-textes-affine.md")
print("\n--- texte reellement neuf, 45 premieres ---")
for s in sorted(vraiment_neuf)[:45]:
    print("  %s" % s[:100])
