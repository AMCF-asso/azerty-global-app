"""Métriques transversales du dépôt AZERTY Global (C#), stdlib seule.

Usage :
    python metriques.py <racine_depot> <sortie.json> [--fenetre 8]

Lecture seule sur le dépôt : liste les fichiers suivis par git (`git ls-files src`),
ne lance aucune commande qui modifie l'état. Toutes les mesures reposent sur le
mini-analyseur `cslex.py` (approximations documentées dans la clé "methode" du JSON).
"""
import collections
import hashlib
import json
import os
import re
import statistics
import subprocess
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from cslex import FileModel  # noqa: E402
import squelette_fenetres  # noqa: E402
import wndproc  # noqa: E402

# ------------------------------------------------------------------ zones

ZONE_RULES = [
    # (préfixe ou nom exact, zone) : premier qui correspond
    ('src/TypingEngine.Core/', 'moteur'),
    ('src/TypingEngine.Windows/', 'moteur'),
    ('src/AzertyGlobalWindowsTypingHost.cs', 'moteur'),
    ('src/Localization/L.Learning.cs', 'lecons'),
    ('src/Localization/L.Lessons.cs', 'lecons'),
    ('src/Localization/L.LessonsWindow.cs', 'lecons'),
    ('src/Localization/L.Challenge.cs', 'lecons'),
    ('src/Localization/L.Keyboard.cs', 'visuel'),
    ('src/Localization/L.Settings.cs', 'fenetres'),
    ('src/Localization/L.About.cs', 'fenetres'),
    ('src/Localization/L.Onboarding.cs', 'fenetres'),
    ('src/Localization/L.Stats.cs', 'fenetres'),
    ('src/Localization/L.Search.cs', 'visuel'),
    ('src/Localization/L.Layers.cs', 'fenetres'),
    ('src/Localization/', 'app'),
]
ZONE_BY_NAME = {
    'lecons': ['LearningModule', 'LearningSessionTracker', 'LessonCatalog', 'LessonHintProvider',
               'LessonProgressStore', 'LessonTypingSession', 'LessonsWindow', 'DailyChallenge', 'ChallengeShare',
               ],
    'fenetres': ['SettingsWindow', 'SettingsScrollState', 'AboutWindow', 'OnboardingWindow',
                 'LayoutConflictWindow', 'MaintainableLayersWindow', 'PauseDurationDialog', 'DialogNavigation',
                 'WindowSizing', 'AccessibleName'],
    'visuel': ['KeyboardRenderer', 'VirtualKeyboard', 'GdiHelpers', 'GdiImageLoader', 'LayerIndicatorWindow', 'CharacterSearch', 'UsageStatsWindow',
               'DisplayGlyph'],
}


def zone_of(rel):
    for pref, z in ZONE_RULES:
        if rel.startswith(pref):
            return z
    base = os.path.splitext(os.path.basename(rel))[0]
    for z, names in ZONE_BY_NAME.items():
        if base in names:
            return z
    return 'app'


def is_test(rel):
    return '.Tests/' in rel or rel.startswith('src/TestSupport/')


def project_of(rel):
    for p in ('AZERTYGlobal.Tests', 'TypingEngine.Core.Tests', 'TypingEngine.Windows.Tests', 'TypingEngine.Core',
              'TypingEngine.Windows', 'TestSupport'):
        if rel.startswith('src/' + p + '/'):
            return p
    return 'AZERTYGlobal'


# ------------------------------------------------------------------ utilitaires

CS_KEYWORDS = set('''abstract as base bool break byte case catch char checked class const continue decimal default
delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int
interface internal is lock long namespace new null object operator out override params private protected public
readonly ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint
ulong unchecked unsafe ushort using virtual void volatile while var async await yield get set init value when and or
not record nint nuint partial global where nameof dynamic managed unmanaged required file with'''.split())

IDENT = re.compile(r'[A-Za-z_]\w*')


def median(xs):
    return statistics.median(xs) if xs else 0


def git_ls(root):
    out = subprocess.run(['git', '-C', root, 'ls-files', 'src'], capture_output=True, text=True, encoding='utf-8',
                         check=True).stdout
    return [ln for ln in out.splitlines() if ln.strip()]


def read(path):
    with open(path, encoding='utf-8-sig', errors='replace') as f:
        return f.read()


# ------------------------------------------------------------------ 1. métriques par fichier

def file_metrics(fm):
    ls = fm.line_stats()
    meths = [m for m in fm.members if m['kind'] == 'methode']
    lens = [m['lignes'] for m in meths]
    ccs = [m['cc'] for m in meths]
    code_plus_com = ls['code'] + ls['commentaire']
    return {
        'lignes': ls,
        'densite_commentaires': round(ls['commentaire'] / code_plus_com, 3) if code_plus_com else 0,
        'methodes': len(meths),
        'methodes_corps_bloc': sum(1 for m in meths if not m['expr']),
        'longueur_methode_mediane': median(lens),
        'longueur_methode_max': max(lens) if lens else 0,
        'methodes_plus_150': [f"{m['owner']}.{m['name']}:{m['start']}-{m['end']} ({m['lignes']} l.)" for m in meths
                              if m['lignes'] > 150],
        'profondeur_max': max([m['profondeur'] for m in meths], default=0),
        'cc_max': max(ccs, default=0),
        'cc_somme': sum(ccs),
        'cc_mediane': median(ccs),
        'methodes_cc_sup_20': sum(1 for c in ccs if c > 20),
        'types': [t['name'] for t in fm.types if t['kind'] != 'delegate'],
        'champs': sum(1 for m in fm.members if m['kind'] == 'champ'),
        'proprietes': sum(1 for m in fm.members if m['kind'] == 'propriete'),
        'constantes': sum(1 for m in fm.members if m['kind'] == 'constante'),
        'pinvoke': sum(1 for m in fm.members if m['kind'] == 'pinvoke'),
    }


def aggregate(fms):
    meths = [m for fm in fms for m in fm.members if m['kind'] == 'methode']
    lens = [m['lignes'] for m in meths]
    ccs = [m['cc'] for m in meths]
    tot = collections.Counter()
    for fm in fms:
        tot.update(fm.line_stats())
    return {
        'fichiers': len(fms),
        'lignes': dict(tot),
        'methodes': len(meths),
        'longueur_methode_mediane': median(lens),
        'longueur_methode_moyenne': round(sum(lens) / len(lens), 1) if lens else 0,
        'longueur_methode_max': max(lens, default=0),
        'methodes_plus_50': sum(1 for x in lens if x > 50),
        'methodes_plus_100': sum(1 for x in lens if x > 100),
        'methodes_plus_150': sum(1 for x in lens if x > 150),
        'lignes_dans_methodes_plus_100': sum(x for x in lens if x > 100),
        'cc_mediane': median(ccs),
        'cc_max': max(ccs, default=0),
        'methodes_cc_sup_20': sum(1 for c in ccs if c > 20),
        'methodes_cc_sup_50': sum(1 for c in ccs if c > 50),
        'profondeur_max': max([m['profondeur'] for m in meths], default=0),
        'methodes_profondeur_sup_4': sum(1 for m in meths if m['profondeur'] > 4),
        'densite_commentaires': round(tot['commentaire'] / (tot['code'] + tot['commentaire']), 3)
        if tot['code'] else 0,
    }


