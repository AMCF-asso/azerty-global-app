"""Titres et fichiers des constats des autres zones (pour recaler zones et recoupements).
python voir_autres_zones.py"""
import glob
import json
import os

here = os.path.dirname(os.path.abspath(__file__))
for path in sorted(glob.glob(os.path.join(here, '..', 'constats-*.json'))):
    if path.endswith('constats-transversal.json'):
        continue
    with open(path, encoding='utf-8') as f:
        try:
            data = json.load(f)
        except json.JSONDecodeError as e:
            print(os.path.basename(path), 'JSON invalide', e)
            continue
    files = set()
    print('=====', os.path.basename(path), len(data), 'constats')
    for c in data:
        for fi in c.get('fichiers', []):
            files.add(fi.split(':')[0])
        print(f"  {c.get('id')} [{c.get('gravite')}/{c.get('etiquette')}] {c.get('titre')[:110]}")
    print('  fichiers cités :', sorted(files))
