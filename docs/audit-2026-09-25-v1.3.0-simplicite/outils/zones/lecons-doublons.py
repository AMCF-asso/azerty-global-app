"""Mesure des doublons entre LearningModule.cs, LessonsWindow.cs et KeyboardRenderer.cs.

Compare des blocs de lignes (normalisés : espaces de tête retirés, lignes vides et
commentaires ignorés) et compte les lignes identiques. Lecture seule.
"""
from pathlib import Path

SRC = Path(r"D:/My files/Keyboard Layouts/projects/azerty-global/components/microsoft-store/src")


def block(name, start, end):
    lines = (SRC / name).read_text(encoding="utf-8").splitlines()[start - 1:end]
    out = []
    for raw in lines:
        s = raw.strip()
        if not s or s.startswith("//") or s.startswith("///"):
            continue
        out.append(s)
    return out


def compare(label, a, b):
    sa, sb = set(a), set(b)
    common = [l for l in a if l in sb]
    print(f"{label}: A={len(a)} lignes utiles, B={len(b)} lignes utiles, "
          f"lignes de A presentes a l'identique dans B={len(common)} ({100*len(common)//max(1,len(a))} %)")


# 1. Saisie positionnelle (texte attendu de la touche physique)
lm = block("LearningModule.cs", 1174, 1226) + block("LearningModule.cs", 1577, 1600)
lw = block("LessonsWindow.cs", 2749, 2825)
compare("Saisie positionnelle LM 1174-1226+1577-1600 vs LW 2749-2825", lm, lw)

# 2. Rendu clavier LM vs KeyboardRenderer
lmk = block("LearningModule.cs", 2373, 3195)
kr = block("KeyboardRenderer.cs", 1, 863)
compare("Rendu clavier LM 2373-3195 vs KeyboardRenderer 1-863", lmk, kr)

# 3. Info-bulles LM vs KeyboardRenderer
lmt = block("LearningModule.cs", 577, 655)
compare("Info-bulles LM 577-655 vs KeyboardRenderer", lmt, kr)

# 4. Tables statiques LM vs KeyboardRenderer
lms = block("LearningModule.cs", 134, 155) + block("LearningModule.cs", 3025, 3133)
compare("Tables statiques et filtres LM 134-155+3025-3133 vs KeyboardRenderer", lms, kr)

# 5. Parse character-index LM vs LessonHintProvider
lmc = block("LearningModule.cs", 660, 736) + block("LearningModule.cs", 1084, 1103)
lhp = block("LessonHintProvider.cs", 72, 138)
compare("Parse character-index LM 660-736+1084-1103 vs LessonHintProvider 72-138", lmc, lhp)

# 6. Deux subclass proc identiques dans LM
q = block("LearningModule.cs", 2825, 2852)
s = block("LearningModule.cs", 2900, 2927)
compare("QuitButtonSubclassProc vs SkipButtonSubclassProc", q, s)

# Tailles des grands blocs de LearningModule
blocks_lm = [
    ("Etapes + constantes + couleurs + tables", 1, 155),
    ("LearningTweaks (reglages live)", 157, 240),
    ("Champs + ctor", 242, 483),
    ("Info-bulles Win32", 485, 655),
    ("Parse character-index", 657, 736),
    ("Polices + fenetre + controles + langue", 738, 1038),
    ("Show/Close/pause/focus", 1040, 1145),
    ("Evenements + souris", 1147, 1274),
    ("WndProc", 1276, 1523),
    ("Saisie + navigation + boutons en-tete", 1525, 1769),
    ("Guidage (highlight)", 1771, 1988),
    ("Rendu pages (exercice, choix, final, legende)", 1990, 2368),
    ("Rendu clavier virtuel + boutons owner-draw + filtres", 2370, 3195),
    ("Dispose", 3197, 3245),
]
print("\nLearningModule.cs, blocs :")
for name, a, b in blocks_lm:
    print(f"  {a:>4}-{b:<4} {b-a+1:>4} l.  {name}")

blocks_lw = [
    ("Constantes, champs, ctor", 1, 176),
    ("Langue + catalogue + Defi", 178, 305),
    ("Polices", 307, 377),
    ("Fenetre, DPI, taille, redimensionnement", 379, 749),
    ("Selection + demarrage seance", 751, 819),
    ("WndProc", 821, 949),
    ("Rendu : en-tete, barre laterale, reglages", 951, 1155),
    ("Rendu : lecon, ligne cible, recap, boutons, clavier", 1157, 1509),
    ("Mode libre (rendu + edition)", 1511, 1867),
    ("Primitives de dessin + info-bulles + clic + focus", 1869, 2139),
    ("Clavier/saisie/minuteries", 2141, 2261),
    ("Fin d'exercice, partage Defi, indices", 2263, 2458),
    ("Bascules, navigation, formatage", 2460, 2731),
    ("Saisie positionnelle + Hide/Dispose", 2733, 2874),
]
print("\nLessonsWindow.cs, blocs :")
for name, a, b in blocks_lw:
    print(f"  {a:>4}-{b:<4} {b-a+1:>4} l.  {name}")
