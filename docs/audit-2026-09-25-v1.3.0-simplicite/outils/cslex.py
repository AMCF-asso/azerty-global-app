"""Mini-analyseur C# (stdlib seule) pour l'audit transversal AZERTY Global.

Ce n'est PAS un compilateur : lexer maison (commentaires, chaînes normales,
verbatim, interpolées, brutes, caractères), puis découpage des membres par
accolades. Suffisant pour compter, pas pour prouver. Toutes les métriques qui en
sortent sont des approximations documentées.
"""
import re

# ---------------------------------------------------------------- lexer

_STR_START = re.compile(r'(\$+@?|@\$+|@)?("{3,}|")')


def _skip_char_literal(t, i):
    """t[i] == "'" ; renvoie l'index après le littéral."""
    n = len(t)
    j = i + 1
    if j < n and t[j] == '\\':
        j += 2
        while j < n and t[j] != "'" and t[j] != '\n':
            j += 1
    else:
        j += 1
    if j < n and t[j] == "'":
        return j + 1
    return i + 1  # pas un littéral, on avance d'un caractère


def _skip_string(t, i):
    """t[i] début de chaîne (préfixe éventuel). Renvoie (fin, ouverture_contenu, fermeture_contenu)."""
    n = len(t)
    m = _STR_START.match(t, i)
    prefix = m.group(1) or ''
    quotes = m.group(2)
    interp = '$' in prefix
    verbatim = '@' in prefix
    j = m.end()
    cstart = j
    if len(quotes) >= 3:  # chaîne brute C# 11
        k = t.find(quotes, j)
        if k < 0:
            return n, cstart, n
        # une chaîne brute peut finir par plus de guillemets que l'ouverture
        e = k + len(quotes)
        while e < n and t[e] == '"':
            e += 1
        return e, cstart, e - len(quotes)
    while j < n:
        c = t[j]
        if interp and c == '{':
            if j + 1 < n and t[j + 1] == '{':
                j += 2
                continue
            depth = 1
            j += 1
            while j < n and depth > 0:
                c2 = t[j]
                if c2 in '$@"':
                    mm = _STR_START.match(t, j)
                    if mm and mm.group(2):
                        j = _skip_string(t, j)[0]
                        continue
                if c2 == "'":
                    j = _skip_char_literal(t, j)
                    continue
                if c2 == '{':
                    depth += 1
                elif c2 == '}':
                    depth -= 1
                j += 1
            continue
        if not verbatim and c == '\\':
            j += 2
            continue
        if c == '"':
            if verbatim and j + 1 < n and t[j + 1] == '"':
                j += 2
                continue
            return j + 1, cstart, j
        if c == '\n' and not verbatim:
            return j, cstart, j  # récupération d'erreur
        j += 1
    return n, cstart, n


def lex(text):
    """Renvoie dict(nc=texte sans commentaires, ns=sans commentaires ni contenu
    de chaînes/caractères, comments=[(debut, fin, texte)], strings=[(debut, fin)]).
    Les longueurs et les sauts de ligne sont conservés."""
    n = len(text)
    nc = list(text)
    ns = list(text)
    comments = []
    strings = []

    def blank(arr, a, b):
        for k in range(a, b):
            if arr[k] != '\n':
                arr[k] = ' '

    i = 0
    line_start = True
    while i < n:
        c = text[i]
        if c == '\n':
            line_start = True
            i += 1
            continue
        if line_start and c == '#':
            # directive de préprocesseur : on l'efface de nc/ns (sans accolades utiles)
            j = text.find('\n', i)
            j = n if j < 0 else j
            blank(nc, i, j)
            blank(ns, i, j)
            i = j
            continue
        if not c.isspace():
            line_start = False
        if c == '/' and i + 1 < n and text[i + 1] == '/':
            j = text.find('\n', i)
            j = n if j < 0 else j
            comments.append((i, j, text[i:j]))
            blank(nc, i, j)
            blank(ns, i, j)
            i = j
            continue
        if c == '/' and i + 1 < n and text[i + 1] == '*':
            j = text.find('*/', i + 2)
            j = n if j < 0 else j + 2
            comments.append((i, j, text[i:j]))
            blank(nc, i, j)
            blank(ns, i, j)
            i = j
            continue
        if c == "'":
            j = _skip_char_literal(text, i)
            if j > i + 1:
                blank(ns, i + 1, j - 1)
            i = j
            continue
        if c in '$@"':
            m = _STR_START.match(text, i)
            if m and m.group(2):
                end, cs, ce = _skip_string(text, i)
                strings.append((i, end))
                blank(ns, cs, ce)
                i = end
                continue
        i += 1
    return {'nc': ''.join(nc), 'ns': ''.join(ns), 'comments': comments, 'strings': strings}


