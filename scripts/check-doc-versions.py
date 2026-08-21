"""Scanner de version périmée des documents de parc — lot F du plan v1.2.0.

Décision du 2026-08-20 : la version et l'empreinte des documents remis à une DSI sont
gardées par un scanner, pas par une étape manuelle de la procédure de release. Motif
mesuré : `Pilotes/Note informatique.md` est resté en 1.1.0 du 2026-07-06 au 2026-08-21
sans que rien ne le signale, parce que le seul contrôle existant vit **dans**
`update_kit_version.py`, qui ne tourne qu'au moment où l'on bascule le kit.

    python scripts/check-doc-versions.py
    python scripts/check-doc-versions.py --release

Le mode par défaut échoue (code 1) sur ce qui est faux **aujourd'hui** et corrigeable
sans le bundle signé : bloc de suivi absent, version déclarée périmée, empreinte non
déclarée, URL de téléchargement portant un numéro de version. Il se contente de lister
ce qui ne peut être juste qu'après la bascule du kit — nom de bundle et empreintes de la
version précédente, empreinte annoncée « en-attente ». `--release` refuse aussi ces
attentes : un document dont l'empreinte est en attente n'est pas publiable.

Chaque document surveillé porte un bloc machine-lisible, invisible au rendu Markdown :

    <!-- suivi-version
    version-app: 1.2.0
    versions-historiques: 1.0.0, 1.1.0
    versions-en-bascule: 1.1.0.0
    empreintes-attendues: en-attente
    -->

`version-app` est ce que le document prétend décrire, et doit égaler la version du
csproj. Les versions du corps qui ne sont ni la courante ni déclarées sont signalées avec
leur ligne : c'est ce qui attrape un `1.1.0` oublié dans un lien, une commande ou un
tableau. Les empreintes du corps doivent être déclarées, ce qui attrape l'empreinte d'un
bundle qui n'est plus celui que le document décrit.

Les deux listes ne disent pas la même chose, et confondre les deux rend le scanner muet :

- `versions-historiques` — des faits datés et durables, « v1.1.0 publiée sur le Store le
  2026-07-23 ». Ils restent vrais après la prochaine release, donc silence.
- `versions-en-bascule` — ce que la bascule du kit remplacera, « **Version** : `1.1.0.0` ».
  Signalé en attente à chaque passage, jamais tu.

Un **nom de fichier** `AZERTY_Global_1.1.0.msixbundle` est toujours traité en attente, même
si sa version figure dans les historiques : il nomme le fichier réellement livré dans le
kit, pas un fait daté.

⚠️ Deux conventions cohabitent depuis le lot E et le scanner les traite différemment :
le **nom de fichier local** du bundle reste versionné, parce qu'il nomme un fichier
réellement livré dans le kit ZIP et basculé par `update_kit_version.py` ; l'**URL de
téléchargement** ne porte plus de version, décision du lot E, parce qu'App Installer
relit une URL stable pendant toute la vie de l'installation. Une URL versionnée est donc
une erreur du jour, un nom de fichier périmé une attente de bascule.

Les documents de parc vivent en partie hors du dépôt de l'app, dans
`sources/legacy/AZERTY Global/2026/`, et deux d'entre eux sont dans l'arbre du dépôt mais
exclus du dépôt public par `.gitignore`. Quand une de ces cibles manque, le scanner retourne
le code 2, « inconcluant », et jamais 0. Un scanner dont la panne est le silence ne garde rien.

⚠️ **C'est donc un contrôle de poste, pas un contrôle de CI.** Sur un clone du dépôt public,
aucun document de parc n'est présent et le scanner ne peut rien conclure : il le dit par un
code 2 au lieu de valider le vide. Il appartient à la séquence de release, jouée là où les
documents existent, et `Publication Microsoft Store.md` le liste en première étape.
"""

import argparse
import re
import sys
from pathlib import Path

for stream in (sys.stdout, sys.stderr):
    reconfigure = getattr(stream, "reconfigure", None)
    if reconfigure is not None:
        try:
            reconfigure(encoding="utf-8", errors="backslashreplace")
        except (AttributeError, OSError, ValueError):
            pass

REPO = Path(__file__).resolve().parent.parent
CSPROJ = REPO / "src" / "AZERTYGlobal.csproj"
LEGACY = REPO.parent.parent / "sources" / "legacy" / "AZERTY Global" / "2026"
KIT = LEGACY / "Fichiers d'installation" / "Application AZERTY Global (Windows Store-MSIX)"

