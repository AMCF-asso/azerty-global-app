"""Témoin de mutation du scanner de version périmée — lot F du plan v1.2.0.

    python docs/audit-v1.2.0/witness-lot-f-versions.py

Le scanner annonce « 0 erreur » sur les six documents de parc, et ses 33 tests passent. Ni
l'un ni l'autre ne prouve quoi que ce soit : un contrôle qui n'a jamais échoué n'est pas un
contrôle. C'est mesuré ici même — à sa première exécution, le 2026-08-21, le scanner
annonçait « conforme » sur cinq documents périmés, parce que son motif de version ne voyait
aucun nom de bundle. Ce script casse les fichiers réels d'une façon précise, relance le
contrôle concerné, et dit ce qui a été vu.

Deux familles de mutations, deux contrôleurs différents :

- cinq mutations de **documents**, dont le juge est le scanner lui-même : il doit passer du
  code 0 au code 1, c'est-à-dire produire une ERREUR et non une simple attente de bascule ;
- cinq mutations du **scanner**, dont le juge est la suite `scripts/tests/` : casser un garde
  doit faire rougir le test qui le couvre. Les deux mutations du motif de version rejouent les
  deux faux négatifs réellement observés.

Chaque fichier est sauvegardé en octets et réécrit tel quel à la fin, y compris si une
mutation lève. Le document du dépôt est en CRLF quand ceux du kit sont en LF : tout passe par
les octets, jamais par du texte réécrit.

Sortie attendue sur un dépôt sain : dix mutations, dix rouges, six fichiers restaurés à
l'identique.
"""
import hashlib
import subprocess
import sys
from pathlib import Path

for stream in (sys.stdout, sys.stderr):
    reconfigure = getattr(stream, "reconfigure", None)
    if reconfigure is not None:
        try:
            reconfigure(encoding="utf-8", errors="backslashreplace")
        except (OSError, ValueError):
            pass

ROOT = Path(__file__).resolve().parent.parent.parent
SCANNER = ROOT / "scripts" / "check-doc-versions.py"
LEGACY = ROOT.parent.parent / "sources" / "legacy" / "AZERTY Global" / "2026"
KIT = LEGACY / "Fichiers d'installation" / "Application AZERTY Global (Windows Store-MSIX)"

DEPOT_DISTRIB = ROOT / "Distribution Entreprises.md"
NOTE = LEGACY / "Pilotes" / "Note informatique.md"
DSI = KIT / "LISEZMOI-DSI.md"
SIGNATURE = KIT / "SIGNATURE.md"

TOUS = [SCANNER, DEPOT_DISTRIB, NOTE, DSI, SIGNATURE]


def lance(commande):
    """Retourne (code, dernière ligne utile) — la sortie complète ne sert pas ici."""
    resultat = subprocess.run(
        [sys.executable] + commande, cwd=ROOT,
        capture_output=True, text=True, encoding="utf-8", errors="replace")
    lignes = [l for l in (resultat.stdout + resultat.stderr).splitlines() if l.strip()]
    return resultat.returncode, (lignes[-1] if lignes else "")


def juge_scanner():
    return lance(["scripts/check-doc-versions.py"])


def juge_tests():
    return lance(["-m", "unittest", "discover", "-s", "scripts/tests"])


def remplace(chemin, ancien, nouveau, attendu=1):
    octets = chemin.read_bytes()
    a, n = ancien.encode("utf-8"), nouveau.encode("utf-8")
    vu = octets.count(a)
    if vu != attendu:
        raise AssertionError(f"{chemin.name} : « {ancien[:50]} » vu {vu} fois, attendu {attendu}")
    chemin.write_bytes(octets.replace(a, n, attendu))


# --- Mutations de documents : le scanner doit rougir -------------------------------------

def mute_bloc_retire():
    """Le bloc de suivi disparaît : le document n'est plus surveillé par rien."""
    remplace(DSI, "<!-- suivi-version\nversion-app: 1.2.0\n", "<!-- rien\n")


def mute_version_declaree_perimee():
    remplace(DEPOT_DISTRIB, "version-app: 1.2.0", "version-app: 1.1.0")


def mute_url_reversionnee():
    """Retour à l'URL versionnée que le lot E a écartée."""
    remplace(NOTE, "download.azerty.global/AZERTY_Global.msixbundle",
             "download.azerty.global/AZERTY_Global_1.1.0.msixbundle")