def line_index(text):
    """Table des débuts de ligne pour convertir position -> numéro (1-based)."""
    starts = [0]
    for m in re.finditer('\n', text):
        starts.append(m.end())
    return starts


def pos2line(starts, pos):
    lo, hi = 0, len(starts) - 1
    while lo < hi:
        mid = (lo + hi + 1) // 2
        if starts[mid] <= pos:
            lo = mid
        else:
            hi = mid - 1
    return lo + 1


def brace_map(ns):
    stack = []
    match = {}
    for i, c in enumerate(ns):
        if c == '{':
            stack.append(i)
        elif c == '}':
            if stack:
                match[stack.pop()] = i
    return match


# ---------------------------------------------------------------- classification

CTRL_RE = re.compile(r'^(?:else\s+if|if|else|for|foreach|while|do|switch|try|catch|finally|lock|using|fixed|unsafe|checked|unchecked|case\b.*:|default\s*:)\b', re.S)
TYPE_RE = re.compile(r'(?:^|\s)(class|struct|interface|enum|record(?:\s+struct|\s+class)?)\s+(?P<name>[A-Za-z_]\w*)')
ACCESSOR_RE = re.compile(r'^(?:(?:private|protected|internal|public|readonly)\s+)*(get|set|init|add|remove)$')
METHOD_RE = re.compile(r'''^(?P<pre>[^=;{}]*?\s)??
    (?P<name>~?[A-Za-z_]\w*|operator\s*[^\s(]+|this)\s*(?:<[^()]*>)?\s*
    \((?P<params>.*)\)\s*
    (?::\s*(?:base|this)\s*\(.*\))?\s*
    (?:where\s.+)?$''', re.S | re.X)
class _Sig:
    def __init__(self, name, pre):
        self.name, self.pre = name, pre

    def group(self, k):
        return self.name if k == 'name' else self.pre


def match_sig(h):
    """Signature de méthode : nom = identifiant juste avant le DERNIER groupe (...) de
    l'en-tête (après retrait de `where ...` et de `: base(...)`). Gère les types de retour
    tuple et génériques imbriqués, que METHOD_RE ratait."""
    s = h.strip()
    s = re.sub(r'\s+where\s+\w+\s*:.*$', '', s, flags=re.S)
    for _ in range(2):
        if not s.endswith(')'):
            return None
        depth = 0
        k = len(s) - 1
        while k >= 0:
            if s[k] == ')':
                depth += 1
            elif s[k] == '(':
                depth -= 1
                if depth == 0:
                    break
            k -= 1
        if k < 0:
            return None
        before = s[:k].rstrip()
        if before.endswith('>'):
            d = 0
            j = len(before) - 1
            while j >= 0:
                if before[j] == '>':
                    d += 1
                elif before[j] == '<':
                    d -= 1
                    if d == 0:
                        break
                j -= 1
            before = before[:j].rstrip()
        m = re.search(r'(operator\s*[^\s\w(]+|operator\s+\w+|~?[A-Za-z_]\w*)$', before)
        if not m:
            return None
        name = m.group(1)
        pre = before[:m.start()]
        if name in ('base', 'this') and pre.rstrip().endswith(':'):
            s = pre.rstrip()[:-1].rstrip()
            continue
        if pre and not re.search(r'[\s>\])?*]$', pre):
            return None
        if '=' in pre or ';' in pre:
            return None
        return _Sig(name, pre)
    return None


