"""Témoins du comparateur de captures du banc de fenêtres.

    python -m unittest discover -s scripts/tests -v

Le comparateur tient lieu de preuve visuelle : il dit « identique » là où Antoine ne
regardera pas. Un décodeur PNG qui se trompe d'un filtre, ou un masque de bruit qui avale
une vraie différence, rendrait un faux « identique ». Les témoins ci-dessous vérifient
d'abord le décodage des cinq filtres PNG, puis le classement : une différence stable est
retenue, un pixel qui bouge déjà entre deux passes du même côté ne l'est pas, et une
différence hors du bruit reste retenue même quand du bruit l'accompagne.
"""
import importlib.util
import shutil
import struct
import tempfile
import unittest
import zlib
from contextlib import redirect_stderr, redirect_stdout
from io import StringIO
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent

spec = importlib.util.spec_from_file_location("compare_captures", ROOT / "scripts" / "compare-captures.py")
cc = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cc)

LARGEUR = 12
HAUTEUR = 9


def image_de_base(canaux=3):
    """Un dégradé : chaque pixel est distinct de ses voisins, les filtres ont du travail."""
    lignes = []
    for y in range(HAUTEUR):
        ligne = bytearray()
        for x in range(LARGEUR):
            pixel = [(x * 23 + y * 7) % 256, (x * 5 + y * 41) % 256, (x * y * 3 + 17) % 256]
            if canaux == 4:
                pixel.append(255 - (x + y) % 3)
            ligne += bytes(pixel)
        lignes.append(bytes(ligne))
    return cc.Image(LARGEUR, HAUTEUR, canaux, lignes)


def avec_pixel(image, x, y, couleur):
    lignes = [bytearray(l) for l in image.lignes]
    c = image.canaux
    lignes[y][x * c:x * c + 3] = bytes(couleur)
    return cc.Image(image.largeur, image.hauteur, c, [bytes(l) for l in lignes])


def filtrer(type_filtre, ligne, precedente, bpp):
    """Encodeur de référence, écrit d'après la spécification PNG et indépendant du décodeur."""
    sortie = bytearray()
    for i, octet in enumerate(ligne):
        a = ligne[i - bpp] if i >= bpp else 0
        b = precedente[i]
        c = precedente[i - bpp] if i >= bpp else 0
        if type_filtre == 0:
            predit = 0
        elif type_filtre == 1:
            predit = a
        elif type_filtre == 2:
            predit = b
        elif type_filtre == 3:
            predit = (a + b) // 2
        else:
            p = a + b - c
            pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
            predit = a if pa <= pb and pa <= pc else (b if pb <= pc else c)
        sortie.append((octet - predit) % 256)
    return bytes(sortie)


def png_filtre(chemin, image, types):
    """Écrit un PNG dont chaque rangée porte le filtre donné, en boucle sur `types`."""
    type_couleur = 6 if image.canaux == 4 else 2
    precedente = bytes(len(image.lignes[0]))
    brut = bytearray()
    for y, ligne in enumerate(image.lignes):
        t = types[y % len(types)]
        brut += bytes((t,)) + filtrer(t, ligne, precedente, image.canaux)
        precedente = ligne
    entete = struct.pack(">IIBBBBB", image.largeur, image.hauteur, 8, type_couleur, 0, 0, 0)
    Path(chemin).write_bytes(cc.SIGNATURE + cc._bloc(b"IHDR", entete)
                             + cc._bloc(b"IDAT", zlib.compress(bytes(brut))) + cc._bloc(b"IEND", b""))


class DecodagePng(unittest.TestCase):
    def setUp(self):
        self.dossier = Path(tempfile.mkdtemp(prefix="cmpcap_"))

    def tearDown(self):
        shutil.rmtree(self.dossier, ignore_errors=True)

    def test_aller_retour_sans_filtre(self):
        image = image_de_base()
        cc.ecrire_png(self.dossier / "a.png", image)
        relue = cc.lire_png(self.dossier / "a.png")
        self.assertEqual((relue.largeur, relue.hauteur, relue.canaux), (LARGEUR, HAUTEUR, 3))
        self.assertEqual(relue.lignes, image.lignes)

    def test_les_cinq_filtres_en_rvb_et_en_rvba(self):
        for canaux in (3, 4):
            for types in ([0], [1], [2], [3], [4], [0, 1, 2, 3, 4]):
                with self.subTest(canaux=canaux, filtres=types):
                    image = image_de_base(canaux)
                    chemin = self.dossier / f"f{canaux}-{''.join(map(str, types))}.png"
                    png_filtre(chemin, image, types)
                    self.assertEqual(cc.lire_png(chemin).lignes, image.lignes)

    def test_paeth_departage_haut_avant_haut_gauche(self):
        # Gauche 5, haut 20, haut-gauche 10 : p = 15, écarts 10 / 5 / 5. À égalité entre haut
        # et haut-gauche, la spécification retient le haut. Un dégradé ne tombe jamais sur ce cas.
        image = cc.Image(2, 2, 3, [bytes((10,) * 3 + (20,) * 3), bytes((5,) * 3 + (77,) * 3)])
        chemin = self.dossier / "paeth.png"
        png_filtre(chemin, image, [4])
        self.assertEqual(cc.lire_png(chemin).lignes, image.lignes)

    def test_gris_passe_en_rvba(self):
        entete = struct.pack(">IIBBBBB", 2, 1, 8, 0, 0, 0, 0)
        chemin = self.dossier / "gris.png"
        chemin.write_bytes(cc.SIGNATURE + cc._bloc(b"IHDR", entete)
                           + cc._bloc(b"IDAT", zlib.compress(b"\x00\x10\xf0")) + cc._bloc(b"IEND", b""))
        image = cc.lire_png(chemin)
        self.assertEqual(image.canaux, 4)
        self.assertEqual(image.lignes[0], bytes((16, 16, 16, 255, 240, 240, 240, 255)))

    def test_png_entrelace_refuse(self):
        entete = struct.pack(">IIBBBBB", 1, 1, 8, 2, 0, 0, 1)
        chemin = self.dossier / "entrelace.png"
        chemin.write_bytes(cc.SIGNATURE + cc._bloc(b"IHDR", entete)
                           + cc._bloc(b"IDAT", zlib.compress(b"\x00\x00\x00\x00")) + cc._bloc(b"IEND", b""))
        with self.assertRaises(cc.PngIllisible):
            cc.lire_png(chemin)


