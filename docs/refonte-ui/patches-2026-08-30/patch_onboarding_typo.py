"""CH3 passe 2, lot B — Onboarding descend sur les rôles de la charte et rejoint son échelle.

Deux décisions d'Antoine du 2026-08-30, dans cet ordre :

1. Descente sur les rôles existants **plus un rôle `Display` 26/700** pour les deux gros titres.
2. **Suppression de `ONBOARDING_UI_SCALE`**, la constante 0,75 que la fenêtre appliquait à ses
   139 dimensions, polices comprises. C'est elle qui rendait ses « 28 px » à 21 et son corps de
   texte à 12,75 quand le reste de l'application est à 15. La fenêtre grandit d'un tiers et
   devient la seule chose qu'elle n'était pas : à l'échelle de l'application.

Quinze handles de police tombent sur six rôles. Deux pertes assumées, faute de jeton :
l'italique de `_hFontSmall` et de `_hFontReassure` — la charte n'en a pas, et Paramètres rend
déjà son message de validation droit ; et la taille sur mesure de `_hFontReassure`, calibrée à
9,7 px pour tenir sur une ligne à 175 %, qui passe à Secondary 13. Le rendu dira si cette ligne
tient encore.

⚠️ OnboardingWindow.cs porte un BOM et **1 470 CRLF pour 246 LF**. La fin de ligne ne se déduit
ni du fichier ni d'une ancre d'une seule ligne : chaque bloc est essayé en CRLF puis en LF, et le
remplacement reprend celle qui a matché.

Et le premier bloc **est** mixte, ce que seule la mesure a montré : la déclaration de
`_hFontVersion` se termine par un saut de ligne isolé au milieu de dix-huit lignes en CRLF. Ses
octets sont donc pris tels quels entre deux bornes, jamais reconstruits par jointure. Cette
Quatre lignes portant un saut isolé sont supprimées par ce patch — la déclaration de
`_hFontVersion`, ses deux lignes dans `CreateFonts` et `DestroyFonts`, et `ONBOARDING_UI_SCALE`.
Le script exige donc **LF moins quatre** exactement, et refuse d'écrire pour tout autre écart.
"""

import pathlib
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = pathlib.Path(__file__).resolve().parents[3]
ONBOARDING = ROOT / "src" / "OnboardingWindow.cs"
THEME = ROOT / "src" / "Theme.cs"

CRLF = b"\r\n"
LF = b"\n"
BOM = b"\xef\xbb\xbf"

# ══════════════════════════════════════════════════════════════════════════
# Theme.cs — le septième rôle
# ══════════════════════════════════════════════════════════════════════════

theme = THEME.read_bytes()
if theme.count(CRLF) or theme[:3] == BOM:
    sys.exit("Theme.cs n'est plus en LF pur sans BOM")


def theme_replace(name, old, new):
    global theme
    b = old.encode("utf-8")
    if theme.count(b) != 1:
        sys.exit(f"Theme.cs / {name} : {theme.count(b)} occurrences, attendu 1")
    theme = theme.replace(b, new.encode("utf-8"))
    print(f"  Theme.cs  {name}")


theme_replace(
    "FontRole.Display",
    """    /// <summary>Titre de fenêtre dessiné dans le corps, pas la barre système. 24 pt, graisse 600.</summary>
    WindowTitle,""",
    """    /// <summary>Titre de fenêtre dessiné dans le corps, pas la barre système. 24 pt, graisse 600.</summary>
    WindowTitle,

    /// <summary>
    /// Titre d'accueil, plus grand que celui d'une fenêtre ordinaire. 26 px, graisse 700.
    ///
    /// Septième rôle, ajouté le 2026-08-30 sur arbitrage d'Antoine, et le seul depuis que
    /// l'échelle a été figée à CH1. Motif : la fenêtre de bienvenue portait deux titres à 28 et
    /// 26 px pour 700 de graisse ; les faire tomber sur WindowTitle (24/600) leur retirait le
    /// poids qui distingue l'accueil du reste de l'application. Une taille de plus, pas une
    /// famille : c'est le même Segoe UI, et rien d'autre ne l'emploie.
    /// </summary>
    Display,""",
)