# ------------------------------------------------------------------ 2. clones

TRIVIAL = re.compile(r'^[{}()\[\];,]+$')
NUM = re.compile(r'\b(?:0[xX][0-9A-Fa-f_]+|\d[\d_]*(?:\.\d+)?)(?:[uU][lL]?|[lL][uU]?|[fFdDmM])?\b')


def norm_lines(fm, mode):
    """Liste de (texte normalisé, n° de ligne d'origine) sans lignes triviales."""
    src = fm.nc if mode == 'A' else fm.ns
    out = []
    for k, ln in enumerate(src.split('\n'), start=1):
        s = re.sub(r'\s+', ' ', ln).strip()
        if not s or TRIVIAL.match(s):
            continue
        if re.match(r'^(global )?using [\w.= ]+;$', s) or re.match(r'^namespace [\w.]+;?$', s):
            continue
        if mode == 'B':
            s = re.sub(r'[$@]*"+[^"]*"+', 'S', s)
            s = re.sub(r"'[^']*'", 'C', s)
            s = NUM.sub('N', s)
            s = IDENT.sub(lambda m: m.group(0) if m.group(0) in CS_KEYWORDS else 'I', s)
        out.append((s, k))
    return out


def clones(fms, window, mode, zones, min_distinct):
    seqs = {fm.rel: norm_lines(fm, mode) for fm in fms}
    index = collections.defaultdict(list)
    for rel, seq in seqs.items():
        texts = [s for s, _ in seq]
        for i in range(len(seq) - window + 1):
            w = texts[i:i + window]
            if len(set(w)) < min_distinct:
                continue
            h = hashlib.sha1('\n'.join(w).encode()).hexdigest()
            index[h].append((rel, i))
    diag = collections.defaultdict(set)
    intra = collections.defaultdict(set)
    for h, occ in index.items():
        files = {r for r, _ in occ}
        if len(occ) < 2:
            continue
        for a in range(len(occ)):
            for b in range(a + 1, len(occ)):
                (f, i), (g, j) = occ[a], occ[b]
                if f == g:
                    if abs(i - j) >= window:
                        intra[f].update(range(min(i, j), min(i, j) + window))
                        intra[f].update(range(max(i, j), max(i, j) + window))
                    continue
                if len(files) < 2:
                    continue
                if f > g:
                    f, g, i, j = g, f, j, i
                diag[(f, g)].add((i, j))
    # runs diagonaux -> régions
    regions = []  # (rel, i0, i1) indices normalisés inclusifs
    links = []
    for (f, g), pts in diag.items():
        for (i, j) in sorted(pts):
            if (i - 1, j - 1) in pts:
                continue
            k = 0
            while (i + k + 1, j + k + 1) in pts:
                k += 1
            ra = (f, i, i + k + window - 1)
            rb = (g, j, j + k + window - 1)
            regions.append(ra)
            regions.append(rb)
            links.append((len(regions) - 2, len(regions) - 1))
    # union-find : régions liées + régions chevauchantes dans le même fichier
    parent = list(range(len(regions)))

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    def union(a, b):
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[ra] = rb

    for a, b in links:
        union(a, b)
    by_file = collections.defaultdict(list)
    for idx, (rel, i0, i1) in enumerate(regions):
        by_file[rel].append((i0, i1, idx))
    for rel, lst in by_file.items():
        lst.sort()
        cur_end = -1
        cur_idx = None
        for i0, i1, idx in lst:
            if cur_idx is not None and i0 <= cur_end:
                union(idx, cur_idx)
                cur_end = max(cur_end, i1)
            else:
                cur_end = i1
                cur_idx = idx
    fams = collections.defaultdict(list)
    for idx in range(len(regions)):
        fams[find(idx)].append(regions[idx])
    families = []
    covered = collections.defaultdict(set)
    for members in fams.values():
        per_file = collections.defaultdict(list)
        for rel, i0, i1 in members:
            per_file[rel].append((i0, i1))
        occs = []
        for rel, ivs in per_file.items():
            ivs.sort()
            merged = []
            for a, b in ivs:
                if merged and a <= merged[-1][1] + 1:
                    merged[-1][1] = max(merged[-1][1], b)
                else:
                    merged.append([a, b])
            seq = seqs[rel]
            for a, b in merged:
                covered[rel].update(range(a, b + 1))
                occs.append({'fichier': rel, 'lignes': f"{seq[a][1]}-{seq[b][1]}", 'lignes_norm': b - a + 1,
                             'lignes_brutes': seq[b][1] - seq[a][1] + 1, 'zone': zones[rel]})
        if len({o['fichier'] for o in occs}) < 2:
            continue
        sizes = [o['lignes_norm'] for o in occs]
        biggest = max(occs, key=lambda o: o['lignes_norm'])
        seq = seqs[biggest['fichier']]
        start_line = int(biggest['lignes'].split('-')[0])
        sample_idx = [k for k, (_, ln) in enumerate(seq) if ln >= start_line][:4]
        families.append({
            'occurrences': sorted(occs, key=lambda o: (o['fichier'], int(o['lignes'].split('-')[0]))),
            'nb_occurrences': len(occs),
            'fichiers': len({o['fichier'] for o in occs}),
            'zones': sorted({o['zone'] for o in occs}),
            'lignes_norm_totales': sum(sizes),
            'lignes_norm_en_trop': sum(sizes) - max(sizes),
            'extrait': [seq[k][0][:140] for k in sample_idx],
        })
    families.sort(key=lambda f: (-f['lignes_norm_en_trop'], -f['lignes_norm_totales']))
    total_norm = sum(len(s) for s in seqs.values())
    dup_norm = sum(len(v) for v in covered.values())
    intra_tot = {rel: len(v) for rel, v in intra.items()}
    return {
        'mode': mode,
        'fenetre': window,
        'lignes_normalisees_totales': total_norm,
        'lignes_couvertes_par_un_clone_inter_fichiers': dup_norm,
        'part_couverte': round(dup_norm / total_norm, 4) if total_norm else 0,
        'lignes_en_trop_estimees': sum(f['lignes_norm_en_trop'] for f in families),
        'familles': len(families),
        'par_zone_lignes_couvertes': dict(collections.Counter({z: 0 for z in set(zones.values())}) +
                                          collections.Counter({zones[r]: len(v) for r, v in covered.items()})),
        'top_familles': families[:15],
        'intra_fichier_lignes_norm': dict(sorted(intra_tot.items(), key=lambda kv: -kv[1])[:15]),
    }


