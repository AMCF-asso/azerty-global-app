"""Valide constats-transversal.json contre le schéma de la grille : python valider_constats.py"""
import collections
import json
import os

here = os.path.dirname(os.path.abspath(__file__))
path = os.path.join(here, '..', 'constats-transversal.json')
with open(path, encoding='utf-8') as f:
    data = json.load(f)
REQ = ['id', 'zone', 'fichiers', 'axe', 'etiquette', 'gravite', 'titre', 'constat', 'preuve', 'proposition', 'gain',
       'risque', 'cible', 'lecon_v2', 'confiance']
ENUM = {'axe': {'exec', 'eco', 'lisi'}, 'etiquette': {'delete', 'stdlib', 'native', 'yagni', 'shrink', 'perf', 'clarte'},
        'gravite': {'majeur', 'moyen', 'mineur'}, 'cible': {'1.4.0', 'v2', 'les deux'},
        'confiance': {'élevée', 'moyenne', 'faible'}}
ok = True
for c in data:
    for k in REQ:
        if k not in c:
            print(c.get('id'), 'manque', k)
            ok = False
    for k, vals in ENUM.items():
        if c.get(k) not in vals:
            print(c.get('id'), k, 'invalide :', c.get(k))
            ok = False
    g = c.get('gain', {})
    if set(g) != {'lignes', 'exec', 'nature'} or g.get('nature') not in ('preuve', 'estimation'):
        print(c.get('id'), 'gain invalide', g)
        ok = False
print('valide' if ok else 'ERREURS', len(data), 'constats', dict(collections.Counter(c['gravite'] for c in data)),
      'gain total', sum(c['gain']['lignes'] for c in data))