def mute_empreinte_fausse():
    """« en-attente » cède la place à une empreinte plausible mais fausse."""
    remplace(NOTE, "empreintes-attendues: en-attente",
             "empreintes-attendues: " + "0" * 64)


def mute_version_inconnue():
    remplace(SIGNATURE, "# Vérification du MSIX signé AMCF",
             "# Vérification du MSIX signé AMCF v0.9.8")


# --- Mutations du scanner : la suite de tests doit rougir --------------------------------

def mute_motif_avec_frontiere_de_mot():
    """Faux négatif n°1 du 2026-08-21 : `\\b` ne voit rien entre `_` et `1`."""
    remplace(SCANNER, r'r"(?<![\d.])\d+\.\d+\.\d+(?:\.\d+)?(?!\d)"',
             r'r"\b\d+\.\d+\.\d+(?:\.\d+)?\b"')


def mute_motif_refusant_le_point():
    """Faux négatif n°2 : la version d'un nom de fichier est suivie du point de l'extension."""
    remplace(SCANNER, r'(?:\.\d+)?(?!\d)"', r'(?:\.\d+)?(?![\d.])"')


def mute_bloc_scanne_lui_meme():
    remplace(SCANNER, "        if n in lignes_bloc:", "        if False:")


def mute_priorite_du_nom_de_bundle():
    """Sans cette priorité, un nom de fichier périmé se cache derrière la liste historique."""
    remplace(SCANNER, "            if vue in noms_bundle:", "            if False:")


def mute_attente_empreinte_ignoree():
    remplace(SCANNER, 'attente_empreinte = champs.get("empreintes-attendues", "") == EN_ATTENTE',
             "attente_empreinte = False")


MUTATIONS = (
    ("bloc de suivi retiré", mute_bloc_retire, juge_scanner),
    ("version-app périmée", mute_version_declaree_perimee, juge_scanner),
    ("URL de nouveau versionnée", mute_url_reversionnee, juge_scanner),
    ("empreinte fausse au lieu de en-attente", mute_empreinte_fausse, juge_scanner),
    ("version inconnue dans le corps", mute_version_inconnue, juge_scanner),
    ("motif de version avec \\b", mute_motif_avec_frontiere_de_mot, juge_tests),
    ("motif de version refusant le point", mute_motif_refusant_le_point, juge_tests),
    ("bloc de suivi scanné lui-même", mute_bloc_scanne_lui_meme, juge_tests),
    ("priorité du nom de bundle retirée", mute_priorite_du_nom_de_bundle, juge_tests),
    ("attente d'empreinte ignorée", mute_attente_empreinte_ignoree, juge_tests),
)


def main():
    absents = [c for c in TOUS if not c.is_file()]
    if absents:
        print("INCONCLUANT : fichier(s) introuvable(s) — les documents de parc vivent hors "
              "du dépôt public :")
        for c in absents:
            print(f"  {c}")
        return 2

    sauvegarde = {c: c.read_bytes() for c in TOUS}
    empreintes = {c: hashlib.sha256(o).hexdigest() for c, o in sauvegarde.items()}

    code, ligne = juge_scanner()
    print(f"État sain, scanner : code {code} — {ligne}")
    if code != 0:
        print("INCONCLUANT : le scanner rougit déjà avant toute mutation.")
        return 2
    code, ligne = juge_tests()
    print(f"État sain, tests   : code {code} — {ligne}")
    if code != 0:
        print("INCONCLUANT : la suite rougit déjà avant toute mutation.")
        return 2

    rouges = 0
    try:
        for libelle, muter, juge in MUTATIONS:
            muter()
            try:
                code, ligne = juge()
            finally:
                for chemin, octets in sauvegarde.items():
                    chemin.write_bytes(octets)
            vu = "ROUGE" if code != 0 else "VERT  <-- MUTATION NON VUE"
            if code != 0:
                rouges += 1
            print(f"  {vu:<26} {libelle}  (code {code} — {ligne[:70]})")
    finally:
        for chemin, octets in sauvegarde.items():
            chemin.write_bytes(octets)

    intacts = sum(1 for c in TOUS
                  if hashlib.sha256(c.read_bytes()).hexdigest() == empreintes[c])
    print(f"\n{len(MUTATIONS)} mutations, {rouges} rouge(s). "
          f"{intacts}/{len(TOUS)} fichier(s) restauré(s) à l'identique.")
    return 0 if rouges == len(MUTATIONS) and intacts == len(TOUS) else 1


if __name__ == "__main__":
    sys.exit(main())