# ------------------------------------------------------------------ 3. P/Invoke et constantes Win32

PINV_RE = re.compile(r'\[\s*(?P<attr>DllImport|LibraryImport)\s*\(\s*(?P<dll>"[^"]*"|[\w.]+)(?P<args>[^\]]*)\)\s*\]'
                     r'(?P<after>(?:\s*\[[^\]]*\])*\s*[\w\s<>\[\],.?*]*?\b(?:extern|partial)\b[^;(]*?\b(?P<name>\w+)\s*\()',
                     re.S)
CONST_RE = re.compile(r'\bconst\s+(?P<type>[\w.]+)\s+(?P<name>[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+)\s*=\s*(?P<val>[^;,]+)[;,]')
STRUCT_RE = re.compile(r'\b(?:struct|delegate\s+[\w*]+)\s+(?P<name>[A-Z][A-Za-z0-9_]*)\b')


WIN32_PREFIXES = set('''WM VK WS DT TTM TTF TTS TTN ES EM BM BS BN BST WMSZ TCM TCN TCIF SB SIF SWP MF MB SS CLSCTX RRF GWL
GWLP SW SM HWND MONITOR KEYEVENTF INPUT LLKHF WH HC CS IDI IMAGE LR NIM NIF NIN NIIF NIS TPM CB CBN LB LBN DWMWA PM GA
SPI WDA TME HT COLOR SRCCOPY AC ULW EVENT WINEVENT OBJID CHILDID ERROR MAPVK KLF LOCALE TB TBM UIA UIS UISF DLGC ICC
WA MA MK SC SIZE HKEY KEY REG SPIF CF GMEM GHND NOTIFYICON DPI PROCESS STILL WAIT INFINITE FW DEFAULT ANSI OUT CLIP
PROOF CLEARTYPE FF TRANSPARENT OPAQUE GDIP SMTO MOD QS PBT WTS NOTIFY STGM CO COINIT SEE WPF RDW DCX CW'''.split())


def eval_const(v, known):
    s = v.strip()
    s = re.sub(r'\bunchecked\s*\(', '(', s)
    s = re.sub(r'\((?:int|uint|long|ulong|short|ushort|byte|sbyte|IntPtr|nint|nuint)\)', '', s)
    s = re.sub(r'(?<=[0-9A-Fa-f])(?:[uU][lL]?|[lL][uU]?)\b', '', s)
    s = re.sub(r'\b[A-Z][A-Z0-9_]+\b', lambda m: str(known.get(m.group(0), m.group(0))), s)
    if not re.fullmatch(r'[0-9a-fA-FxX|&<>+\-*() ~_]+', s):
        return None
    try:
        return int(eval(s.replace('_', ''), {'__builtins__': {}}, {})) & 0xFFFFFFFFFFFFFFFF  # noqa: S307
    except Exception:  # noqa: BLE001
        return None


def pinvoke(fms):
    decls = []
    consts = []
    structs = []
    for fm in fms:
        for m in PINV_RE.finditer(fm.nc):
            dll = m.group('dll').strip('"')
            args = m.group('args')
            ep = re.search(r'EntryPoint\s*=\s*"([^"]+)"', args)
            name = m.group('name')
            decls.append({'fichier': fm.rel, 'ligne': fm.line(m.start()), 'attr': m.group('attr'),
                          'dll': dll.lower().replace('.dll', ''), 'nom': name,
                          'point_entree': ep.group(1) if ep else name,
                          'zone': fm.zone})
        for m in CONST_RE.finditer(fm.ns):
            raw_val = fm.nc[m.start('val'):m.end('val')].strip()
            consts.append({'fichier': fm.rel, 'ligne': fm.line(m.start()), 'nom': m.group('name'),
                           'type': m.group('type'), 'valeur': raw_val})
        for m in STRUCT_RE.finditer(fm.ns):
            nm = m.group('name')
            if re.fullmatch(r'[A-Z][A-Z0-9_]+', nm) or nm.endswith('Proc') or nm in ('POINT', 'RECT', 'MSG', 'SIZE'):
                structs.append({'fichier': fm.rel, 'ligne': fm.line(m.start()), 'nom': nm})
    known = {}
    for c in consts:
        v = eval_const(c['valeur'], known)
        c['valeur_num'] = v
        if v is not None:
            known.setdefault(c['nom'], v)
    by_key = collections.defaultdict(list)
    for d in decls:
        ep = d['point_entree']
        base = re.sub(r'(W|A)$', '', ep) if not ep.endswith(('Proc', 'Ex')) else ep
        by_key[(d['dll'], ep)].append(d)
    dup = {f"{k[0]}!{k[1]}": v for k, v in by_key.items() if len(v) > 1}
    by_const = collections.defaultdict(list)
    for c in consts:
        by_const[c['nom']].append(c)
    dup_consts = {}
    for k, v in by_const.items():
        if len(v) > 1:
            vals = {c['valeur_num'] if c['valeur_num'] is not None else c['valeur'] for c in v}
            dup_consts[k] = {'occurrences': [f"{c['fichier']}:{c['ligne']} = {c['valeur']}" for c in v],
                             'valeurs_differentes': len(vals) > 1}
    for k, v in dup_consts.items():
        v['categorie'] = 'win32' if k.split('_')[0] in WIN32_PREFIXES else 'application'
    w32 = {k: v for k, v in dup_consts.items() if v['categorie'] == 'win32'}
    # palette : couleurs CLR_* recopiées (même valeur dans plusieurs fichiers)
    clr_by_val = collections.defaultdict(set)
    clr_decl = 0
    for c in consts:
        if c['nom'].startswith('CLR_'):
            clr_decl += 1
            clr_by_val[c['valeur_num'] if c['valeur_num'] is not None else c['valeur']].add(c['fichier'])
    palette = {'declarations_CLR': clr_decl, 'valeurs_distinctes': len(clr_by_val),
               'valeurs_presentes_dans_3_fichiers_ou_plus': sorted(
                   [(hex(v) if isinstance(v, int) else v, len(fs)) for v, fs in clr_by_val.items() if len(fs) >= 3],
                   key=lambda x: -x[1]),
               'fichiers_declarant_CLR': len({c['fichier'] for c in consts if c['nom'].startswith('CLR_')})}
    by_struct = collections.defaultdict(list)
    for s in structs:
        by_struct[s['nom']].append(f"{s['fichier']}:{s['ligne']}")
    dup_structs = {k: v for k, v in by_struct.items() if len({x.split(':')[0] for x in v}) > 1}
    per_file = collections.Counter(d['fichier'] for d in decls)
    per_attr = collections.Counter(d['attr'] for d in decls)
    const_prefix = collections.Counter(c['nom'].split('_')[0] for c in consts)
    const_files = collections.Counter(c['fichier'] for c in consts)
    return {
        'declarations': len(decls),
        'par_attribut': dict(per_attr),
        'par_fichier': dict(per_file.most_common()),
        'fonctions_distinctes': len(by_key),
        'fonctions_declarees_plusieurs_fois': len(dup),
        'declarations_en_trop': sum(len(v) - 1 for v in dup.values()),
        'doublons': {k: [f"{d['fichier']}:{d['ligne']} ({d['attr']})" for d in v] for k, v in sorted(dup.items())},
        'constantes_style_win32': len(consts),
        'constantes_par_fichier': dict(const_files.most_common()),
        'constantes_par_prefixe': dict(const_prefix.most_common(25)),
        'constantes_nom_declare_plusieurs_fois': len(dup_consts),
        'constantes_declarations_en_trop': sum(len(v['occurrences']) - 1 for v in dup_consts.values()),
        'constantes_valeurs_differentes': [k for k, v in dup_consts.items() if v['valeurs_differentes']],
        'constantes_win32_redeclarees': len(w32),
        'constantes_win32_declarations_en_trop': sum(len(v['occurrences']) - 1 for v in w32.values()),
        'constantes_win32_valeurs_differentes': [k for k, v in w32.items() if v['valeurs_differentes']],
        'constantes_win32_doublons': dict(sorted(w32.items())),
        'palette_CLR': palette,
        'constantes_doublons': dict(sorted(dup_consts.items())),
        'structs_delegues_win32_dupliques': dup_structs,
    }


