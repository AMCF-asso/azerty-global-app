"""Diagnostic du découpage : part des lignes de code couvertes par un membre reconnu.
python couverture.py <racine_depot>"""
import os
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from cslex import FileModel  # noqa: E402

root = sys.argv[1]
files = [f for f in subprocess.run(['git', '-C', root, 'ls-files', 'src'], capture_output=True, text=True,
                                   encoding='utf-8').stdout.splitlines() if f.endswith('.cs')]
rows = []
for rel in files:
    with open(os.path.join(root, rel), encoding='utf-8-sig') as f:
        fm = FileModel(rel, f.read())
    code_lines = {k for k, ln in enumerate(fm.nc.split('\n'), start=1) if ln.strip() and ln.strip() not in '{}'}
    covered = set()
    for m in fm.members:
        covered.update(range(m['start'], m['end'] + 1))
    type_hdr = {t['start'] for t in fm.types} | {t['end'] for t in fm.types}
    for t in fm.types:
        if t['kind'] in ('enum', 'delegate', 'interface'):
            covered.update(range(t['start'], t['end'] + 1))
    miss = sorted(code_lines - covered - type_hdr)
    unbalanced = fm.ns.count('{') != fm.ns.count('}')
    rows.append((len(miss), rel, miss[:12], unbalanced))
rows.sort(reverse=True)
for n, rel, miss, unb in rows[:25]:
    print(n, rel, miss, 'DESEQUILIBRE' if unb else '')
print('fichiers déséquilibrés :', sum(1 for r in rows if r[3]))