theme_replace(
    "Metrics(Display)",
    """        FontRole.WindowTitle => (24, 600, SegoeUi),""",
    """        FontRole.WindowTitle => (24, 600, SegoeUi),
        FontRole.Display => (26, 700, SegoeUi),""",
)

# ══════════════════════════════════════════════════════════════════════════
# OnboardingWindow.cs
# ══════════════════════════════════════════════════════════════════════════

data = ONBOARDING.read_bytes()
crlf_before = data.count(CRLF)
lf_before = data.count(LF) - crlf_before
print(f"  OnboardingWindow.cs  avant : CRLF {crlf_before}, LF isolés {lf_before}, "
      f"BOM {data[:3] == BOM}")


def block(name, old_lines, new_lines):
    """Bloc à fins de ligne homogènes : essayé en CRLF puis en LF."""
    global data
    for eol, label in ((CRLF, "CRLF"), (LF, "LF  ")):
        old = eol.join(line.encode("utf-8") for line in old_lines)
        found = data.count(old)
        if found == 1:
            data = data.replace(old, eol.join(line.encode("utf-8") for line in new_lines))
            print(f"  {name:34s} {label}  {len(old_lines)} → {len(new_lines)} lignes")
            return
        if found > 1:
            sys.exit(f"{name} : {found} occurrences en {label}, attendu 1")
    sys.exit(f"{name} : introuvable en CRLF comme en LF — bloc mixte ?")


# ── 1. Le bloc mixte : octets réels entre deux bornes ─────────────────────
START = "    // DPI scaling — mutable, recalculé sur WM_DPICHANGED"
END = "    private IntPtr _hFontLinkStrong;"

s_b, e_b = START.encode("utf-8"), END.encode("utf-8")
if data.count(s_b) != 1 or data.count(e_b) != 1:
    sys.exit(f"bloc des champs : bornes non uniques ({data.count(s_b)}, {data.count(e_b)})")

i = data.index(s_b)
j = data.index(e_b) + len(e_b)
region = data[i:j]
stray = region.count(LF) - region.count(CRLF)
if stray != 1:
    sys.exit(f"bloc des champs : {stray} saut(s) isolé(s), 1 attendu — le fichier a changé")

NEW_FIELDS = [
    "    // DPI scaling — mutable, recalculé sur WM_DPICHANGED",
    "    private float _dpiScale;",
    "",
    "    // ONBOARDING_UI_SCALE, la constante 0,75 qui multipliait ces 139 dimensions, a été",
    "    // supprimée le 2026-08-30 sur arbitrage d'Antoine. Elle rendait le corps de texte de",
    "    // cette fenêtre à 12,75 px quand le reste de l'application est à 15, et ses « 28 px »",
    "    // de titre à 21 : la fenêtre de bienvenue était la seule à ne pas être à l'échelle de",
    "    // l'application, sans que rien ne le dise. Tout grandit d'un tiers.",
    "    private int S(int val) => (int)(val * _dpiScale);",
    "",
    "    /// <summary>L'échelle en points par pouce, dont Theme a besoin pour ses polices.</summary>",
    "    private int _dpi => (int)Math.Round(96 * _dpiScale);",
    "",
    "    // Polices — quinze handles pour dix tailles distinctes jusqu'au 2026-08-30, six rôles de",
    "    // la charte depuis. Ce ne sont plus des champs : Theme tient le cache, indexé par rôle et",
    "    // par DPI, et une bascule d'échelle n'a donc plus rien à recréer ici.",
    "    private IntPtr _hFontTitle => Theme.Font(FontRole.Display, _dpi);",
    "    private IntPtr _hFontPageTitle => Theme.Font(FontRole.Display, _dpi);",
    "    private IntPtr _hFontSubtitle => Theme.Font(FontRole.SectionTitle, _dpi);",
    "    private IntPtr _hFontBannerBold => Theme.Font(FontRole.SectionTitle, _dpi);",
    "    private IntPtr _hFontStepSummary => Theme.Font(FontRole.SectionTitle, _dpi);",
    "    private IntPtr _hFontText => Theme.Font(FontRole.Body, _dpi);",
    "    private IntPtr _hFontFeatureDesc => Theme.Font(FontRole.Body, _dpi);",
    "    private IntPtr _hFontBold => Theme.Font(FontRole.BodyStrong, _dpi);",
    "    private IntPtr _hFontButton => Theme.Font(FontRole.BodyStrong, _dpi);",
    "    private IntPtr _hFontSection => Theme.Font(FontRole.BodyStrong, _dpi);",
    "    private IntPtr _hFontLink => Theme.Font(FontRole.Body, _dpi, underlined: true);",
    "    private IntPtr _hFontLinkStrong => Theme.Font(FontRole.BodyStrong, _dpi, underlined: true);",
    "    // L'italique de ces deux-là est perdu : la charte n'a pas de jeton d'italique, et",
    "    // Paramètres rend déjà son message de validation droit. _hFontReassure perd en plus sa",
    "    // taille sur mesure — 9,7 px, calibrés pour tenir sur une ligne à 175 %.",
    "    private IntPtr _hFontSmall => Theme.Font(FontRole.Secondary, _dpi);",
    "    private IntPtr _hFontReassure => Theme.Font(FontRole.Secondary, _dpi);",
    "    private IntPtr _hFontVersion => Theme.Font(FontRole.Mono, _dpi);",
]