# ------------------------------------------------------------------ 4. code probablement mort

def dead_code(prod, tests, others_text):
    cnt_prod = collections.Counter()
    cnt_test = collections.Counter()
    cnt_other = collections.Counter()
    for fm in prod:
        cnt_prod.update(IDENT.findall(fm.nc))
    for fm in tests:
        cnt_test.update(IDENT.findall(fm.nc))
    for t in others_text:
        cnt_other.update(IDENT.findall(t))
    decl_prod = collections.Counter()
    decl_test = collections.Counter()
    for fms, dc in ((prod, decl_prod), (tests, decl_test)):
        for fm in fms:
            for m in fm.members:
                if m['name'] != 'this[]':
                    dc[m['name']] += 1
            for t in fm.types:
                dc[t['name']] += 1
                if t.get('enum_members'):
                    for em in t['enum_members']:
                        dc[em] += 1
    com_types = set()
    interop_types = set()
    for fm in prod:
        for t in fm.types:
            if 'GeneratedCom' in t.get('attrs', '') or 'ComVisible' in t.get('attrs', ''):
                com_types.add(t['name'])
            if 'StructLayout' in t.get('attrs', '') or (t['kind'] == 'struct' and re.fullmatch(r'[A-Z0-9_]+', t['name'])):
                interop_types.add(t['name'])
    EXCL = {'Main', 'Dispose', 'ToString', 'Equals', 'GetHashCode', 'Finalize', 'this[]'}
    cands = []
    test_only = []
    for fm in prod:
        items = [(m['kind'], m) for m in fm.members] + [('type', t) for t in fm.types]
        for kind, m in items:
            name = m['name']
            if name in EXCL or name.startswith('operator'):
                continue
            mods = m.get('mods', set())
            if 'override' in mods or '.' in name:
                continue
            if kind == 'methode' and name == m.get('owner'):
                continue  # constructeur
            if kind == 'type' and m['kind'] == 'delegate':
                pass
            rp = cnt_prod[name] - decl_prod[name]
            rt = cnt_test[name] - decl_test[name]
            ro = cnt_other[name]
            size = (m['end'] - m['start'] + 1) if 'end' in m else 1
            cat = kind
            if fm.rel.startswith('src/Localization/') and m.get('owner') == 'L' and kind in ('propriete', 'methode'):
                cat = 'cle_L'
            if kind == 'champ' and m.get('owner') in interop_types:
                continue  # champ de struct d'interop : exigé par la disposition mémoire, pas « mort »
            if m.get('owner') in com_types:
                continue  # appelé par COM (vtable), invisible textuellement
            rec = {'nom': name, 'categorie': cat, 'fichier': fm.rel, 'ligne': m['start'], 'lignes': size,
                   'owner': m.get('owner'), 'refs_prod': rp, 'refs_tests': rt, 'refs_autres': ro, 'zone': fm.zone}
            if rp <= 0 and rt <= 0 and ro <= 0:
                cands.append(rec)
            elif rp <= 0 and rt > 0 and ro <= 0:
                test_only.append(rec)
    cands.sort(key=lambda r: -r['lignes'])
    test_only.sort(key=lambda r: -r['lignes'])
    by_cat = collections.Counter(r['categorie'] for r in cands)
    return {
        'candidats_morts': len(cands),
        'candidats_par_categorie': dict(by_cat),
        'lignes_candidats': sum(r['lignes'] for r in cands),
        'candidats': cands,
        'utilises_seulement_par_tests': len(test_only),
        'utilises_seulement_par_tests_lignes': sum(r['lignes'] for r in test_only),
        'liste_tests_seulement': test_only,
    }


# ------------------------------------------------------------------ 5. marqueurs

TODO_RE = re.compile(r'\b(TODO|FIXME|HACK|XXX)\b')
LOG_RE = re.compile(r'\b(Log\w*|Debug\.|Trace\.|Console\.|Diag\w*|Telemetry|Report\w*)\b')


def enclosing_method(fm, pos):
    best = None
    for m in fm.members:
        if m['kind'] == 'methode' and m['body'][0] <= pos <= m['body'][1]:
            if best is None or m['body'][0] > best['body'][0]:
                best = m
    return best