KEYWORDS_NOT_NAMES = {'if', 'for', 'foreach', 'while', 'switch', 'catch', 'using', 'lock', 'return', 'new',
                      'else', 'do', 'fixed', 'typeof', 'sizeof', 'nameof', 'default', 'checked', 'unchecked',
                      'throw', 'await', 'when', 'is', 'as', 'in', 'out', 'ref', 'base', 'stackalloc'}
MODIFIERS = {'public', 'private', 'protected', 'internal', 'static', 'readonly', 'const', 'volatile', 'extern',
             'override', 'virtual', 'abstract', 'sealed', 'new', 'partial', 'async', 'unsafe', 'file', 'required',
             'event', 'implicit', 'explicit'}


def strip_attributes(h):
    """Retire les groupes [ ... ] en tête (attributs). Renvoie (attributs, reste)."""
    s = h.lstrip()
    attrs = []
    while s.startswith('['):
        depth = 0
        k = 0
        for k, ch in enumerate(s):
            if ch == '[':
                depth += 1
            elif ch == ']':
                depth -= 1
                if depth == 0:
                    break
        attrs.append(s[:k + 1])
        s = s[k + 1:].lstrip()
    return attrs, s


def norm_ws(s):
    return re.sub(r'\s+', ' ', s).strip()


def classify_block_header(h):
    """h : en-tête nettoyé (sans attributs, espaces normalisés)."""
    if not h:
        return 'bloc'
    if re.match(r'^namespace\b', h):
        return 'namespace'
    if CTRL_RE.match(h):
        return 'controle'
    if h.endswith('=>') or re.search(r'\bdelegate\s*(\([^)]*\))?$', h):
        return 'lambda'
    if re.search(r'\bswitch$', h):
        return 'switch_expr'
    if ACCESSOR_RE.match(h):
        return 'accesseur'
    tm = TYPE_RE.search(h)
    if tm and '(' not in h.split(tm.group(0))[0] and not h.rstrip().endswith('=') and '=' not in h.split(tm.group(0))[0]:
        return 'type:' + tm.group(1).split()[0]
    if h.endswith('=') or h.endswith(',') or h.endswith('(') or h.endswith('return'):
        return 'initialiseur'
    m = match_sig(h)
    if m and m.group('name') not in KEYWORDS_NOT_NAMES:
        pre = (m.group('pre') or '').split()
        if set(pre) & {'new', 'return', 'await', 'throw', 'yield', 'case', '=>', 'is', 'as', 'in'}:
            return 'initialiseur'
        return 'methode'
    if re.search(r'\bnew\b', h):
        return 'initialiseur'
    if re.search(r'[A-Za-z_]\w*$', h) or re.search(r'\bthis\s*\[.*\]$', h):
        return 'propriete'
    return 'autre'


# ---------------------------------------------------------------- complexité

CC_PATTERNS = {
    'if': re.compile(r'\bif\b'),
    'case': re.compile(r'\bcase\b'),
    'for': re.compile(r'\bfor\b'),
    'foreach': re.compile(r'\bforeach\b'),
    'while': re.compile(r'\bwhile\b'),
    'catch': re.compile(r'\bcatch\b'),
    'when': re.compile(r'\bwhen\b'),
    '&&': re.compile(r'&&'),
    '||': re.compile(r'\|\|'),
    '?:': re.compile(r'(?<=\s)\?(?=\s)'),
    '??': re.compile(r'\?\?'),
    'motif and/or': re.compile(r'\b(?:and|or)\b'),
}


def cyclomatic(body_ns, switch_arms=0):
    counts = {k: len(p.findall(body_ns)) for k, p in CC_PATTERNS.items()}
    counts['bras switch'] = switch_arms
    return 1 + sum(counts.values()), counts


# ---------------------------------------------------------------- parse des membres

