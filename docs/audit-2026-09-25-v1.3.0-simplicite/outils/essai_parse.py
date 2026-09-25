"""Contrôle visuel du découpage : python essai_parse.py <fichier.cs> [filtre_kind]"""
import sys
from cslex import FileModel

path = sys.argv[1]
flt = sys.argv[2] if len(sys.argv) > 2 else None
with open(path, encoding='utf-8-sig') as f:
    fm = FileModel(path, f.read())
print('types:', [(t['kind'], t['name'], t['start'], t['end']) for t in fm.types])
for m in fm.members:
    if flt and m['kind'] != flt:
        continue
    extra = ''
    if m['kind'] == 'methode':
        extra = f" lignes={m['lignes']} cc={m['cc']} prof={m['profondeur']} expr={m['expr']}"
    print(f"{m['kind']:12} {m['owner']}.{m['name']} L{m['start']}-{m['end']}{extra}")
print(fm.line_stats())
braces_open = fm.ns.count('{')
print('accolades', braces_open, fm.ns.count('}'), 'appariées', len(fm.match))