def markers(fms):
    todos = []
    prepro = collections.defaultdict(list)
    catches = []
    suppress = []
    for fm in fms:
        for a, b, txt in fm.comments:
            for m in TODO_RE.finditer(txt):
                todos.append(f"{fm.rel}:{fm.line(a)} {txt.strip()[:120]}")
        for k, ln in enumerate(fm.text.split('\n'), start=1):
            s = ln.strip()
            m = re.match(r'#\s*(if|elif|pragma|nullable|region|define|undef|error|warning)\b(.*)', s)
            if m:
                prepro[m.group(1)].append(f"{fm.rel}:{k} {s[:100]}")
        for m in re.finditer(r'\[\s*(?:System\.Diagnostics\.CodeAnalysis\.)?(?:SuppressMessage|UnconditionalSuppressMessage)\b', fm.nc):
            suppress.append(f"{fm.rel}:{fm.line(m.start())}")
        for m in re.finditer(r'\bcatch\b\s*(?:\((?P<decl>[^)]*)\))?\s*(?:when\s*\((?P<when>.*?)\)\s*)?\{', fm.ns):
            open_ = m.end() - 1
            close = fm.match.get(open_)
            if close is None:
                continue
            body_ns = fm.ns[open_ + 1:close]
            body_code = re.sub(r'\s+', ' ', body_ns).strip()
            raw_body = fm.text[open_ + 1:close]
            decl = (m.group('decl') or '').strip()
            dtype = decl.split()[0] if decl else ''
            catch_all = dtype in ('', 'Exception', 'System.Exception') and not m.group('when')
            has_comment = '//' in raw_body or '/*' in raw_body
            # commentaire juste avant le catch (ligne précédente) ou sur la ligne du catch
            line_no = fm.line(m.start())
            ctx_lines = fm.text.split('\n')[max(0, line_no - 3):line_no]
            comment_near = any('//' in x for x in ctx_lines)
            rethrow = bool(re.search(r'\bthrow\b', body_code))
            logs = bool(LOG_RE.search(fm.nc[open_ + 1:close]))
            if not body_code:
                nature = 'vide'
            elif re.fullmatch(r'(return( (false|null|default|0|-1|true|""|string\.Empty|IntPtr\.Zero|[\w.]+))?;|continue;|break;)', body_code.replace('""', 'S')) or re.fullmatch(r'return [\w.]+;', body_code):
                nature = 'retour_par_defaut'
            else:
                nature = 'traitement'
            meth = enclosing_method(fm, m.start())
            catches.append({'emplacement': f"{fm.rel}:{line_no}", 'zone': fm.zone,
                            'type': dtype or '(tout)', 'filtre_when': bool(m.group('when')),
                            'attrape_tout': catch_all, 'nature': nature, 'relance': rethrow, 'journalise': logs,
                            'commentaire_dans_bloc': has_comment, 'commentaire_proche': comment_near,
                            'methode': f"{meth['owner']}.{meth['name']}" if meth else None,
                            'corps': body_code[:80]})
    muets = [c for c in catches if c['attrape_tout'] and not c['relance'] and not c['journalise']
             and c['nature'] in ('vide', 'retour_par_defaut')]
    return {
        'todo_fixme_hack': todos,
        'preprocesseur': {k: v for k, v in prepro.items()},
        'preprocesseur_compte': {k: len(v) for k, v in prepro.items()},
        'suppressmessage': suppress,
        'catch_total': len(catches),
        'catch_attrape_tout': sum(1 for c in catches if c['attrape_tout']),
        'catch_par_type': dict(collections.Counter(c['type'] for c in catches).most_common()),
        'catch_par_nature': dict(collections.Counter(c['nature'] for c in catches)),
        'catch_muets_attrape_tout': len(muets),
        'catch_muets_sans_commentaire': sum(1 for c in muets if not c['commentaire_dans_bloc'] and not c['commentaire_proche']),
        'catch_muets_par_zone': dict(collections.Counter(c['zone'] for c in muets)),
        'catch_muets_par_fichier': dict(collections.Counter(c['emplacement'].split(':')[0] for c in muets).most_common()),
        'catch_liste': catches,
    }


# ------------------------------------------------------------------ 6. nommage FR/EN, commentaires, renvois d'audit

FR_WORDS = set()  # rempli depuis lexique_fr.txt


def load_lexicon(path):
    words = set()
    if os.path.exists(path):
        for ln in read(path).splitlines():
            ln = ln.split('#')[0].strip().lower()
            if ln:
                words.update(ln.split())
    return words


def split_words(name):
    out = []
    for part in re.split(r'_+', name):
        out += re.findall(r'[A-Z]+(?=[A-Z][a-z])|[A-Z]?[a-zà-ÿ]+|[A-Z]+|\d+', part)
    return [w.lower() for w in out if not w.isdigit() and len(w) > 1]


def naming(prod, tests, fr_words, ambiguous):
    def classify(names):
        res = collections.Counter()
        ex = collections.defaultdict(list)
        vocab = collections.Counter()
        for n in names:
            ws = split_words(n)
            vocab.update(ws)
            fr = [w for w in ws if w in fr_words or re.search(r'[à-ÿœ]', w)]
            en = [w for w in ws if w not in fr_words and w not in ambiguous]
            if fr and en:
                c = 'mixte'
            elif fr:
                c = 'fr'
            elif en:
                c = 'en'
            else:
                c = 'indetermine'
            res[c] += 1
            if len(ex[c]) < 25:
                ex[c].append(n)
        total = sum(res.values())
        return {'total': total, 'repartition': dict(res),
                'part_avec_francais': round((res['fr'] + res['mixte']) / total, 3) if total else 0,
                'exemples': {k: v for k, v in ex.items() if k in ('fr', 'mixte')}}, vocab

    prod_names = set()
    for fm in prod:
        for m in fm.members:
            if m['name'] != 'this[]' and not (fm.rel.startswith('src/Localization/')):
                prod_names.add(m['name'])
        for t in fm.types:
            prod_names.add(t['name'])
    test_names = set()
    test_methods = set()
    for fm in tests:
        for m in fm.members:
            if m['kind'] == 'methode':
                test_names.add(m['name'])
                if re.search(r'\[\s*(Fact|Theory)', m.get('attrs', '')):
                    test_methods.add(m['name'])
        for t in fm.types:
            test_names.add(t['name'])
    p, vocab_p = classify(sorted(prod_names))
    t, vocab_t = classify(sorted(test_methods))
    # styles de noms de test
    styles = collections.Counter()
    style_ex = collections.defaultdict(list)
    for n in sorted(test_methods):
        if re.match(r'^[A-Z]{1,3}\d{1,3}_', n):
            s = 'prefixe_identifiant (K3_, D1_, N10_...)'
        elif re.search(r'_[a-zà-ÿ]', n) and n.count('_') >= 3:
            s = 'phrase_en_snake_case'
        elif '_' in n:
            s = 'Segments_PascalCase'
        else:
            s = 'PascalCase_sans_separateur'
        styles[s] += 1
        if len(style_ex[s]) < 4:
            style_ex[s].append(n)
    t['styles'] = dict(styles)
    t['styles_exemples'] = dict(style_ex)
    t['noms_accentues'] = sum(1 for n in test_methods if re.search(r'[à-ÿÀ-ß]', n))
    # identifiants à accents
    accents = sorted({n for n in prod_names | test_names if re.search(r'[à-ÿÀ-ß]', n)})
    return {'production_identifiants_declares': p, 'tests_noms_de_methodes_de_test': t,
            'identifiants_accentues': accents[:40],
            'vocabulaire_prod_top': vocab_p.most_common(),
            'vocabulaire_tests_top': vocab_t.most_common()}


