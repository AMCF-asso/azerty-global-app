"""Les cartes d'Onboarding se mesurent sur leur texte.

Suite directe de `patch_onboarding_mesure.py`, et ce qu'il a rendu visible. La fenêtre mesurait
enfin son contenu, mais **le levier typographique restait sans effet sur elle** : mesuré le
2026-08-30, échelle du texte à 0,90, hauteur inchangée à 724 px. La densité, elle, agissait.

Cause : le contenu n'était pas mesuré non plus. Chacun des trois peintres de carte portait sa
copie des mêmes valeurs en dur — titre de 24 px, pastille de 34 × 24, interligne de 22 — et
surtout un plancher, `FEATURE_CARD_MIN_H = 73` ou `STEP_CARD_MIN_H = 78`. Cinq cartes à 73 font
**365 px sur les 724** de l'étape 1. Réduire le texte laissait les cartes à leur plancher avec
plus de vide dedans, exactement le défaut que la suppression de `BASE_WIN_H` venait de corriger
un cran plus haut.

Les quatre valeurs deviennent des mesures, dans `OnboardingWindow.Theme.cs`, une seule fois pour
les trois peintres. Le seul plancher qui reste est la pastille : une carte ne peut pas être plus
courte que le numéro qu'elle porte, et celui-là se mesure aussi.
"""

import pathlib
import re
import sys

sys.stdout.reconfigure(encoding="utf-8")

ROOT = pathlib.Path(__file__).resolve().parents[3]
TARGET = ROOT / "src" / "OnboardingWindow.cs"

data = TARGET.read_bytes()
if data[:3] != b"\xef\xbb\xbf":
    sys.exit("OnboardingWindow.cs a perdu son BOM")

LF_BEFORE = data.count(b"\n") - data.count(b"\r\n")
text = data.decode("utf-8")


def replace(name, old, new, expected=1):
    global text
    lines = old.replace("\r\n", "\n").split("\n")
    rx = re.compile(r"\r?\n".join(re.escape(l) for l in lines))
    matches = list(rx.finditer(text))
    if len(matches) != expected:
        sys.exit(f"{name} : {len(matches)} occurrence(s), {expected} attendue(s)\n---\n{old}\n---")
    out, cursor = [], 0
    for m in matches:
        eol = "\r\n" if "\r\n" in m.group(0) else "\n"
        out.append(text[cursor:m.start()])
        out.append(new.replace("\r\n", "\n").replace("\n", eol))
        cursor = m.end()
    out.append(text[cursor:])
    text = "".join(out)
    print(f"  {name:44s} {expected}×")


# ── 1. Les deux planchers s'en vont ─────────────────────────────────────────
replace(
    "planchers de carte supprimes",
    """    private const int STEP_CARD_MIN_H = 78;
    private const int FEATURE_CARD_MIN_H = 73;""",
    """    // STEP_CARD_MIN_H = 78 et FEATURE_CARD_MIN_H = 73 sont partis le 2026-08-30. Ils étaient
    // la vraie hauteur de cette fenêtre — cinq cartes de l'étape 1 à 73 px, soit 365 sur 724 —
    // et ils rendaient l'échelle typographique sans effet : le texte rétrécissait, la carte non.
    // Le plancher est désormais la pastille, mesurée, dans OnboardingWindow.Theme.cs.""",
)

# ── 2. Les trois peintres mesurent ──────────────────────────────────────────
replace(
    "pastille et titre mesures",
    """        int badgeW = S(34);
        int badgeGap = S(16);
        int contentWidth = cw - margin * 2;
        int textX = margin + cardPaddingX + badgeW + badgeGap;
        int textWidth = contentWidth - cardPaddingX * 2 - badgeW - badgeGap;
        int titleHeight = S(24);""",
    """        int badgeW = BadgeWidth(hdc);
        int badgeGap = S(16);
        int contentWidth = cw - margin * 2;
        int textX = margin + cardPaddingX + badgeW + badgeGap;
        int textWidth = contentWidth - cardPaddingX * 2 - badgeW - badgeGap;
        int titleHeight = CardTitleHeight(hdc);""",
    expected=3,
)

replace(
    "hauteur de DrawStepCard",
    """        int descHeight = MeasureTextHeight(hdc, _hFontText, description, textWidth);
        int cardHeight = Math.Max(S(minCardHeight), cardPaddingY * 2 + titleHeight + descHeight + S(4));""",
    """        int descHeight = MeasureTextHeight(hdc, _hFontText, description, textWidth);
        int cardHeight = Math.Max(CardFloor(hdc), cardPaddingY * 2 + titleHeight + descHeight + S(4));""",
)

