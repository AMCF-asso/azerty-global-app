"""Affiche une clé de metriques.json : python voir.py <cle1/cle2/...> [max_elements]"""
import json
import os
import sys

here = os.path.dirname(os.path.abspath(__file__))
with open(os.path.join(here, '..', 'metriques.json'), encoding='utf-8') as f:
    data = json.load(f)
node = data
for k in sys.argv[1].split('/'):
    if not k:
        continue
    node = node[int(k)] if isinstance(node, list) else node[k]
limit = int(sys.argv[2]) if len(sys.argv) > 2 else 60
if isinstance(node, list):
    for x in node[:limit]:
        print(json.dumps(x, ensure_ascii=False))
    print(f'... {len(node)} éléments')
elif isinstance(node, dict):
    for i, (k, v) in enumerate(node.items()):
        if i >= limit:
            print(f'... {len(node)} clés')
            break
        print(k, '=', json.dumps(v, ensure_ascii=False)[:600])
else:
    print(node)
