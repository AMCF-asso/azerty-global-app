"""Chiffres clés tirés de metriques.json (pour les notes) : python chiffres.py"""
import collections
import json
import os

here = os.path.dirname(os.path.abspath(__file__))
with open(os.path.join(here, '..', 'metriques.json'), encoding='utf-8') as f:
    d = json.load(f)
p = d['agregat_production']
t = d['agregat_tests']
print('PROD', p['fichiers'], 'fichiers', p['lignes'], 'méthodes', p['methodes'], 'médiane', p['longueur_methode_mediane'],
      '>100', p['methodes_plus_100'], '>150', p['methodes_plus_150'], 'lignes>100', p['lignes_dans_methodes_plus_100'],
      'cc>20', p['methodes_cc_sup_20'], 'cc>50', p['methodes_cc_sup_50'], 'densité com', p['densite_commentaires'])
print('TESTS', t['fichiers'], 'fichiers', t['lignes'], 'méthodes', t['methodes'], 'tests', d['tests']['nb_tests_fact_theory'])
top = d['types_top15_lignes']
big = [x for x in top if x['lignes'] >= 1900]
print('god classes >=1900 l.:', [(x['type'], x['lignes']) for x in big], 'somme', sum(x['lignes'] for x in big),
      'part', round(sum(x['lignes'] for x in big) / p['lignes']['total'], 3))
for k in ('clones_mode_A_litteraux_conserves', 'clones_mode_B_identifiants_abstraits'):
    c = d[k]
    print(k, 'couvertes', c['lignes_couvertes_par_un_clone_inter_fichiers'], '/', c['lignes_normalisees_totales'],
          c['part_couverte'], 'en trop', c['lignes_en_trop_estimees'], 'familles', c['familles'],
          'par zone', c['par_zone_lignes_couvertes'])
    zfam = collections.Counter()
    for fam in c['top_familles']:
        zfam['+'.join(fam['zones'])] += fam['lignes_norm_en_trop']
    print('   en trop par combinaison de zones (top 15 familles):', dict(zfam))
pv = d['pinvoke']
print('PINVOKE', pv['declarations'], pv['par_attribut'], 'distinctes', pv['fonctions_distinctes'], 'multi', pv['fonctions_declarees_plusieurs_fois'],
      'en trop', pv['declarations_en_trop'], '| const win32 redéclarées', pv['constantes_win32_redeclarees'],
      'en trop', pv['constantes_win32_declarations_en_trop'], 'valeurs diff', pv['constantes_win32_valeurs_differentes'],
      '| const style win32 total', pv['constantes_style_win32'], '| palette', pv['palette_CLR']['declarations_CLR'],
      pv['palette_CLR']['valeurs_distinctes'], pv['palette_CLR']['fichiers_declarant_CLR'],
      '| structs', list(pv['structs_delegues_win32_dupliques']))
m = d['code_mort']
print('MORT', m['candidats_morts'], m['candidats_par_categorie'], 'lignes', m['lignes_candidats'],
      '| tests seulement', m['utilises_seulement_par_tests'], m['utilises_seulement_par_tests_lignes'])
k = d['marqueurs']
print('CATCH', k['catch_total'], 'attrape-tout', k['catch_attrape_tout'], 'types', k['catch_par_type'], 'nature', k['catch_par_nature'],
      'muets', k['catch_muets_attrape_tout'], 'muets sans commentaire', k['catch_muets_sans_commentaire'], 'zones', k['catch_muets_par_zone'])
filt = sum(1 for c in k['catch_liste'] if c['filtre_when'])
print('   catch avec filtre when', filt, '| journalisés', sum(1 for c in k['catch_liste'] if c['journalise']))
print('TODO', len(k['todo_fixme_hack']), 'prepro', k['preprocesseur_compte'], 'SuppressMessage', len(k['suppressmessage']))
n = d['nommage']
print('NOMMAGE prod', n['production_identifiants_declares']['repartition'], 'tests', n['tests_noms_de_methodes_de_test']['repartition'],
      n['tests_noms_de_methodes_de_test']['styles'], 'accentués', n['tests_noms_de_methodes_de_test']['noms_accentues'])
r = d['renvois_audit_commentaires_production']
print('RENVOIS prod', r['blocs_de_commentaire_avec_renvoi'], 'expliqués', r['blocs_avec_explication_8_mots_plus'], 'nus', r['blocs_renvoi_nu'],
      r['renvois_par_categorie'], 'introuvables', r['identifiants_introuvables'])
ts = d['tests']
audit_files = [f for f in ts['fichiers'] if f['nomme_par_audit_ou_lot']]
print('TESTS par audit/lot : fichiers', len(audit_files), '/', len(ts['fichiers']), 'tests', sum(f['tests'] for f in audit_files),
      '/', ts['nb_tests_fact_theory'], 'lignes', sum(f['lignes'] for f in audit_files))
print('   nommés par identifiant', len(ts['tests_nommes_par_identifiant']), 'corps identiques', len(ts['corps_de_test_identiques_texte']),
      'structure identique (groupes)', ts['nb_groupes_corps_identiques_structure'], 'clones tests', ts['clones_inter_fichiers_tests'])
for z, v in ts['ratio_par_zone'].items():
    print('   ', z, v)
dens = d['densite_commentaires_par_fichier']
print('densité 0-5 %:', sum(1 for v in dens.values() if v < 0.05), 'fichiers ; >40 %:', sum(1 for v in dens.values() if v > 0.4))
for z, a in d['agregat_par_zone'].items():
    print('ZONE', z, a['fichiers'], a['lignes']['total'], 'com', a['densite_commentaires'], 'méth>100', a['methodes_plus_100'], 'cc>20', a['methodes_cc_sup_20'])
