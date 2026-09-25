"""Procédures de fenêtre (WndProc, *SubclassProc) : taille et complexité par fichier.
python wndproc.py <racine_depot>   (aussi importé par metriques.py)"""
import os
import re
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from cslex import FileModel  # noqa: E402

NAME_RE = re.compile(r'^(WndProc|WndProcCallback|\w*SubclassProc)$')


def mesurer(root):
    files = [f for f in subprocess.run(['git', '-C', root, 'ls-files', 'src'], capture_output=True, text=True,
                                       encoding='utf-8').stdout.splitlines()
             if f.endswith('.cs') and '.Tests/' not in f and 'TestSupport' not in f]
    rows = []
    for rel in files:
        with open(os.path.join(root, rel), encoding='utf-8-sig') as f:
            fm = FileModel(rel, f.read())
        for m in fm.members:
            if m['kind'] == 'methode' and NAME_RE.match(m['name']):
                body = fm.ns[m['body'][0]:m['body'][1]]
                cases = len(re.findall(r'\bcase\s+(?:Win32\.)?WM_\w+', body))
                rows.append({'methode': f"{m['owner']}.{m['name']}", 'emplacement': f"{rel}:{m['start']}-{m['end']}",
                             'lignes': m['lignes'], 'cc': m['cc'], 'case_WM': cases})
    rows.sort(key=lambda r: -r['lignes'])
    return {'procedures': len(rows), 'lignes_totales': sum(r['lignes'] for r in rows),
            'plus_100_lignes': sum(1 for r in rows if r['lignes'] > 100),
            'lignes_plus_100': sum(r['lignes'] for r in rows if r['lignes'] > 100),
            'liste': rows}


if __name__ == '__main__':
    res = mesurer(sys.argv[1])
    for r in res['liste']:
        print(f"{r['lignes']:4d} l.  cc={r['cc']:3d}  case WM_*={r['case_WM']:3d}  {r['methode']}  {r['emplacement']}")
    print({k: v for k, v in res.items() if k != 'liste'})
