"""Compare deux jeux de captures du banc de fenêtres, pixel par pixel.

    python scripts/compare-captures.py --avant A1 [A2] --apres B1 [B2] [--diff DOSSIER] [--json FICHIER]
    python scripts/compare-captures.py --avant A1 A2 [--diff DOSSIER]

Chaque dossier est une passe du banc (`CaptureBench` et `KeyboardContextBench`) : les PNG
y portent le même nom d'une passe à l'autre. Les images sont appariées par nom.

Le banc n'est pas déterministe : un curseur qui clignote, un focus qui arrive ou non, une
minuterie qui tombe d'un côté ou de l'autre de la capture changent quelques pixels d'une
passe à l'autre. Une seconde passe par côté sert à les reconnaître. Les pixels qui changent
entre les deux passes d'un même côté forment le masque de bruit ; une différence entre
avant et après n'est retenue que hors de ce masque.

Classement de chaque image :
- identique : avant et après ont les mêmes pixels ;
- différente : des pixels changent hors du bruit, ou la taille change ;
- instable : les seuls pixels qui changent sont dans le bruit, ou l'image elle-même change
  de taille d'une passe à l'autre ;
- manquante : l'image n'existe que d'un côté.

Avec `--avant A1 A2` seul, le script dit seulement quelles images sont stables entre les
deux passes et lesquelles ne le sont pas.

`--diff` écrit, pour chaque image différente ou instable, un PNG de l'image d'après
délavée, les pixels changés en magenta, le bruit en jaune.

Codes de sortie : 0 sans différence ni image manquante ; 1 sinon ; 2 pour une erreur
d'usage ou un PNG illisible.

Bibliothèque standard seule : le décodeur PNG ci-dessous lit les PNG non entrelacés, en
8 bits par canal (ceux qu'écrit GDI+), et convertit les autres formats courants en RVBA.
"""
import argparse
import hashlib
import json
import struct
import sys
import zlib
from dataclasses import dataclass, field
from pathlib import Path

for _flux in (sys.stdout, sys.stderr):
    _reconfigurer = getattr(_flux, "reconfigure", None)
    if _reconfigurer is not None:
        try:
            _reconfigurer(encoding="utf-8", errors="backslashreplace")
        except (OSError, ValueError):
            pass

SIGNATURE = b"\x89PNG\r\n\x1a\n"


class PngIllisible(Exception):
    """PNG absent, tronqué ou d'un format que ce décodeur ne lit pas."""


@dataclass
class Image:
    """Pixels décodés : `canaux` vaut 3 (RVB) ou 4 (RVBA), une ligne d'octets par rangée."""
    largeur: int
    hauteur: int
    canaux: int
    lignes: list


# ═══════════════════════════════════════════════════════════════
# Décodage
# ═══════════════════════════════════════════════════════════════

_MASQUES = {}


def _masques(n):
    """Masques 0x7F… et 0x80… de n octets, pour additionner octet à octet sans retenue."""
    if n not in _MASQUES:
        _MASQUES[n] = (int.from_bytes(b"\x7f" * n, "little"), int.from_bytes(b"\x80" * n, "little"))
    return _MASQUES[n]


def _ajouter_octets(a, b):
    """Somme octet par octet, modulo 256, calculée en un seul entier (filtre Up)."""
    n = len(a)
    bas, haut = _masques(n)
    x = int.from_bytes(a, "little")
    y = int.from_bytes(b, "little")
    return (((x & bas) + (y & bas)) ^ ((x ^ y) & haut)).to_bytes(n, "little")


def _defiltrer(type_filtre, ligne, precedente, bpp):
    """Annule le filtre PNG d'une rangée (spécification PNG, § 9)."""
    if type_filtre == 0:
        return bytes(ligne)
    if type_filtre == 2:
        return _ajouter_octets(ligne, precedente)
    sortie = bytearray(ligne)
    n = len(sortie)
    if type_filtre == 1:
        for i in range(bpp, n):
            sortie[i] = (sortie[i] + sortie[i - bpp]) & 0xFF
    elif type_filtre == 3:
        for i in range(n):
            gauche = sortie[i - bpp] if i >= bpp else 0
            sortie[i] = (sortie[i] + ((gauche + precedente[i]) >> 1)) & 0xFF
    elif type_filtre == 4:
        for i in range(n):
            a = sortie[i - bpp] if i >= bpp else 0
            b = precedente[i]
            c = precedente[i - bpp] if i >= bpp else 0
            p = a + b - c
            pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
            if pa <= pb and pa <= pc:
                predit = a
            elif pb <= pc:
                predit = b
            else:
                predit = c
            sortie[i] = (sortie[i] + predit) & 0xFF
    else:
        raise PngIllisible(f"filtre PNG inconnu : {type_filtre}")
    return bytes(sortie)


