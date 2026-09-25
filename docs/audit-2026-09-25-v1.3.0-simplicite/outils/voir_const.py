"""Occurrences d'une constante (nom exact ou valeur hex) : python voir_const.py <NOM|0xVALEUR> [...]"""
import json
import os
import sys

here = os.path.dirname(os.path.abspath(__file__))
with open(os.path.join(here, '..', 'metriques.json'), encoding='utf-8') as f:
    d = json.load(f)
dup = d['pinvoke']['constantes_doublons']
for q in sys.argv[1:]:
    if q in dup:
        print(q, dup[q])
    else:
        print(q, '(pas de doublon de nom)')