class Classement(unittest.TestCase):
    """Quatre passes (avant 1 et 2, après 1 et 2), une image par cas."""

    def setUp(self):
        self.racine = Path(tempfile.mkdtemp(prefix="cmpcap_"))
        self.passes = {n: self.racine / n for n in ("a1", "a2", "b1", "b2")}
        for d in self.passes.values():
            d.mkdir()
        base = image_de_base()
        rouge = (255, 0, 0)
        cas = {
            # nom : (avant 1, avant 2, après 1, après 2)
            "identique.png": (base, base, base, base),
            "stable.png": (base, base, avec_pixel(base, 5, 5, rouge), avec_pixel(base, 5, 5, rouge)),
            "bruit.png": (base, avec_pixel(base, 2, 2, rouge), avec_pixel(base, 2, 2, rouge),
                          avec_pixel(base, 2, 2, rouge)),
            "bruit-et-vrai.png": (base, avec_pixel(base, 2, 2, rouge),
                                  avec_pixel(avec_pixel(base, 2, 2, rouge), 7, 7, rouge),
                                  avec_pixel(avec_pixel(base, 2, 2, rouge), 7, 7, rouge)),
        }
        for nom, images in cas.items():
            for passe, image in zip(("a1", "a2", "b1", "b2"), images):
                cc.ecrire_png(self.passes[passe] / nom, image)
        petite = cc.Image(3, 2, 3, [bytes(9), bytes(9)])
        for passe in ("a1", "a2"):
            cc.ecrire_png(self.passes[passe] / "taille.png", base)
        for passe in ("b1", "b2"):
            cc.ecrire_png(self.passes[passe] / "taille.png", petite)
        cc.ecrire_png(self.passes["a1"] / "seule.png", base)
        cc.ecrire_png(self.passes["a2"] / "seule.png", base)
        (self.racine / "diff").mkdir()

    def tearDown(self):
        shutil.rmtree(self.racine, ignore_errors=True)

    def resultats(self, quatre=True):
        j = {n: cc.Jeu(d) for n, d in self.passes.items()}
        liste = cc.comparer(j["a1"], j["b1"], j["a2"] if quatre else None, j["b2"] if quatre else None,
                            dossier_diff=self.racine / "diff")
        return {r.nom: r for r in liste}

    def test_classement_des_cas_simples(self):
        r = self.resultats()
        self.assertEqual(r["identique.png"].statut, "identique")
        self.assertEqual((r["stable.png"].statut, r["stable.png"].pixels), ("differente", 1))
        self.assertEqual(r["bruit.png"].statut, "instable")
        self.assertEqual((r["taille.png"].statut, r["taille.png"].detail), ("differente", "la taille change"))
        self.assertEqual((r["taille.png"].taille_avant, r["taille.png"].taille_apres), ((LARGEUR, HAUTEUR), (3, 2)))
        self.assertEqual(r["seule.png"].statut, "manquante")

    def test_une_vraie_difference_survit_au_bruit(self):
        r = self.resultats()["bruit-et-vrai.png"]
        self.assertEqual(r.statut, "differente")
        self.assertEqual(r.pixels, 1)
        self.assertEqual(r.boite, (7, 7, 7, 7))
        self.assertEqual(r.bruit, 1)

    def test_sans_seconde_passe_le_bruit_compte(self):
        r = self.resultats(quatre=False)
        self.assertEqual(r["bruit.png"].statut, "differente")

    def test_image_de_difference(self):
        r = self.resultats()["bruit-et-vrai.png"]
        diff = cc.lire_png(r.diff)
        self.assertEqual(diff.lignes[7][7 * 3:7 * 3 + 3], bytes((255, 0, 255)))
        self.assertEqual(diff.lignes[2][2 * 3:2 * 3 + 3], bytes((255, 200, 0)))

    def test_stabilite_de_deux_passes(self):
        liste = cc.stabilite(cc.Jeu(self.passes["a1"]), cc.Jeu(self.passes["a2"]))
        statuts = {r.nom: r.statut for r in liste}
        self.assertEqual(statuts["bruit.png"], "instable")
        self.assertEqual(statuts["bruit-et-vrai.png"], "instable")
        self.assertEqual(statuts["identique.png"], "stable")

    def test_codes_de_sortie(self):
        with redirect_stdout(StringIO()), redirect_stderr(StringIO()):
            self.assertEqual(cc.main(["--avant", str(self.passes["a1"]), str(self.passes["a2"]),
                                      "--apres", str(self.passes["b1"]), str(self.passes["b2"])]), 1)
            self.assertEqual(cc.main(["--avant", str(self.passes["a1"]),
                                      "--apres", str(self.passes["a1"])]), 0)
            self.assertEqual(cc.main(["--avant", str(self.passes["a1"]), str(self.racine / "absent"),
                                      "--apres", str(self.passes["b1"])]), 2)


if __name__ == "__main__":
    unittest.main()
