# Couverture par fichier : lignes normalisées appartenant à un bloc (>= MIN_RUN) partagé
# avec au moins un autre fichier. Évite le double comptage par paire.
import importlib.util, pathlib, itertools, collections, sys

HERE = pathlib.Path(__file__).parent
spec = importlib.util.spec_from_file_location("dup", HERE / "fenetres_dup.py")
# Charger les fonctions sans exécuter le rapport : relire le source jusqu'aux définitions.
src = (HERE / "fenetres_dup.py").read_text(encoding="utf-8").split("def run(")[0]
ns = {}
sys.argv = [sys.argv[0], sys.argv[1] if len(sys.argv) > 1 else "4"]
exec(src, ns)
norm_lines, blocks, ZONE, EXTRA = ns["norm_lines"], ns["blocks"], ns["ZONE"], ns["EXTRA"]

def coverage(files):
    covered = collections.defaultdict(set)
    for a, b in itertools.permutations(files, 2):
        for (a0, a1, _, _, _) in blocks(a, b):
            for ln, _ in norm_lines(a):
                if a0 <= ln <= a1:
                    covered[a].add(ln)
    return covered

for label, files in (("zone", ZONE), ("zone + UsageStatsWindow", ZONE + ["UsageStatsWindow.cs"])):
    cov = coverage(files)
    print(f"== {label}")
    tot_n = tot_c = 0
    for f in files:
        n = len(norm_lines(f)); c = len(cov[f])
        tot_n += n; tot_c += c
        print(f"  {f:28s} {c:4d} / {n:4d} lignes normalisées dans un bloc partagé ({100*c/n:.0f} %)")
    print(f"  TOTAL {tot_c} / {tot_n}")
