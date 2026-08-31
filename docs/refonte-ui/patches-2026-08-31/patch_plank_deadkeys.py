"""La nappe de clavier affichait l'identifiant d'une touche morte au lieu d'un glyphe.

Défaut mesuré le 2026-08-31 sur `clavier-charte-sombre.png` : deux touches rendaient « rcu » et
« _acu », soit `dk_circumflex` et `dk_acute` centrés puis rognés par la largeur de la touche. Le
cas était traité pour la couche AltGr et oublié pour la couche de base.

Patch en octets, ancre comptée, fins de ligne préservées.
"""

import pathlib
import sys

sys.stdout.reconfigure(encoding="utf-8")

CIBLE = pathlib.Path(__file__).resolve().parents[3] / "src" / "AZERTYGlobal.Tests" / "KeyboardPlank.cs"

AVANT = """            string main = def.Base ?? key.Label;
            string? sub = def.AltGr;
            if (sub != null && sub.StartsWith("dk_", StringComparison.Ordinal))
                sub = "◌";
"""

APRES = """            string main = Glyphe(def.Base) ?? key.Label;
            string? sub = Glyphe(def.AltGr);
"""

AVANT_METHODE = """    private static void Render(Layout layout, Palette palette, string file)
"""

APRES_METHODE = """    /// <summary>
    /// Ce qu'une couche donne à voir sur une touche. Une référence de touche morte —
    /// <c>dk_circumflex</c> — n'est pas un caractère : rendue telle quelle elle sort en
    /// identifiant rogné par la largeur de la touche. Le cercle pointillé est la convention que
    /// l'application emploie déjà pour une diacritique isolée.
    /// </summary>
    private static string? Glyphe(string? valeur)
    {
        if (string.IsNullOrEmpty(valeur))
            return null;
        return valeur.StartsWith("dk_", StringComparison.Ordinal) ? "◌" : valeur;
    }

    private static void Render(Layout layout, Palette palette, string file)
"""

brut = CIBLE.read_bytes()
crlf = brut.count(b"\r\n")
if crlf:
    sys.exit(f"{CIBLE.name} : {crlf} CRLF, ce patch n'ecrit qu'en LF pur")

texte = brut.decode("utf-8")
for avant, apres in ((AVANT, APRES), (AVANT_METHODE, APRES_METHODE)):
    n = texte.count(avant)
    if n != 1:
        sys.exit(f"{CIBLE.name} : ancre trouvee {n} fois, attendu 1\n---\n{avant}")
    texte = texte.replace(avant, apres)

CIBLE.write_bytes(texte.encode("utf-8"))
print(f"{CIBLE.name} : 2 ancres, {texte.count(chr(10))} LF")
