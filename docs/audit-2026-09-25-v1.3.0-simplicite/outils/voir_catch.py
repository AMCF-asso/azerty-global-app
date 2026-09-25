"""Liste les catch de metriques.json avec contexte : python voir_catch.py [muets|tous] [racine_depot]"""
import json
import os
import sys

here = os.path.dirname(os.path.abspath(__file__))
with open(os.path.join(here, '..', 'metriques.json'), encoding='utf-8') as f:
    data = json.load(f)
mode = sys.argv[1] if len(sys.argv) > 1 else 'muets'
root = sys.argv[2] if len(sys.argv) > 2 else None
lst = data['marqueurs']['catch_liste']
if mode == 'muets':
    lst = [c for c in lst if c['attrape_tout'] and not c['relance'] and not c['journalise']
           and c['nature'] in ('vide', 'retour_par_defaut')]
for c in lst:
    print(f"{c['emplacement']} [{c['zone']}] {c['type']} {c['nature']} meth={c['methode']} "
          f"com_bloc={c['commentaire_dans_bloc']} com_proche={c['commentaire_proche']} log={c['journalise']} corps={c['corps']!r}")
    if root:
        rel, ln = c['emplacement'].rsplit(':', 1)
        ln = int(ln)
        with open(os.path.join(root, rel), encoding='utf-8-sig') as g:
            lines = g.read().split('\n')
        for k in range(max(0, ln - 4), min(len(lines), ln + 2)):
            print('    ', k + 1, lines[k][:150])
print(len(lst), 'catch')
