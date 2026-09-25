"""Copie l'audit du scratchpad vers docs/audit-2026-09-25-v1.3.0-simplicite/ du dépôt app.

Refuse d'écrire si le dossier cible existe déjà. Exclut binaires, bin/obj, caches.
Applique aux .md les espaces insécables (avant : ; ? ! » et après «), hors code et hors blocs ```.
Tout est écrit en UTF-8 sans BOM et LF (convention de docs/).
"""
import pathlib, re, shutil, sys

SRC = pathlib.Path(__file__).resolve().parent.parent
DEPOT = pathlib.Path("D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store")
DST = DEPOT / "docs" / "audit-2026-09-25-v1.3.0-simplicite"

if DST.exists() and "--reprendre" not in sys.argv:
    sys.exit(f"ARRÊT : {DST} existe déjà (--reprendre pour réécrire les fichiers de ce plan)")

def lire(p):
    b = p.read_bytes()
    try:
        return b.decode("utf-8-sig")
    except UnicodeDecodeError:
        return b.decode("cp850")  # sortie console Windows (dotnet run redirigé)

EXCLUS_DIRS = {"bin", "obj", "__pycache__", "publish", "build-pompe-aot"}
EXCLUS_SUFFIXES = {".exe", ".pdb", ".dll", ".pyc"}
EXCLUS_NOMS = {"main-VirtualKeyboard.cs.txt"}

plan = {
    "rapport.md": "rapport.md",
    "annexe-constats.md": "annexe-constats.md",
    "GRILLE.md": "GRILLE.md",
    "donnees/constats.json": "donnees/constats.json",
    "verdicts.json": "donnees/verdicts.json",
    "metriques.json": "donnees/metriques.json",
    "ponytail-recherche.md": "notes/ponytail-recherche.md",
    "page/gabarit.html": "page/gabarit.html",
    "page/synthese.html": "page/synthese.html",
}
for z in ("moteur", "app", "lecons", "fenetres", "visuel", "transversal"):
    plan[f"constats-{z}.json"] = f"donnees/zones/constats-{z}.json"
    plan[f"notes-{z}.md"] = f"notes/notes-{z}.md"
for g in ("groupe1", "groupe2", "transversal"):
    plan[f"contre-avis-{g}.md"] = f"contre-avis/contre-avis-{g}.md"

def ajouter_arbre(src_rel, dst_rel):
    base = SRC / src_rel
    for p in base.rglob("*"):
        if not p.is_file():
            continue
        rel = p.relative_to(base)
        if set(rel.parts[:-1]) & EXCLUS_DIRS or p.suffix in EXCLUS_SUFFIXES or p.name in EXCLUS_NOMS:
            continue
        plan[f"{src_rel}/{rel.as_posix()}"] = f"{dst_rel}/{rel.as_posix()}"

for b in ("bench-moteur", "bench-app", "bench-lecons", "bench-visuel"):
    ajouter_arbre(b, f"bancs/{b.removeprefix('bench-')}")
ajouter_arbre("contre-avis/pompe", "bancs/pompe-sta")
plan["contre-avis/resultats-pompe.txt"] = "bancs/pompe-sta/resultats.txt"
ajouter_arbre("outils", "outils")
ajouter_arbre("contre-avis-transversal", "outils/contre-avis-transversal")
plan["contre-avis2/layers.py"] = "outils/contre-avis-groupe2/layers.py"
for p in (SRC.parent / "correctifs").glob("*.py"):
    plan[f"../correctifs/{p.name}"] = f"outils/correctifs/{p.name}"
for p in SRC.glob("*.py"):
    plan[p.name] = f"outils/zones/{p.name}"
for p in SRC.glob("*.sh"):
    plan[p.name] = f"outils/zones/{p.name}"

NBSP = "\u00a0"
def typo(texte):
    out, dans_bloc = [], False
    for ligne in texte.split("\n"):
        if ligne.lstrip().startswith("```"):
            dans_bloc = not dans_bloc
            out.append(ligne)
            continue
        if dans_bloc:
            out.append(ligne)
            continue
        morceaux = re.split(r"(`[^`]*`)", ligne)
        for i in range(0, len(morceaux), 2):
            m = morceaux[i]
            m = re.sub(r"(?<=\S) ([:;?!»])", NBSP + r"\1", m)
            m = re.sub(r"« ", "«" + NBSP, m)
            morceaux[i] = m
        out.append("".join(morceaux))
    return "\n".join(out)

total = 0
for s, d in sorted(plan.items(), key=lambda kv: kv[1]):
    sp, dp = SRC / s, DST / d
    if not sp.exists():
        print("absent :", s)
        continue
    dp.parent.mkdir(parents=True, exist_ok=True)
    if sp.suffix in {".md", ".txt", ".py", ".sh", ".cs", ".csproj", ".json", ".html"}:
        t = lire(sp).replace("\r\n", "\n")
        if sp.suffix == ".md":
            t = typo(t)
        dp.write_text(t, encoding="utf-8", newline="\n")
    else:
        shutil.copyfile(sp, dp)
    total += dp.stat().st_size
    print(f"{dp.stat().st_size:>8}  {d}")
print(f"{len(plan)} fichiers prévus, {total // 1024} Ko écrits dans {DST}")
