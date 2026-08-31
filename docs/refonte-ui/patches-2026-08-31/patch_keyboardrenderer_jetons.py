"""CH4 — `KeyboardRenderer` passe aux jetons de la charte.

Les 14 constantes `CLR_` du renderer disparaissent au profit de `KeyboardTheme` et de la palette
courante. Rien d'autre ne change : la geometrie, les profils, le filtrage par lecon et les cinq
polices recues des appelants sont intacts. Onze de ces quatorze valeurs sont identiques au bit
pres a celles de `LearningModule`, trace d'un copier-coller — c'est ce doublon que le chantier
supprime.

Quatre points de traduction ne sortent pas de la charte et sont a arbitrer par Antoine ; ils sont
implementes avec la valeur la plus defendable et signales dans l'audit :

1. fond d'une touche contextuelle au repos -> `papier` (elle etait plus sombre que les autres) ;
2. barre du verrou majuscule -> `sur-action` (une marque sur un fond d'accent) ;
3. glyphe d'une touche morte -> aucun jeton propre, il suit le rang de sa couche ;
4. ligne d'etat de la touche morte armee -> `action`.

⚠️ `KeyboardRenderer.cs` porte 820 CRLF et 42 LF. Ce script patche en octets : les ancres d'une
seule ligne n'emportent aucune fin de ligne, et les regions multi-lignes rendent la leur apres
avoir verifie qu'elle est uniforme. Aucune normalisation, jamais.
"""

import pathlib
import sys

sys.stdout.reconfigure(encoding="utf-8")

CIBLE = pathlib.Path(__file__).resolve().parents[3] / "src" / "KeyboardRenderer.cs"


def region(brut, premiere, derniere, lignes):
    """Remplace la region qui va de `premiere` a `derniere`, bornes comprises."""
    a = brut.encode("utf-8") if isinstance(brut, str) else brut
    p = premiere.encode("utf-8")
    d = derniere.encode("utf-8")

    debut = a.find(p)
    if debut < 0 or a.count(p) != 1:
        sys.exit(f"ancre de debut trouvee {a.count(p)} fois :\n{premiere}")
    # L'ancre de fin est cherchee a partir du debut de la region : « };  » n'est unique nulle
    # part dans un fichier de code, et l'exiger unique rendrait la fonction inutilisable.
    fin_ancre = a.find(d, debut)
    if fin_ancre < 0:
        sys.exit(f"ancre de fin introuvable apres le debut :\n{derniere}")
    fin = fin_ancre + len(d)

    zone = a[debut:fin]
    crlf = zone.count(b"\r\n")
    lf = zone.count(b"\n") - crlf
    if crlf and lf:
        sys.exit(f"region mixte ({crlf} CRLF, {lf} LF), refus d'ecrire :\n{premiere}")
    saut = b"\r\n" if crlf else b"\n"

    remplacement = saut.join(l.encode("utf-8") for l in lignes)
    return a[:debut] + remplacement + a[fin:], crlf, lf


def ligne(brut, avant, apres):
    """Remplace le contenu d'une ligne, sans toucher a sa fin de ligne."""
    a = brut.encode("utf-8") if isinstance(brut, str) else brut
    p = avant.encode("utf-8")
    if a.count(p) != 1:
        sys.exit(f"ancre trouvee {a.count(p)} fois, attendu 1 :\n{avant}")
    return a.replace(p, apres.encode("utf-8"))


brut = CIBLE.read_bytes()
crlf_avant = brut.count(b"\r\n")
lf_avant = brut.count(b"\n") - crlf_avant

# 1. Les quatorze constantes s'en vont.
brut, _, _ = region(
    brut,
    "    private const uint CLR_KEY = 0x003A3A3A;",
    "    private const uint CLR_CAPS_BAR = 0x0000A5FF;",
    [
        "    // Aucune couleur ici : elles sortent de KeyboardTheme et de la palette courante,",
        "    // depuis le chantier CH4. Les quatorze constantes CLR_ qui vivaient a cet endroit",
        "    // portaient onze valeurs identiques au bit pres a celles de LearningModule.",
    ],
)