def _vers_rvba(brute, type_couleur, profondeur, largeur, palette, transparence):
    """Convertit une rangée défiltrée d'un format peu courant en RVBA 8 bits."""
    if profondeur == 16:
        brute = brute[::2]  # octet de poids fort de chaque échantillon
        profondeur = 8
    if type_couleur == 2:
        return b"".join(brute[i:i + 3] + b"\xff" for i in range(0, largeur * 3, 3))
    if type_couleur == 6:
        return bytes(brute[:largeur * 4])
    if type_couleur == 4:
        return b"".join(bytes((brute[i], brute[i], brute[i], brute[i + 1])) for i in range(0, largeur * 2, 2))
    # Gris (0) et palette (3), éventuellement sous 8 bits par pixel.
    valeurs = []
    if profondeur == 8:
        valeurs = list(brute[:largeur])
    else:
        par_octet = 8 // profondeur
        masque = (1 << profondeur) - 1
        for octet in brute:
            for k in range(par_octet):
                valeurs.append((octet >> (8 - profondeur * (k + 1))) & masque)
        valeurs = valeurs[:largeur]
    sortie = bytearray()
    for v in valeurs:
        if type_couleur == 3:
            if v * 3 + 2 >= len(palette):
                raise PngIllisible("indice hors de la palette")
            alpha = transparence[v] if v < len(transparence) else 255
            sortie += palette[v * 3:v * 3 + 3] + bytes((alpha,))
        else:
            gris = v * 255 // ((1 << profondeur) - 1)
            sortie += bytes((gris, gris, gris, 255))
    return bytes(sortie)