class FileModel:
    def __init__(self, path, text):
        self.path = path
        self.text = text
        lx = lex(text)
        self.nc = lx['nc']
        self.ns = lx['ns']
        self.comments = lx['comments']
        self.strings = lx['strings']
        self.starts = line_index(text)
        self.match = brace_map(self.ns)
        self.members = []   # dict(kind, name, type, start, end, ...)
        self.types = []
        self.blocks = []    # (open, close, kind, header)
        self._headers = {}
        self._parse()

    def line(self, pos):
        return pos2line(self.starts, pos)

    def header_before(self, i):
        """Texte entre le dernier ; { } et la position i (dans ns)."""
        j = i - 1
        ns = self.ns
        while j >= 0 and ns[j] not in ';{}':
            j -= 1
        return j + 1

    def block_kind(self, i):
        if i in self._headers:
            return self._headers[i]
        hs = self.header_before(i)
        attrs, h = strip_attributes(self.ns[hs:i])
        k = classify_block_header(norm_ws(h))
        self._headers[i] = (k, norm_ws(h))
        return self._headers[i]

    # -- découpage d'un corps de type / namespace / fichier
    def _parse(self):
        self._parse_members(0, len(self.ns), None, 'fichier')

    def _seg_first_pos(self, a, b):
        """Position du premier caractère de code du segment, après attributs."""
        seg = self.ns[a:b]
        attrs, rest = strip_attributes(seg)
        off = len(seg) - len(rest)
        k = a + off
        while k < b and self.ns[k].isspace():
            k += 1
        # attributs bruts (avec chaînes) pour la détection de [Fact], [DllImport]
        return k, self.nc[a:k]

    def _parse_members(self, a, b, owner, owner_kind):
        ns = self.ns
        i = a
        seg = a
        while i < b:
            c = ns[i]
            if c == ';':
                self._simple_member(seg, i, owner, owner_kind)
                i += 1
                seg = i
                continue
            if c == '{':
                close = self.match.get(i, b - 1)
                first, attrs_raw = self._seg_first_pos(seg, i)
                attrs, h = strip_attributes(ns[seg:i])
                h = norm_ws(h)
                kind = classify_block_header(h)
                if kind == 'namespace':
                    self._parse_members(i + 1, close, owner, 'namespace')
                    i = close + 1
                    seg = i
                    continue
                if kind.startswith('type:'):
                    tm = TYPE_RE.search(h)
                    tname = tm.group('name')
                    t = {'kind': kind[5:], 'name': tname, 'start': self.line(first), 'end': self.line(close),
                         'owner': owner, 'header': h, 'attrs': attrs_raw, 'pos': first}
                    self.types.append(t)
                    if kind != 'type:enum':
                        self._parse_members(i + 1, close, tname, kind[5:])
                    else:
                        t['enum_members'] = [x.strip().split('=')[0].strip() for x in ns[i + 1:close].split(',') if x.strip()]
                    i = close + 1
                    seg = i
                    continue
                if kind == 'methode' and owner_kind not in ('fichier', 'namespace'):
                    m = match_sig(h)
                    name = m.group('name')
                    pre = set((m.group('pre') or '').split())
                    self._add_method(name, owner, first, i, close, h, attrs_raw, pre, expr=False)
                    i = close + 1
                    seg = i
                    continue
                if kind in ('propriete',) and owner_kind not in ('fichier', 'namespace'):
                    name = 'this[]' if re.search(r'\bthis\s*\[', h) else re.search(r'([A-Za-z_]\w*)$', h).group(1)
                    pre = set(h.split()[:-1])
                    j = close + 1
                    while j < b and ns[j].isspace():
                        j += 1
                    end = close
                    if j < b and ns[j] == '=':
                        k = self._find_top_semicolon(j, b)
                        end = k
                    self.members.append({'kind': 'propriete', 'name': name, 'owner': owner, 'start': self.line(first),
                                         'end': self.line(end), 'header': h, 'attrs': attrs_raw, 'mods': pre,
                                         'pos': first, 'body': (i, close)})
                    i = end + 1
                    seg = i
                    continue
                # initialiseur, lambda, accesseur isolé... : le membre continue jusqu'au ';'
                i = close + 1
                continue
            i += 1

    def _find_top_semicolon(self, j, b):
        ns = self.ns
        depth = 0
        while j < b:
            ch = ns[j]
            if ch in '({[':
                depth += 1
            elif ch in ')}]':
                depth -= 1
            elif ch == ';' and depth <= 0:
                return j
            j += 1
        return b - 1

    def _add_method(self, name, owner, first, body_open, body_close, header, attrs_raw, pre, expr):
        body = self.ns[body_open:body_close + 1]
        arms = 0
        depth_max = 0
        if not expr:
            # profondeur : blocs de contrôle / lambdas / fonctions locales imbriqués
            stack = []
            cur = 0
            for k in range(body_open + 1, body_close):
                ch = self.ns[k]
                if ch == '{':
                    kind, h = self.block_kind(k)
                    ctl = kind in ('controle', 'lambda', 'switch_expr', 'methode')
                    stack.append(ctl)
                    if ctl:
                        cur += 1
                        depth_max = max(depth_max, cur)
                    if kind == 'switch_expr':
                        close = self.match.get(k)
                        if close:
                            inner = self.ns[k:close]
                            a = inner.count('=>') - len(re.findall(r'\b_\s*=>', inner))
                            arms += max(a, 0)
                elif ch == '}':
                    if stack and stack.pop():
                        cur -= 1
        else:
            for sm in re.finditer(r'\bswitch\s*\{', body):
                k = body_open + sm.end() - 1
                close = self.match.get(k)
                if close:
                    inner = self.ns[k:close]
                    arms += max(inner.count('=>') - len(re.findall(r'\b_\s*=>', inner)), 0)
        cc, detail = cyclomatic(body, arms)
        start = self.line(first)
        end = self.line(body_close)
        code_lines = sum(1 for ln in self.nc[self.starts[start - 1]:body_close + 1].split('\n') if ln.strip())
        self.members.append({'kind': 'methode', 'name': name, 'owner': owner, 'start': start, 'end': end,
                             'lignes': end - start + 1, 'lignes_code': code_lines, 'cc': cc, 'cc_detail': detail,
                             'profondeur': depth_max, 'header': header, 'attrs': attrs_raw, 'mods': pre,
                             'expr': expr, 'pos': first, 'body': (body_open, body_close)})

    def _simple_member(self, a, b, owner, owner_kind):
        ns = self.ns
        first, attrs_raw = self._seg_first_pos(a, b)
        attrs, s = strip_attributes(ns[a:b])
        s = norm_ws(s)
        if not s or owner_kind in ('fichier', 'namespace'):
            # file-scoped namespace, using, délégués au niveau fichier
            dm = re.search(r'\bdelegate\s+.*?([A-Za-z_]\w*)\s*(?:<[^>]*>)?\s*\(', s) if s else None
            if dm:
                self.types.append({'kind': 'delegate', 'name': dm.group(1), 'start': self.line(first),
                                   'end': self.line(b), 'owner': owner, 'header': s, 'attrs': attrs_raw, 'pos': first})
            return
        if re.match(r'^(using|namespace|extern alias)\b', s):
            return
        dm = re.search(r'(?:^|\s)delegate\s+.*?([A-Za-z_]\w*)\s*(?:<[^>]*>)?\s*\(', s)
        if dm and '=' not in s.split('delegate')[0]:
            self.types.append({'kind': 'delegate', 'name': dm.group(1), 'start': self.line(first),
                               'end': self.line(b), 'owner': owner, 'header': s, 'attrs': attrs_raw, 'pos': first})
            return
        # => au niveau supérieur ?
        arrow = self._top_arrow(s)
        if arrow is not None:
            left = s[:arrow].strip()
            m = match_sig(left)
            if m and m.group('name') not in KEYWORDS_NOT_NAMES and not re.search(r'\bnew\b', m.group('pre') or ''):
                # corps expression : position du => dans ns
                raw_arrow = ns.find('=>', first, b)
                self._add_method(m.group('name'), owner, first, raw_arrow, b, left, attrs_raw,
                                 set((m.group('pre') or '').split()), expr=True)
                return
            if '=' not in left:
                name = 'this[]' if re.search(r'\bthis\s*\[', left) else (re.search(r'([A-Za-z_]\w*)\s*$', left) or [None, None])[1]
                if name:
                    self.members.append({'kind': 'propriete', 'name': name, 'owner': owner, 'start': self.line(first),
                                         'end': self.line(b), 'header': left, 'attrs': attrs_raw,
                                         'mods': set(left.split()[:-1]), 'pos': first, 'body': (first, b)})
                    return
        m = match_sig(s)
        if m and m.group('name') not in KEYWORDS_NOT_NAMES and '=' not in (m.group('pre') or ''):
            pre = set((m.group('pre') or '').split())
            kind = 'pinvoke' if ('extern' in pre or ('partial' in pre and 'LibraryImport' in attrs_raw)) else 'decl_methode'
            self.members.append({'kind': kind, 'name': m.group('name'), 'owner': owner, 'start': self.line(first),
                                 'end': self.line(b), 'header': s, 'attrs': attrs_raw, 'mods': pre, 'pos': first})
            return
        # champ(s) / constante(s) / événement
        eq = self._top_eq(s)
        left = s if eq is None else s[:eq]
        pieces = self._split_top_commas(s)
        is_const = bool(re.search(r'\bconst\b', left))
        is_event = bool(re.search(r'\bevent\b', left))
        kind = 'constante' if is_const else ('evenement' if is_event else 'champ')
        names = []
        first_left = pieces[0]
        e0 = self._top_eq(first_left)
        fl = first_left if e0 is None else first_left[:e0]
        nm = re.search(r'([A-Za-z_]\w*)\s*$', fl.strip())
        if nm:
            names.append(nm.group(1))
        for p in pieces[1:]:
            nm2 = re.match(r'\s*([A-Za-z_]\w*)\s*(=|$)', p)
            if nm2:
                names.append(nm2.group(1))
        mods = set(fl.split()[:-1])
        for nmv in names:
            self.members.append({'kind': kind, 'name': nmv, 'owner': owner, 'start': self.line(first),
                                 'end': self.line(b), 'header': s[:200], 'attrs': attrs_raw, 'mods': mods, 'pos': first})

    @staticmethod
    def _top_arrow(s):
        depth = 0
        for k in range(len(s) - 1):
            ch = s[k]
            if ch in '([{<':
                depth += 1 if ch != '<' else 0
            elif ch in ')]}':
                depth -= 1
            elif ch == '=' and s[k + 1] == '>' and depth == 0:
                return k
        return None

    @staticmethod
    def _top_eq(s):
        depth = 0
        for k, ch in enumerate(s):
            if ch in '([{':
                depth += 1
            elif ch in ')]}':
                depth -= 1
            elif ch == '=' and depth == 0:
                prev = s[k - 1] if k else ''
                nxt = s[k + 1] if k + 1 < len(s) else ''
                if nxt in '=>' or prev in '=!<>':
                    continue
                return k
        return None

    @staticmethod
    def _split_top_commas(s):
        out = []
        depth = 0
        angle = 0
        cur = []
        eq_seen = False
        for ch in s:
            if ch in '([{':
                depth += 1
            elif ch in ')]}':
                depth -= 1
            elif ch == '<' and not eq_seen:
                angle += 1
            elif ch == '>' and not eq_seen and angle:
                angle -= 1
            elif ch == '=':
                eq_seen = True
            if ch == ',' and depth == 0 and angle == 0:
                out.append(''.join(cur))
                cur = []
                eq_seen = False
                continue
            cur.append(ch)
        out.append(''.join(cur))
        return out

    # -- lignes
    def line_stats(self):
        raw = self.text.split('\n')
        ncl = self.nc.split('\n')
        total = len(raw) if not self.text.endswith('\n') else len(raw) - 1
        code = comment = blank = mixed = pre = 0
        for k in range(total):
            r = raw[k].strip()
            c = ncl[k].strip()
            if not r:
                blank += 1
            elif r.startswith('#'):
                pre += 1
            elif c:
                code += 1
                if len(c) < len(r) and ('//' in r or '/*' in r):
                    mixed += 1
            else:
                comment += 1
        return {'total': total, 'code': code, 'commentaire': comment, 'vide': blank,
                'code_avec_commentaire_fin': mixed, 'preprocesseur': pre}