REF_PATTERNS = {
    'AG130-xx (audit 2026-09-20/22)': re.compile(r'\bAG\d{3}-\d{2}\b'),
    'SEV-Ax-yy (audit sécu 2026-05)': re.compile(r'\bSEV-A\d-\d{2}\b'),
    'audit daté': re.compile(r'\b[Aa]udit(?:\s+(?:sécu|secu|Store|du))?\s+(?:du\s+)?\d{4}-\d{2}(?:-\d{2})?'),
    'audit jj/mm': re.compile(r'\b[Aa]udit(?:\s+du)?\s+\d{1,2}/\d{2}\b'),
    'Audit120*/nom de test': re.compile(r'\bAudit1[23]0\w*'),
    'lot court (R/B/K + n°)': re.compile(r'(?<![\w-])[RBK]\d{1,2}(?![\w-])'),
    'version (vX.Y.Z)': re.compile(r'\bv\d\.\d\.\d\b'),
}
ID_ONLY = re.compile(r'\b(?:AG\d{3}-\d{2}|SEV-A\d-\d{2})\b')


def comment_blocks(fm):
    """Regroupe les commentaires // consécutifs en blocs ; renvoie [(ligne, texte)]."""
    blocks = []
    prev_line = None
    for a, b, txt in fm.comments:
        ln = fm.line(a)
        t = re.sub(r'^\s*(///?|/\*+|\*)\s?', '', txt).replace('*/', '')
        t = re.sub(r'</?\w+[^>]*>', ' ', t)
        if blocks and prev_line is not None and ln == prev_line + 1 and txt.startswith('//'):
            blocks[-1][1] += ' ' + t.strip()
        else:
            blocks.append([ln, t.strip()])
        prev_line = fm.line(b)
    return blocks


def audit_refs(fms, docs_text):
    per_cat = collections.Counter()
    per_file = collections.Counter()
    per_zone = collections.Counter()
    ids = collections.Counter()
    bare = []
    explained = 0
    total_blocks_with_ref = 0
    examples = []
    for fm in fms:
        for ln, txt in comment_blocks(fm):
            found = []
            for cat, rx in REF_PATTERNS.items():
                if cat.startswith('lot court') and 'audit' not in txt.lower():
                    continue
                hits = rx.findall(txt)
                if hits:
                    per_cat[cat] += len(hits)
                    found += hits
            if not found:
                continue
            refs_only = [h for h in found if not h.startswith('v')]
            if not refs_only:
                continue
            total_blocks_with_ref += 1
            per_file[fm.rel] += 1
            per_zone[fm.zone] += 1
            for h in ID_ONLY.findall(txt):
                ids[h] += 1
            rest = txt
            for rx in REF_PATTERNS.values():
                rest = rx.sub('', rest)
            words = re.findall(r'[A-Za-zÀ-ÿ]{3,}', rest)
            if len(words) >= 8:
                explained += 1
            else:
                bare.append(f"{fm.rel}:{ln} {txt[:110]}")
            if len(examples) < 12 and len(words) >= 8:
                examples.append(f"{fm.rel}:{ln} {txt[:160]}")
    resolved = {i: (i in docs_text) for i in ids}
    return {
        'blocs_de_commentaire_avec_renvoi': total_blocks_with_ref,
        'renvois_par_categorie': dict(per_cat),
        'blocs_avec_explication_8_mots_plus': explained,
        'blocs_renvoi_nu': len(bare),
        'liste_renvois_nus': bare,
        'par_fichier': dict(per_file.most_common(20)),
        'par_zone': dict(per_zone),
        'identifiants_distincts': len(ids),
        'identifiants_trouves_dans_docs_ou_changelog': sum(1 for v in resolved.values() if v),
        'identifiants_introuvables': sorted(i for i, v in resolved.items() if not v),
        'exemples_expliques': examples,
    }


# ------------------------------------------------------------------ 7. tests