def lire_png(chemin):
    """Décode un PNG non entrelacé. RVB et RVBA 8 bits restent tels quels ; le reste passe en RVBA."""
    donnees = Path(chemin).read_bytes()
    if not donnees.startswith(SIGNATURE):
        raise PngIllisible(f"{chemin} : signature PNG absente")
    position = len(SIGNATURE)
    entete = None
    idat = bytearray()
    palette = b""
    transparence = b""
    while position + 8 <= len(donnees):
        longueur, genre = struct.unpack(">I4s", donnees[position:position + 8])
        corps = donnees[position + 8:position + 8 + longueur]
        if len(corps) != longueur:
            raise PngIllisible(f"{chemin} : bloc {genre!r} tronqué")
        position += 12 + longueur
        if genre == b"IHDR":
            entete = struct.unpack(">IIBBBBB", corps)
        elif genre == b"PLTE":
            palette = corps
        elif genre == b"tRNS":
            transparence = corps
        elif genre == b"IDAT":
            idat += corps
        elif genre == b"IEND":
            break
    if entete is None:
        raise PngIllisible(f"{chemin} : IHDR absent")
    largeur, hauteur, profondeur, type_couleur, _, _, entrelacement = entete
    if entrelacement != 0:
        raise PngIllisible(f"{chemin} : PNG entrelacé, non pris en charge")
    canaux_par_type = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}
    if type_couleur not in canaux_par_type or profondeur not in (1, 2, 4, 8, 16):
        raise PngIllisible(f"{chemin} : format non pris en charge (type {type_couleur}, {profondeur} bits)")
    bits_par_pixel = canaux_par_type[type_couleur] * profondeur
    bpp = max(1, bits_par_pixel // 8)
    pas = (largeur * bits_par_pixel + 7) // 8
    try:
        brut = zlib.decompress(bytes(idat))
    except zlib.error as erreur:
        raise PngIllisible(f"{chemin} : IDAT illisible ({erreur})") from erreur
    if len(brut) < hauteur * (pas + 1):
        raise PngIllisible(f"{chemin} : données d'image tronquées")

    natif = profondeur == 8 and type_couleur in (2, 6)
    canaux = canaux_par_type[type_couleur] if natif else 4
    lignes = []
    precedente = bytes(pas)
    for y in range(hauteur):
        debut = y * (pas + 1)
        ligne = _defiltrer(brut[debut], brut[debut + 1:debut + 1 + pas], precedente, bpp)
        precedente = ligne
        lignes.append(ligne if natif else _vers_rvba(ligne, type_couleur, profondeur, largeur, palette, transparence))
    return Image(largeur, hauteur, canaux, lignes)


def _en_rvba(image):
    """Même image, en quatre canaux : pour comparer un RVB à un RVBA."""
    if image.canaux == 4:
        return image
    lignes = [b"".join(l[i:i + 3] + b"\xff" for i in range(0, len(l), 3)) for l in image.lignes]
    return Image(image.largeur, image.hauteur, 4, lignes)


# ═══════════════════════════════════════════════════════════════
# Écriture
# ═══════════════════════════════════════════════════════════════

def _bloc(genre, corps):
    return struct.pack(">I", len(corps)) + genre + corps + struct.pack(">I", zlib.crc32(genre + corps) & 0xFFFFFFFF)


def ecrire_png(chemin, image):
    """Écrit un PNG RVB ou RVBA 8 bits, sans filtre."""
    type_couleur = 6 if image.canaux == 4 else 2
    entete = struct.pack(">IIBBBBB", image.largeur, image.hauteur, 8, type_couleur, 0, 0, 0)
    brut = b"".join(b"\x00" + ligne for ligne in image.lignes)
    Path(chemin).write_bytes(SIGNATURE + _bloc(b"IHDR", entete) + _bloc(b"IDAT", zlib.compress(brut, 6)) + _bloc(b"IEND", b""))


# ═══════════════════════════════════════════════════════════════
# Comparaison
# ═══════════════════════════════════════════════════════════════

def pixels_differents(a, b):
    """Positions (x, y) des pixels qui diffèrent entre deux images de même taille."""
    if a.canaux != b.canaux:
        a, b = _en_rvba(a), _en_rvba(b)
    c = a.canaux
    positions = set()
    for y, (la, lb) in enumerate(zip(a.lignes, b.lignes)):
        if la == lb:
            continue
        for x in range(a.largeur):
            i = x * c
            if la[i:i + c] != lb[i:i + c]:
                positions.add((x, y))
    return positions


def boite(positions):
    """Rectangle englobant, bornes incluses : (x0, y0, x1, y1)."""
    xs = [p[0] for p in positions]
    ys = [p[1] for p in positions]
    return (min(xs), min(ys), max(xs), max(ys))


def image_de_difference(base, changes, bruit):
    """L'image délavée, les pixels changés en magenta, le bruit en jaune."""
    delave = bytes(160 + v * 95 // 255 for v in range(256))
    c = base.canaux
    lignes = [bytearray(l.translate(delave)) for l in base.lignes]
    for couleur, positions in (((255, 200, 0), bruit), ((255, 0, 255), changes)):
        for x, y in positions:
            lignes[y][x * c:x * c + 3] = bytes(couleur)
            if c == 4:
                lignes[y][x * c + 3] = 255
    return Image(base.largeur, base.hauteur, c, [bytes(l) for l in lignes])


@dataclass
class Resultat:
    nom: str
    statut: str  # identique, differente, instable, manquante, stable
    detail: str = ""
    pixels: int = 0
    bruit: int = 0
    boite: tuple = None
    taille_avant: tuple = None
    taille_apres: tuple = None
    diff: str = None
    notes: list = field(default_factory=list)


class Jeu:
    """Une passe : ses PNG par nom, décodés à la demande et gardés en cache."""

    def __init__(self, dossier):
        self.dossier = Path(dossier)
        if not self.dossier.is_dir():
            raise PngIllisible(f"{dossier} : dossier introuvable")
        self.noms = {p.name for p in self.dossier.glob("*.png")}
        self._images = {}
        self._empreintes = {}

    def empreinte(self, nom):
        if nom not in self._empreintes:
            self._empreintes[nom] = hashlib.sha256((self.dossier / nom).read_bytes()).hexdigest()
        return self._empreintes[nom]

    def image(self, nom):
        if nom not in self._images:
            self._images[nom] = lire_png(self.dossier / nom)
        return self._images[nom]


def _ecarts(jeu1, jeu2, nom):
    """Pixels qui changent entre deux passes : None si la taille change."""
    if jeu1.empreinte(nom) == jeu2.empreinte(nom):
        return set()
    a, b = jeu1.image(nom), jeu2.image(nom)
    if (a.largeur, a.hauteur) != (b.largeur, b.hauteur):
        return None
    return pixels_differents(a, b)


def _taille(jeu, nom):
    image = jeu.image(nom)
    return (image.largeur, image.hauteur)


def stabilite(passe1, passe2, dossier_diff=None):
    """Deux passes d'un même côté : quelles images changent d'une passe à l'autre."""
    resultats = []
    for nom in sorted(passe1.noms | passe2.noms):
        if nom not in passe1.noms or nom not in passe2.noms:
            absente = "passe 1" if nom not in passe1.noms else "passe 2"
            resultats.append(Resultat(nom, "manquante", f"absente de la {absente}"))
            continue
        ecarts = _ecarts(passe1, passe2, nom)
        if ecarts is None:
            resultats.append(Resultat(nom, "instable", "la taille change d'une passe à l'autre",
                                      taille_avant=_taille(passe1, nom), taille_apres=_taille(passe2, nom)))
        elif ecarts:
            r = Resultat(nom, "instable", pixels=len(ecarts), bruit=len(ecarts), boite=boite(ecarts))
            if dossier_diff is not None:
                r.diff = str(dossier_diff / nom)
                ecrire_png(r.diff, image_de_difference(passe2.image(nom), set(), ecarts))
            resultats.append(r)
        else:
            resultats.append(Resultat(nom, "stable"))
    return resultats


def comparer(avant, apres, avant2=None, apres2=None, dossier_diff=None):
    """Avant contre après, le bruit de chaque côté retiré quand une seconde passe est donnée."""
    resultats = []
    for nom in sorted(avant.noms | apres.noms):
        if nom not in avant.noms or nom not in apres.noms:
            resultats.append(Resultat(nom, "manquante", "absente avant" if nom not in avant.noms else "absente après"))
            continue

        bruit = set()
        taille_instable = []
        for cote, p1, p2 in (("avant", avant, avant2), ("après", apres, apres2)):
            if p2 is None:
                continue
            if nom not in p2.noms:
                taille_instable.append(f"absente de la seconde passe {cote}")
                continue
            ecarts = _ecarts(p1, p2, nom)
            if ecarts is None:
                taille_instable.append(f"taille instable {cote}")
            else:
                bruit |= ecarts
        if taille_instable:
            resultats.append(Resultat(nom, "instable", ", ".join(taille_instable)))
            continue

        if avant.empreinte(nom) == apres.empreinte(nom):
            r = Resultat(nom, "identique", bruit=len(bruit))
            if bruit:
                r.notes.append(f"{len(bruit)} px de bruit dans une passe")
            resultats.append(r)
            continue

        a, b = avant.image(nom), apres.image(nom)
        if (a.largeur, a.hauteur) != (b.largeur, b.hauteur):
            resultats.append(Resultat(nom, "differente", "la taille change",
                                      taille_avant=(a.largeur, a.hauteur), taille_apres=(b.largeur, b.hauteur)))
            continue

        ecarts = pixels_differents(a, b)
        reels = ecarts - bruit
        if not ecarts:
            resultats.append(Resultat(nom, "identique", "pixels identiques, fichiers différents", bruit=len(bruit)))
            continue
        if not reels:
            r = Resultat(nom, "instable", "les pixels changés sont tous dans le bruit",
                         pixels=len(ecarts), bruit=len(bruit), boite=boite(ecarts))
        else:
            r = Resultat(nom, "differente", pixels=len(reels), bruit=len(bruit), boite=boite(reels),
                         taille_avant=(a.largeur, a.hauteur))
            if bruit:
                r.notes.append(f"{len(bruit)} px de bruit écartés")
        if dossier_diff is not None:
            r.diff = str(dossier_diff / nom)
            ecrire_png(r.diff, image_de_difference(b, reels, bruit & ecarts))
        resultats.append(r)
    return resultats


# ═══════════════════════════════════════════════════════════════
# Rapport
# ═══════════════════════════════════════════════════════════════

TITRES = {
    "differente": "Différentes",
    "instable": "Instables",
    "manquante": "Manquantes",
    "identique": "Identiques",
    "stable": "Stables",
}


def _ligne(r):
    morceaux = []
    if r.pixels and r.taille_avant:
        total = r.taille_avant[0] * r.taille_avant[1]
        morceaux.append(f"{r.pixels} px ({100 * r.pixels / total:.3f} %)")
    elif r.pixels:
        morceaux.append(f"{r.pixels} px")
    if r.boite:
        morceaux.append("boîte {}×{} en ({}, {})".format(r.boite[2] - r.boite[0] + 1, r.boite[3] - r.boite[1] + 1,
                                                        r.boite[0], r.boite[1]))
    if r.taille_avant and r.taille_apres:
        morceaux.append(f"{r.taille_avant[0]}×{r.taille_avant[1]} → {r.taille_apres[0]}×{r.taille_apres[1]}")
    if r.detail:
        morceaux.append(r.detail)
    morceaux.extend(r.notes)
    return f"  {r.nom} : " + ", ".join(morceaux) if morceaux else f"  {r.nom}"


def rapport(resultats, entete):
    lignes = [entete]
    for statut in ("differente", "instable", "manquante", "identique", "stable"):
        groupe = [r for r in resultats if r.statut == statut]
        if not groupe and statut in ("stable", "identique"):
            continue
        lignes.append(f"{TITRES[statut]} : {len(groupe)}")
        if statut not in ("identique", "stable"):
            lignes.extend(_ligne(r) for r in groupe)
    return "\n".join(lignes)


def main(argv=None):
    parseur = argparse.ArgumentParser(description="Compare deux jeux de captures du banc, pixel par pixel.")
    parseur.add_argument("--avant", nargs="+", required=True, metavar="DOSSIER",
                         help="une ou deux passes d'avant")
    parseur.add_argument("--apres", nargs="+", metavar="DOSSIER", help="une ou deux passes d'après")
    parseur.add_argument("--diff", metavar="DOSSIER", help="où écrire les PNG de différence")
    parseur.add_argument("--json", metavar="FICHIER", help="résultats détaillés en JSON")
    args = parseur.parse_args(argv)

    if len(args.avant) > 2 or (args.apres and len(args.apres) > 2):
        parseur.error("au plus deux passes par côté")
    if not args.apres and len(args.avant) != 2:
        parseur.error("sans --apres, --avant attend deux passes")

    dossier_diff = None
    if args.diff:
        dossier_diff = Path(args.diff)
        dossier_diff.mkdir(parents=True, exist_ok=True)

    try:
        avant = [Jeu(d) for d in args.avant]
        if args.apres:
            apres = [Jeu(d) for d in args.apres]
            resultats = comparer(avant[0], apres[0], avant[1] if len(avant) > 1 else None,
                                 apres[1] if len(apres) > 1 else None, dossier_diff)
            entete = f"Avant : {' + '.join(args.avant)}\nAprès : {' + '.join(args.apres)}"
            if len(avant) == 1 or len(apres) == 1:
                entete += "\n⚠️ Une seule passe d'un côté : le bruit de ce côté n'est pas écarté."
        else:
            resultats = stabilite(avant[0], avant[1], dossier_diff)
            entete = f"Stabilité entre deux passes : {args.avant[0]} et {args.avant[1]}"
    except PngIllisible as erreur:
        print(f"Erreur : {erreur}", file=sys.stderr)
        return 2

    print(rapport(resultats, entete))
    if args.json:
        Path(args.json).write_text(json.dumps([r.__dict__ for r in resultats], ensure_ascii=False, indent=2) + "\n",
                                   encoding="utf-8")
    probleme = any(r.statut in ("differente", "manquante") for r in resultats)
    if not args.apres:
        probleme = any(r.statut in ("instable", "manquante") for r in resultats)
    return 1 if probleme else 0


if __name__ == "__main__":
    sys.exit(main())