# 2. L'etat de la touche remplace la cascade de couleurs.
brut, _, _ = region(
    brut,
    "        uint fill = key.IsContextual ? CLR_KEY_CONTEXT : CLR_KEY;",
    "            fill = CLR_MOD_ACTIVE;",
    [
        "        KeyState keyState =",
        "            disabledBackspace ? KeyState.Disabled :",
        "            pressed ? KeyState.Pressed :",
        "            modifierActive ? KeyState.ModifierActive :",
        "            highlighted ? KeyState.Pressed :",
        "            KeyState.Rest;",
        "        var palette = Theme.Current;",
        "        var paint = KeyboardTheme.Paint(keyState, palette);",
        "",
        "        // Une touche contextuelle au repos s'enfonce d'un cran — fond de fenetre plutot",
        "        // que surface. C'est le seul ecart a la table d'etats, et il tient la place du",
        "        // CLR_KEY_CONTEXT d'avant, plus sombre que CLR_KEY pour cette meme raison.",
        "        uint fill = keyState == KeyState.Rest && key.IsContextual ? palette.Paper : paint.Fill;",
        "        uint border = paint.Border;",
        "        int borderWidth = paint.BorderWidth;",
    ],
)

# 3. La barre du verrou majuscule est une marque posee sur le fond d'accent de la touche.
brut = ligne(
    brut,
    "            var barBrush = Win32.CreateSolidBrush(CLR_CAPS_BAR);",
    "            var barBrush = Win32.CreateSolidBrush(paint.Label);",
)

# 4. Le libelle d'une touche contextuelle recoit sa couleur, au lieu de la deduire d'un booleen :
#    un modifieur actif porte un fond d'accent, sur lequel l'encre ne tiendrait pas.
brut = ligne(
    brut,
    "            DrawContextKeyLabel(hdc, rect, key, isoEnter, disabledBackspace, hFontContext);",
    "            DrawContextKeyLabel(hdc, rect, key, isoEnter, paint.Label, hFontContext);",
)
brut = ligne(brut, "        bool disabled,", "        uint textColor,")
brut = ligne(
    brut,
    "        Win32.SetTextColor(hdc, disabled ? 0x00606060u : CLR_CTX_TEXT);",
    "        Win32.SetTextColor(hdc, textColor);",
)

# 5. Caractere et libelle sous une touche morte armee.
brut = ligne(
    brut,
    "            Win32.SetTextColor(hdc, CLR_CTX_TEXT);",
    "            Win32.SetTextColor(hdc, Theme.Current.Ink);",
)
brut = ligne(
    brut,
    "        Win32.SetTextColor(hdc, CLR_KEY_LABEL);",
    "        Win32.SetTextColor(hdc, Theme.Current.TextSecondary);",
)
brut = ligne(
    brut,
    "        Win32.SetTextColor(hdc, CLR_DK_ACTIVE_TEXT);",
    "        Win32.SetTextColor(hdc, Theme.Current.Action);",
)

# 6. Deux jetons de texte la ou l'application en employait quatre.
brut, _, _ = region(
    brut,
    "        uint color = (isDeadKey, isActive) switch",
    "        };",
    [
        "        // Deux jetons de texte seulement, la ou l'application en employait quatre :",
        "        // l'encre pour la couche que la frappe produira, texte-2 pour les autres. Une",
        "        // touche morte garde ce rang et se signale par son cercle pointille, pas par une",
        "        // couleur de plus — la charte n'en offre aucune, et l'inventer est interdit.",
        "        uint color = isActive ? Theme.Current.Ink : Theme.Current.TextSecondary;",
    ],
)

restants = brut.count(b"CLR_")
crlf_apres = brut.count(b"\r\n")
lf_apres = brut.count(b"\n") - crlf_apres

CIBLE.write_bytes(brut)
print(f"{CIBLE.name} : CLR_ restants {restants}")
print(f"  CRLF {crlf_avant} -> {crlf_apres}   LF {lf_avant} -> {lf_apres}")