def tests_metrics(prod, tests, window):
    type_zone = {}
    for fm in prod:
        for t in fm.types:
            if t['name'] not in ('L', 'Win32', 'Program', 'ProductIdentity', 'NativeMethods'):
                type_zone.setdefault(t['name'], fm.zone)
    zone_prod_lines = collections.Counter()
    zone_prod_code = collections.Counter()
    for fm in prod:
        ls = fm.line_stats()
        zone_prod_lines[fm.zone] += ls['total']
        zone_prod_code[fm.zone] += ls['code']
    files = []
    zone_test_lines = collections.Counter()
    zone_test_code = collections.Counter()
    zone_test_count = collections.Counter()
    audit_named_files = []
    id_named_tests = []
    all_tests = 0
    bodies_a = collections.defaultdict(list)
    bodies_b = collections.defaultdict(list)
    helpers_a = collections.defaultdict(list)
    for fm in tests:
        toks = collections.Counter(IDENT.findall(fm.nc))
        score = collections.Counter()
        for tn, z in type_zone.items():
            if toks[tn]:
                score[z] += toks[tn]
        zone = score.most_common(1)[0][0] if score else 'support'
        ls = fm.line_stats()
        tms = [m for m in fm.members if m['kind'] == 'methode' and re.search(r'\[\s*(Fact|Theory)', m.get('attrs', ''))]
        inline = len(re.findall(r'\[\s*InlineData\b', fm.nc))
        all_tests += len(tms)
        base = os.path.basename(fm.rel)
        by_audit = bool(re.search(r'Audit\d+|\d{3}Tests|QuickWins|Regression', base))
        if by_audit:
            audit_named_files.append(base)
        for m in tms:
            if re.match(r'^(AG\d+|[RBK]\d{1,2}_|Audit|SEV|Bug\d)', m['name']) or re.search(r'_(AG\d{3}|[RB]\d{1,2})(_|$)', m['name']):
                id_named_tests.append(f"{base}:{m['name']}")
        zone_test_lines[zone] += ls['total']
        zone_test_code[zone] += ls['code']
        zone_test_count[zone] += len(tms)
        files.append({'fichier': fm.rel, 'zone_estimee': zone, 'lignes': ls['total'], 'tests': len(tms),
                      'inline_data': inline, 'nomme_par_audit_ou_lot': by_audit,
                      'score_zones': dict(score.most_common(3))})
        # corps normalisés
        for m in fm.members:
            if m['kind'] != 'methode' or m['expr']:
                continue
            o, c = m['body']
            a_lines = [re.sub(r'\s+', ' ', x).strip() for x in fm.nc[o + 1:c].split('\n')]
            a_lines = [x for x in a_lines if x and not TRIVIAL.match(x)]
            if len(a_lines) < 4:
                continue
            b_lines = [IDENT.sub(lambda mm: mm.group(0) if mm.group(0) in CS_KEYWORDS else 'I',
                                 NUM.sub('N', re.sub(r'[$@]*"+[^"]*"+', 'S', re.sub(r'\s+', ' ', x).strip())))
                       for x in fm.ns[o + 1:c].split('\n')]
            b_lines = [x for x in b_lines if x and not TRIVIAL.match(x)]
            ha = hashlib.sha1('\n'.join(a_lines).encode()).hexdigest()
            hb = hashlib.sha1('\n'.join(b_lines).encode()).hexdigest()
            loc = f"{fm.rel}:{m['start']} {m['owner']}.{m['name']} ({len(a_lines)} l.)"
            if m in tms:
                bodies_a[ha].append(loc)
                bodies_b[hb].append(loc)
            else:
                helpers_a[(m['name'], ha)].append(loc)
    dup_a = [v for v in bodies_a.values() if len(v) > 1]
    dup_b = [v for v in bodies_b.values() if len(v) > 1]
    helper_dups = [v for v in helpers_a.values() if len(v) > 1]
    # helpers de même nom dans plusieurs fichiers (corps différents ou non)
    helper_names = collections.defaultdict(set)
    for fm in tests:
        for m in fm.members:
            if m['kind'] == 'methode' and not re.search(r'\[\s*(Fact|Theory)', m.get('attrs', '')) and m['name'] != m['owner']:
                helper_names[m['name']].add(os.path.basename(fm.rel))
    same_name_helpers = {k: sorted(v) for k, v in helper_names.items() if len(v) >= 3}
    zones = sorted(set(zone_prod_lines) | set(zone_test_lines))
    ratio = {z: {'prod_lignes': zone_prod_lines[z], 'tests_lignes': zone_test_lines[z],
                 'ratio_lignes': round(zone_test_lines[z] / zone_prod_lines[z], 2) if zone_prod_lines[z] else None,
                 'prod_code': zone_prod_code[z], 'tests_code': zone_test_code[z],
                 'tests_nb': zone_test_count[z]} for z in zones}
    clone_tests = clones(tests, window, 'A', {fm.rel: 'tests' for fm in tests}, 3)
    return {
        'nb_tests_fact_theory': all_tests,
        'ratio_par_zone': ratio,
        'fichiers_nommes_par_audit_ou_lot': audit_named_files,
        'tests_nommes_par_identifiant': id_named_tests,
        'corps_de_test_identiques_texte': dup_a,
        'corps_de_test_identiques_structure': [v for v in dup_b if len(v) > 1][:30],
        'nb_groupes_corps_identiques_structure': len(dup_b),
        'helpers_identiques_entre_fichiers': helper_dups,
        'helpers_meme_nom_3_fichiers_ou_plus': same_name_helpers,
        'clones_inter_fichiers_tests': {k: clone_tests[k] for k in ('lignes_normalisees_totales',
                                                                   'lignes_couvertes_par_un_clone_inter_fichiers',
                                                                   'part_couverte', 'lignes_en_trop_estimees',
                                                                   'familles')},
        'clones_tests_top': clone_tests['top_familles'][:8],
        'fichiers': files,
    }


# ------------------------------------------------------------------ 8. csproj

PROPS = ['OutputType', 'TargetFramework', 'TargetFrameworks', 'Nullable', 'ImplicitUsings', 'LangVersion',
         'TreatWarningsAsErrors', 'WarningsAsErrors', 'NoWarn', 'WarningLevel', 'PublishAot', 'PublishTrimmed',
         'IsAotCompatible', 'IsTrimmable', 'PublishReadyToRun', 'PublishSingleFile', 'InvariantGlobalization',
         'EnableNETAnalyzers', 'AnalysisLevel', 'AnalysisMode', 'AllowUnsafeBlocks', 'OptimizationPreference',
         'StackTraceSupport', 'UseSystemResourceKeys', 'ControlFlowGuard', 'Deterministic', 'GenerateAssemblyInfo',
         'RuntimeIdentifiers', 'EnforceCodeStyleInBuild', 'GenerateDocumentationFile']


def csproj(root, files):
    out = {}
    for rel in files:
        if not rel.endswith('.csproj'):
            continue
        tree = ET.parse(os.path.join(root, rel))
        props = {}
        for p in PROPS:
            el = tree.find('.//' + p)
            if el is not None:
                props[p] = (el.text or '').strip()
        pk = [{'nom': e.get('Include'), 'version': e.get('Version')} for e in tree.iter('PackageReference')]
        pr = [e.get('Include') for e in tree.iter('ProjectReference')]
        cr = [e.get('Remove') for e in tree.iter('Compile') if e.get('Remove')]
        ci = [e.get('Include') for e in tree.iter('Compile') if e.get('Include')]
        er = [e.get('Include') for e in tree.iter('EmbeddedResource')]
        out[rel] = {'proprietes': props, 'nuget': pk, 'references_projet': pr, 'compile_remove': cr,
                    'compile_include': ci, 'ressources_embarquees': er}
    return out


# ------------------------------------------------------------------ main