data = data[:i] + CRLF.join(line.encode("utf-8") for line in NEW_FIELDS) + data[j:]
print(f"  {'champs et échelle':34s} octets réels, saut isolé absorbé")

# ── 2. La constante d'échelle elle-même ───────────────────────────────────
block(
    "constante supprimée",
    [
        "    private const int BASE_FLAG_H = 30;",
        "    private const float ONBOARDING_UI_SCALE = 0.75f;",
    ],
    ["    private const int BASE_FLAG_H = 30;"],
)

# ── 3. CreateFonts et DestroyFonts n'ont plus rien à faire ────────────────
# Mixte lui aussi, et pour la même raison : les deux lignes qui nomment _hFontVersion se
# terminent par un saut isolé. Même traitement — octets réels entre deux bornes.
F_START = "    private void CreateFonts()"
F_END = "        Win32.DeleteObject(_hFontLinkStrong);"

fs_b, fe_b = F_START.encode("utf-8"), F_END.encode("utf-8")
if data.count(fs_b) != 1 or data.count(fe_b) != 1:
    sys.exit(f"bloc des polices : bornes non uniques ({data.count(fs_b)}, {data.count(fe_b)})")

fi = data.index(fs_b)
fj = data.index(b"}", data.index(fe_b)) + 1
fregion = data[fi:fj]
fstray = fregion.count(LF) - fregion.count(CRLF)
if fstray != 2:
    sys.exit(f"bloc des polices : {fstray} saut(s) isolé(s), 2 attendus — le fichier a changé")

NEW_FONTS = [
    "    // CreateFonts et DestroyFonts ont disparu le 2026-08-30 : les polices viennent du cache",
    "    // de Theme, indexé par rôle et par DPI, et ce cache n'appartient pas à cette fenêtre.",
    "    // Les détruire ici les retirerait sous les autres.",
]
data = data[:fi] + CRLF.join(line.encode("utf-8") for line in NEW_FONTS) + data[fj:]
print(f"  {'polices : deux méthodes retirées':34s} octets réels, 2 sauts isolés absorbés")


crlf_after = data.count(CRLF)
lf_after = data.count(LF) - crlf_after
# Quatre sauts isolés disparaissent, tous sur des lignes que ce patch supprime : celui de la
# déclaration de _hFontVersion, les deux de ses lignes dans CreateFonts et DestroyFonts, et
# celui de ONBOARDING_UI_SCALE — ce dernier est la raison pour laquelle le bloc 2 a matché en
# LF et non en CRLF. Tout autre écart signifie qu'une fin de ligne a changé de nature.
if lf_after != lf_before - 4:
    sys.exit(f"LF isolés passés de {lf_before} à {lf_after}, attendu {lf_before - 4}")

ONBOARDING.write_bytes(data)
THEME.write_bytes(theme)
print(f"  OnboardingWindow.cs  après : CRLF {crlf_after}, LF isolés {lf_after}")