replace(
    "hauteur de DrawStepCardWithRuns",
    """        int descHeight = GdiHelpers.MeasureColoredRunsHeight(hdc, textWidth, S(22), descriptionRuns);
        int cardHeight = Math.Max(S(minCardHeight), cardPaddingY * 2 + titleHeight + descHeight + S(4));""",
    """        int descHeight = GdiHelpers.MeasureColoredRunsHeight(hdc, textWidth, CardRunLineHeight(hdc), descriptionRuns);
        int cardHeight = Math.Max(CardFloor(hdc), cardPaddingY * 2 + titleHeight + descHeight + S(4));""",
)

replace(
    "hauteur de DrawToggleStepCard",
    """        int shortcutHeight = GdiHelpers.MeasureColoredRunsHeight(hdc, textWidth, S(22), shortcutRuns);
        int cardHeight = Math.Max(S(78), cardPaddingY * 2 + titleHeight + shortcutHeight + S(10));""",
    """        int shortcutHeight = GdiHelpers.MeasureColoredRunsHeight(hdc, textWidth, CardRunLineHeight(hdc), shortcutRuns);
        int cardHeight = Math.Max(CardFloor(hdc), cardPaddingY * 2 + titleHeight + shortcutHeight + S(10));""",
)

replace(
    "rendu des fragments de DrawStepCardWithRuns",
    """        GdiHelpers.DrawColoredRuns(hdc, textX, cardTop + cardPaddingY + titleHeight, textWidth, S(22), descriptionRuns);""",
    """        GdiHelpers.DrawColoredRuns(hdc, textX, cardTop + cardPaddingY + titleHeight, textWidth,
            CardRunLineHeight(hdc), descriptionRuns);""",
)

# ── 3. La pastille elle-meme ────────────────────────────────────────────────
replace(
    "DrawBadge mesure",
    """        int badgeW = S(34);
        int badgeH = S(24);""",
    """        int badgeW = BadgeWidth(hdc);
        int badgeH = BadgeHeight(hdc);""",
)

# ── 4. Les parametres de plancher n'ont plus d'objet ───────────────────────
replace(
    "signature de DrawStepCard",
    """    private void DrawStepCard(IntPtr hdc, int margin, int cw, ref int y, string number, string title, string description, int minCardHeight = STEP_CARD_MIN_H)""",
    """    private void DrawStepCard(IntPtr hdc, int margin, int cw, ref int y, string number, string title, string description)""",
)

replace(
    "signature de DrawStepCardWithRuns",
    """    private void DrawStepCardWithRuns(IntPtr hdc, int margin, int cw, ref int y, string number, string title,
        int minCardHeight, params (string Text, uint Color, IntPtr Font)[] descriptionRuns)""",
    """    private void DrawStepCardWithRuns(IntPtr hdc, int margin, int cw, ref int y, string number, string title,
        params (string Text, uint Color, IntPtr Font)[] descriptionRuns)""",
)

replace(
    "surcharge de transfert supprimee",
    """    private void DrawStepCardWithRuns(IntPtr hdc, int margin, int cw, ref int y, string number, string title,
        params (string Text, uint Color, IntPtr Font)[] descriptionRuns)
    {
        DrawStepCardWithRuns(hdc, margin, cw, ref y, number, title, STEP_CARD_MIN_H, descriptionRuns);
    }

""",
    """    // La surcharge de transfert qui vivait ici ne servait qu'à poser le plancher par défaut.
    // Sans plancher, elle a la signature de celle qu'elle appelait.
""",
)

replace(
    "DrawFeatureWithHighlight n'a plus de plancher a passer",
    """        DrawStepCardWithRuns(hdc, margin, cw, ref y, number, title, FEATURE_CARD_MIN_H, GetStyledDescriptionRuns(number, description));""",
    """        DrawStepCardWithRuns(hdc, margin, cw, ref y, number, title, GetStyledDescriptionRuns(number, description));""",
)

replace(
    "DrawFeature n'a plus de plancher a passer",
    """    private void DrawFeature(IntPtr hdc, int margin, int cw, ref int y, string number, string title, string description)
    {
        DrawStepCard(hdc, margin, cw, ref y, number, title, description, FEATURE_CARD_MIN_H);
    }""",
    """    private void DrawFeature(IntPtr hdc, int margin, int cw, ref int y, string number, string title, string description)
    {
        DrawStepCard(hdc, margin, cw, ref y, number, title, description);
    }""",
)

# ── Verification ────────────────────────────────────────────────────────────
for line in text.splitlines():
    stripped = line.lstrip()
    if stripped.startswith("//"):
        continue
    for dead in ("STEP_CARD_MIN_H", "FEATURE_CARD_MIN_H", "minCardHeight"):
        if dead in line:
            sys.exit(f"{dead} subsiste hors commentaire :\n{line}")

data = text.encode("utf-8")
lf_after = data.count(b"\n") - data.count(b"\r\n")
TARGET.write_bytes(data)
print(f"\n{TARGET.name} écrit — LF isolés {LF_BEFORE} → {lf_after}")