# (chemin, absence tolérée). L'absence est tolérée pour tout document qui ne vit pas dans le
# dépôt public : ceux de `sources/legacy/`, et — mesuré le 2026-08-21 — `Distribution
# Entreprises.md`, qui est dans l'arbre du dépôt mais exclu par `.gitignore:39`, aux côtés de
# `Publication Microsoft Store.md` et `TO-DO.md`. Le marquer obligatoire rendrait le scanner
# inconcluant sur tout clone du dépôt public, où il n'existe pas.
DOCUMENTS = [
    (REPO / "Distribution Entreprises.md", True),
    (REPO / "entreprise" / "Note RGPD - Établissements.md", False),
    (LEGACY / "Microsoft Store" / "Distribution Entreprises.md", True),
    (LEGACY / "Pilotes" / "Note informatique.md", True),
    (KIT / "LISEZMOI-DSI.md", True),
    (KIT / "LISEZ-MOI.md", True),
    (KIT / "SIGNATURE.md", True),
]

DEBUT_BLOC = "<!-- suivi-version"
FIN_BLOC = "-->"
EN_ATTENTE = "en-attente"

# ⚠️ Deux gardes contre-intuitives, chacune mesurée sur un faux négatif de ce jour.
# Pas de `\b` en tête : dans `AZERTY_Global_1.1.0.msixbundle` le `1` suit un souligné, qui est
# un caractère de mot, donc `\b\d` n'y voit aucune frontière — le scanner était muet sur tous
# les noms de bundle du kit. Et la garde de queue est `(?!\d)`, non `(?![\d.])` : dans
# `1.1.0.msixbundle` la version est suivie d'un point, qu'un refus du point rejetait aussi.
# `1.1.0.0` reste lu d'un bloc, le groupe optionnel étant gourmand.
VERSION = re.compile(r"(?<![\d.])\d+\.\d+\.\d+(?:\.\d+)?(?!\d)")
EMPREINTE = re.compile(r"\b[0-9a-fA-F]{64}\b")
NOM_BUNDLE = re.compile(r"AZERTY_Global_(\d+\.\d+\.\d+)\.msixbundle")
URL_TELECHARGEMENT = re.compile(r"[^\s`\"'()<>]*download\.azerty\.global[^\s`\"'()<>]*")

# Les anomalies de ce niveau sont corrigeables sans le bundle signé : elles échouent
# toujours. Les autres attendent la bascule du kit et n'échouent que sous --release.
ERREUR = "ERREUR"
ATTENTE = "ATTENTE"


class Anomalie:
    def __init__(self, niveau, code, ligne, message):
        self.niveau = niveau
        self.code = code
        self.ligne = ligne
        self.message = message

    def __repr__(self):  # lisible dans un échec de test
        return f"{self.niveau} {self.code} L{self.ligne} {self.message}"


def version_courante(texte_csproj):
    """Version applicative de référence, lue dans le csproj. None si absente."""
    trouve = re.search(r"<Version>\s*([^<\s]+)\s*</Version>", texte_csproj)
    return trouve.group(1) if trouve else None


def formes_admises(courante):
    """La version courante s'écrit en trois ou quatre champs selon le document."""
    formes = {courante}
    if courante.count(".") == 2:
        formes.add(courante + ".0")
    return formes


def lire_bloc(texte):
    """Retourne (champs, numéros de ligne du bloc). champs vide si le bloc manque."""
    lignes = texte.splitlines()
    debut = None
    for n, ligne in enumerate(lignes, 1):
        if ligne.strip().startswith(DEBUT_BLOC):
            debut = n
            break
    if debut is None:
        return {}, set()

    champs = {}
    couvertes = {debut}
    for n in range(debut + 1, len(lignes) + 1):
        brute = lignes[n - 1].strip()
        couvertes.add(n)
        if brute.startswith(FIN_BLOC):
            break
        if ":" in brute:
            cle, _, valeur = brute.partition(":")
            champs[cle.strip()] = valeur.strip()
    return champs, couvertes


def valeurs_listees(champs, cle):
    """Liste d'un champ séparé par des virgules, jeton en-attente exclu."""
    brut = champs.get(cle, "")
    if not brut or brut == EN_ATTENTE:
        return set()
    return {v.strip() for v in brut.split(",") if v.strip()}


