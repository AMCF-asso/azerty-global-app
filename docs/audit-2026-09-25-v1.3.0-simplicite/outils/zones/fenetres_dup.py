# Mesure de duplication entre les fichiers de fenêtres (zone F-).
# 1) Blocs d'au moins MIN_RUN lignes normalisées consécutives identiques entre deux fichiers.
# 2) Lignes non triviales présentes dans au moins 2 fichiers.
# Normalisation : espaces retirés, commentaires // retirés, lignes vides ignorées.
import re, pathlib, itertools, collections, sys

SRC = pathlib.Path(r"D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src")
ZONE = ["SettingsWindow.cs", "OnboardingWindow.cs", "AboutWindow.cs", "PauseDurationDialog.cs",
        "LayoutConflictWindow.cs", "MaintainableLayersWindow.cs"]
EXTRA = ["LessonsWindow.cs", "UsageStatsWindow.cs", "LayerIndicatorWindow.cs", "VirtualKeyboard.cs",
         "LearningModule.cs"]
MIN_RUN = int(sys.argv[1]) if len(sys.argv) > 1 else 4
TRIVIAL = {"{", "}", "};", "break;", "returnIntPtr.Zero;", "try", "finally", "else", "});",
           "returnWin32.DefWindowProcW(hWnd,msg,wParam,lParam);", "catch", ")", "("}

def norm_lines(name):
    out = []
    for i, raw in enumerate((SRC / name).read_text(encoding="utf-8").splitlines(), 1):
        s = re.sub(r"//.*$", "", raw)
        s = re.sub(r"\s+", "", s)
        if not s or s.startswith("///"):
            continue
        out.append((i, s))
    return out

def blocks(a, b):
    la, lb = norm_lines(a), norm_lines(b)
    ta = [s for _, s in la]; tb = [s for _, s in lb]
    idx = collections.defaultdict(list)
    for j, s in enumerate(tb):
        idx[s].append(j)
    used_a = set(); res = []
    i = 0
    while i < len(ta):
        best = (0, None)
        if ta[i] not in TRIVIAL:
            for j in idx.get(ta[i], []):
                k = 0
                while i + k < len(ta) and j + k < len(tb) and ta[i + k] == tb[j + k]:
                    k += 1
                if k > best[0]:
                    best = (k, j)
        k, j = best
        nontriv = sum(1 for s in ta[i:i + k] if s not in TRIVIAL)
        if k >= MIN_RUN and nontriv >= MIN_RUN - 1:
            res.append((la[i][0], la[i + k - 1][0], lb[j][0], lb[j + k - 1][0], k))
            i += k
        else:
            i += 1
    return res

def run(files, label):
    print(f"##### Blocs >= {MIN_RUN} lignes identiques ({label})")
    total = collections.Counter()
    for a, b in itertools.combinations(files, 2):
        res = blocks(a, b)
        if not res:
            continue
        n = sum(r[4] for r in res)
        total[(a, b)] = n
        print(f"--- {a} <-> {b} : {n} lignes normalisées en {len(res)} blocs")
        for r in res:
            print(f"    {a}:{r[0]}-{r[1]}  ==  {b}:{r[2]}-{r[3]}  ({r[4]} l.)")
    print("TOTAL paires:", sum(total.values()))

run(ZONE, "zone")
run(ZONE + EXTRA, "zone + fenêtres hors zone")

# Lignes non triviales partagées par >= 2 fichiers de la zone
print("##### Lignes non triviales présentes dans >= 3 fichiers de zone")
occ = collections.defaultdict(set)
for f in ZONE:
    for _, s in norm_lines(f):
        if s not in TRIVIAL and len(s) > 12:
            occ[s].add(f)
multi = [(s, fs) for s, fs in occ.items() if len(fs) >= 3]
print("nb lignes distinctes:", len(multi))
for s, fs in sorted(multi, key=lambda x: -len(x[1]))[:60]:
    print(f"  [{len(fs)}] {s[:120]}")
