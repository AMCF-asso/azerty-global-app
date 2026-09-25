"""Familles de clones en une ligne chacune : python familles.py [A|B] [n]"""
import json
import os
import sys

here = os.path.dirname(os.path.abspath(__file__))
with open(os.path.join(here, '..', 'metriques.json'), encoding='utf-8') as f:
    d = json.load(f)
mode = sys.argv[1] if len(sys.argv) > 1 else 'A'
n = int(sys.argv[2]) if len(sys.argv) > 2 else 10
key = 'clones_mode_A_litteraux_conserves' if mode == 'A' else 'clones_mode_B_identifiants_abstraits'
for i, fam in enumerate(d[key]['top_familles'][:n], start=1):
    occ = ', '.join(f"{o['fichier'].replace('src/', '')}:{o['lignes']}" for o in fam['occurrences'])
    print(f"{i}. en trop={fam['lignes_norm_en_trop']} total={fam['lignes_norm_totales']} zones={'+'.join(fam['zones'])} "
          f"| {fam['extrait'][0][:70]} | {occ}")