def analyser(texte, courante):
    """Anomalies d'un document. Fonction pure : ni fichier, ni sortie, ni code de retour."""
    anomalies = []
    champs, lignes_bloc = lire_bloc(texte)
    if not champs:
        return [Anomalie(ERREUR, "BLOC-ABSENT", 0,
                         f"aucun bloc « {DEBUT_BLOC} » : le document n'est surveillé par rien")]

    admises = formes_admises(courante)
    declaree = champs.get("version-app", "")
    if declaree not in admises:
        anomalies.append(Anomalie(
            ERREUR, "VERSION-DECLAREE-PERIMEE", 0,
            f"version-app annonce « {declaree or 'rien'} » quand le csproj est en {courante}"))

    historiques = valeurs_listees(champs, "versions-historiques")
    en_bascule = valeurs_listees(champs, "versions-en-bascule")
    empreintes = {e.upper() for e in valeurs_listees(champs, "empreintes-attendues")}
    attente_empreinte = champs.get("empreintes-attendues", "") == EN_ATTENTE

    for n, ligne in enumerate(texte.splitlines(), 1):
        if n in lignes_bloc:
            continue

        noms_bundle = {t.group(1) for t in NOM_BUNDLE.finditer(ligne)}
        for trouve in VERSION.finditer(ligne):
            vue = trouve.group(0)
            if vue in admises:
                continue
            # Un nom de fichier périmé passe avant la liste historique : il nomme le fichier
            # réellement livré dans le kit, pas un fait daté, et la bascule le remplacera.
            if vue in noms_bundle:
                anomalies.append(Anomalie(
                    ATTENTE, "NOM-BUNDLE-PERIME", n,
                    f"le kit nomme encore le bundle {vue} ; bascule par update_kit_version.py"))
            elif vue in en_bascule:
                anomalies.append(Anomalie(
                    ATTENTE, "VERSION-EN-BASCULE", n,
                    f"version {vue} déclarée remplaçable par la bascule du kit"))
            elif vue in historiques:
                continue
            else:
                anomalies.append(Anomalie(
                    ERREUR, "VERSION-CORPS-INCONNUE", n,
                    f"version {vue} ni courante, ni en bascule, ni déclarée historique"))

        for trouve in EMPREINTE.finditer(ligne):
            vue = trouve.group(0).upper()
            if vue in empreintes:
                continue
            niveau = ATTENTE if attente_empreinte else ERREUR
            anomalies.append(Anomalie(
                niveau, "EMPREINTE-NON-DECLAREE", n,
                f"empreinte {vue[:12]}… absente de empreintes-attendues"))

        for trouve in URL_TELECHARGEMENT.finditer(ligne):
            url = trouve.group(0)
            portee = VERSION.search(url)
            if portee is not None:
                anomalies.append(Anomalie(
                    ERREUR, "URL-VERSIONNEE", n,
                    f"« {url} » porte une version : le lot E a décidé des URL stables"))

    if attente_empreinte:
        anomalies.append(Anomalie(
            ATTENTE, "EMPREINTE-EN-ATTENTE", 0,
            "empreintes-attendues vaut en-attente : document non publiable en l'état"))

    return anomalies


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--release", action="store_true",
                    help="échouer aussi sur les attentes de bascule (porte de release)")
    args = ap.parse_args()

    if not CSPROJ.is_file():
        print(f"INCONCLUANT : csproj introuvable ({CSPROJ})")
        return 2
    courante = version_courante(CSPROJ.read_text(encoding="utf-8"))
    if courante is None:
        print(f"INCONCLUANT : aucun élément <Version> dans {CSPROJ.name}")
        return 2

    print(f"Version de référence (csproj) : {courante}")
    if args.release:
        print("Mode release : les attentes de bascule échouent aussi.")

    erreurs = attentes = 0
    manquants_externes = []
    for chemin, externe in DOCUMENTS:
        if not chemin.is_file():
            if externe:
                manquants_externes.append(chemin)
                continue
            print(f"\n### {chemin.name}\n  INCONCLUANT : document interne introuvable")
            return 2

        anomalies = analyser(chemin.read_text(encoding="utf-8"), courante)
        erreurs += sum(1 for a in anomalies if a.niveau == ERREUR)
        attentes += sum(1 for a in anomalies if a.niveau == ATTENTE)
        try:
            titre = chemin.relative_to(REPO.parent.parent).as_posix()
        except ValueError:
            titre = chemin.as_posix()
        etat = f"{len(anomalies)} anomalie(s)" if anomalies else "conforme"
        print(f"\n### {titre}  ({etat})")
        for a in anomalies:
            place = f"L{a.ligne}" if a.ligne else "bloc"
            print(f"  {a.niveau:<7} {place:<6} {a.code:<24} {a.message}")

    if manquants_externes:
        print(f"\nINCONCLUANT : {len(manquants_externes)} document(s) de parc hors dépôt "
              f"introuvable(s) — clone nu ?")
        for chemin in manquants_externes:
            print(f"  {chemin}")
        return 2

    print(f"\n{erreurs} erreur(s), {attentes} attente(s) de bascule.")
    if erreurs:
        return 1
    if attentes and args.release:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