def main():
    root = sys.argv[1]
    out_path = sys.argv[2]
    window = 8
    if '--fenetre' in sys.argv:
        window = int(sys.argv[sys.argv.index('--fenetre') + 1])
    here = os.path.dirname(os.path.abspath(__file__))
    fr_words = load_lexicon(os.path.join(here, 'lexique_fr.txt'))
    ambiguous = load_lexicon(os.path.join(here, 'lexique_ambigu.txt'))
    files = git_ls(root)
    cs = [f for f in files if f.endswith('.cs')]
    prod, tests = [], []
    for rel in cs:
        fm = FileModel(rel, read(os.path.join(root, rel)))
        fm.rel = rel
        fm.zone = 'tests' if is_test(rel) else zone_of(rel)
        fm.project = project_of(rel)
        (tests if is_test(rel) else prod).append(fm)
    others_text = [read(os.path.join(root, f)) for f in files
                   if f.endswith(('.json', '.csproj', '.manifest', '.xml', '.xaml', '.props'))]
    zones = {fm.rel: fm.zone for fm in prod}
    per_file = {fm.rel: dict(file_metrics(fm), zone=fm.zone, projet=fm.project) for fm in prod}
    all_meths = [dict(m, fichier=fm.rel, zone=fm.zone) for fm in prod for m in fm.members if m['kind'] == 'methode']
    longest = sorted(all_meths, key=lambda m: -m['lignes'])
    top_cc = sorted(all_meths, key=lambda m: -m['cc'])
    deep = sorted(all_meths, key=lambda m: -m['profondeur'])

    def mref(m):
        return {'methode': f"{m['owner']}.{m['name']}", 'emplacement': f"{m['fichier']}:{m['start']}-{m['end']}",
                'lignes': m['lignes'], 'cc': m['cc'], 'profondeur': m['profondeur'], 'zone': m['zone']}

    # types (god class) : lignes par type de niveau 1 (partial fusionnés)
    type_lines = collections.Counter()
    type_members = collections.Counter()
    type_files = collections.defaultdict(set)
    for fm in prod:
        for t in fm.types:
            if t['kind'] in ('class', 'struct', 'record') and t['owner'] is None:
                type_lines[t['name']] += t['end'] - t['start'] + 1
                type_files[t['name']].add(fm.rel)
        for m in fm.members:
            type_members[m['owner']] += 1
    by_zone = collections.defaultdict(list)
    for fm in prod:
        by_zone[fm.zone].append(fm)
    by_project = collections.defaultdict(list)
    for fm in prod + tests:
        by_project[fm.project].append(fm)
    docs_text = ''
    for dp, dn, fn in os.walk(os.path.join(root, 'docs')):
        if 'ci-checkout' in dp or '__pycache__' in dp:
            continue
        for f in fn:
            if f.endswith(('.md', '.txt', '.json', '.py', '.csv')):
                try:
                    docs_text += read(os.path.join(dp, f))
                except OSError:
                    pass
    for f in ('Changelog.md', 'README.md', 'Cahier des charges.md'):
        p = os.path.join(root, f)
        if os.path.exists(p):
            docs_text += read(p)
    result = {
        'meta': {
            'depot': root,
            'commit': subprocess.run(['git', '-C', root, 'rev-parse', '--short', 'HEAD'], capture_output=True,
                                     text=True).stdout.strip(),
            'fichiers_cs_suivis': len(cs),
            'fichiers_production': len(prod),
            'fichiers_tests': len(tests),
            'methode': ('Analyse textuelle maison (outils/cslex.py) : lexer des commentaires/chaînes, découpage '
                        'des membres par accolades. Complexité cyclomatique APPROCHÉE = 1 + if + case + for + '
                        'foreach + while + catch + when + && + || + ?: + ?? + and/or de motif + bras de switch '
                        'expression. Profondeur = blocs de contrôle/lambdas avec accolades imbriqués dans la '
                        'méthode (if sans accolades non compté). Longueur de méthode = de la signature à '
                        "l'accolade fermante, attributs exclus. Zones : table ZONE_RULES/ZONE_BY_NAME du script."),
        },
        'agregat_production': aggregate(prod),
        'agregat_tests': aggregate(tests),
        'agregat_par_zone': {z: aggregate(v) for z, v in sorted(by_zone.items())},
        'agregat_par_projet': {p: aggregate(v) for p, v in sorted(by_project.items())},
        'par_fichier_production': dict(sorted(per_file.items(), key=lambda kv: -kv[1]['lignes']['total'])),
        'methodes_plus_150_lignes': [mref(m) for m in longest if m['lignes'] > 150],
        'methodes_top20_longueur': [mref(m) for m in longest[:20]],
        'methodes_top20_cc': [mref(m) for m in top_cc[:20]],
        'methodes_top10_profondeur': [mref(m) for m in deep[:10]],
        'types_top15_lignes': [{'type': k, 'lignes': v, 'membres': type_members[k], 'fichiers': sorted(type_files[k])}
                               for k, v in type_lines.most_common(15)],
        'procedures_de_fenetre': wndproc.mesurer(root),
        'squelette_fenetres_win32': squelette_fenetres.mesurer(root),
        'clones_mode_A_litteraux_conserves': clones(prod, window, 'A', zones, 3),
        # mode B : Localization/ exclu (déclarations de chaînes structurellement identiques par nature)
        'clones_mode_B_identifiants_abstraits': clones([fm for fm in prod if not fm.rel.startswith('src/Localization/')],
                                                       window, 'B', zones, 4),
        'pinvoke': pinvoke(prod),
        'code_mort': dead_code(prod, tests, others_text),
        'marqueurs': markers(prod),
        'marqueurs_tests': {k: v for k, v in markers(tests).items() if k in ('todo_fixme_hack', 'preprocesseur_compte',
                                                                           'catch_total', 'catch_muets_attrape_tout')},
        'nommage': naming(prod, tests, fr_words, ambiguous),
        'renvois_audit_commentaires_production': audit_refs(prod, docs_text),
        'renvois_audit_commentaires_tests': {k: v for k, v in audit_refs(tests, docs_text).items()
                                             if k != 'liste_renvois_nus'},
        'densite_commentaires_par_fichier': {fm.rel: per_file[fm.rel]['densite_commentaires'] for fm in
                                             sorted(prod, key=lambda f: -per_file[f.rel]['densite_commentaires'])},
        'tests': tests_metrics(prod, tests, window),
        'csproj': csproj(root, files),
    }
    with open(out_path, 'w', encoding='utf-8') as f:
        json.dump(result, f, ensure_ascii=False, indent=1, default=lambda o: sorted(o) if isinstance(o, set) else str(o))
    a = result['agregat_production']
    print('prod', a['fichiers'], a['lignes'], 'methodes', a['methodes'])
    print('tests', result['agregat_tests']['fichiers'], result['agregat_tests']['lignes'])
    for k in ('clones_mode_A_litteraux_conserves', 'clones_mode_B_identifiants_abstraits'):
        c = result[k]
        print(k, c['lignes_couvertes_par_un_clone_inter_fichiers'], '/', c['lignes_normalisees_totales'],
              'familles', c['familles'], 'en trop', c['lignes_en_trop_estimees'])
    print('pinvoke', result['pinvoke']['declarations'], 'doublons', result['pinvoke']['fonctions_declarees_plusieurs_fois'])
    print('mort', result['code_mort']['candidats_morts'], result['code_mort']['candidats_par_categorie'])
    print('catch', result['marqueurs']['catch_total'], 'muets', result['marqueurs']['catch_muets_attrape_tout'])


if __name__ == '__main__':
    main()
