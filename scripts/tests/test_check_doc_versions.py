"""Témoins du scanner de version périmée des documents de parc.

    python -m unittest discover -s scripts/tests -v

Un document de parc faux reste un Markdown valide : il s'affiche, il se remet à une DSI, et
il ment sur la version, l'empreinte ou l'URL de ce qu'elle va déployer. Aucun test ci-dessous
ne vérifie que le document est bien formé ; tous vérifient que le scanner rougit là où il
doit rougir, et se taît là où le document dit vrai.

⚠️ Deux tests portent sur des faux négatifs réellement observés le 2026-08-21, à la première
exécution du scanner sur les documents du kit — `test_nom_de_bundle_apres_un_souligne` et
`test_nom_de_bundle_suivi_d_un_point`. Le motif de version portait alors un `\\b` en tête et
un refus du point en queue : il ne voyait aucun des 24 noms de bundle du kit, et le scanner
annonçait « conforme » sur cinq documents périmés. Un scanner muet est pire qu'aucun scanner,
puisqu'il tient lieu de preuve. Ces deux tests sont là pour que la cécité ne revienne pas.
"""
import importlib.util
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent

spec = importlib.util.spec_from_file_location(
    "check_doc_versions", ROOT / "scripts" / "check-doc-versions.py")
scanner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(scanner)

COURANTE = "1.2.0"
EMPREINTE_A = "79A9C9C80CE9441272961DA20CEC3206307D26CD9BBF23AB57F9D7BE8BF6530E"
EMPREINTE_B = "1B040DE6AE43A43E6AD0C8EABD962E18083FF084DDCB3ED19EAE8CC4F9C7BFFC"


def doc(bloc, corps=""):
    """Document de test : titre, bloc de suivi, corps."""
    return f"# Titre\n\n{bloc}\n\n{corps}\n"


BLOC_NU = "<!-- suivi-version\nversion-app: 1.2.0\n-->"
BLOC_HISTORIQUE = ("<!-- suivi-version\nversion-app: 1.2.0\n"
                   "versions-historiques: 1.0.0, 1.1.0\n-->")
BLOC_ATTENTE = ("<!-- suivi-version\nversion-app: 1.2.0\n"
                "empreintes-attendues: en-attente\n-->")


def codes(anomalies, niveau=None):
    return [a.code for a in anomalies if niveau is None or a.niveau == niveau]


class VersionDeReferenceTests(unittest.TestCase):
    def test_version_lue_dans_le_csproj(self):
        self.assertEqual(scanner.version_courante("<Version>1.2.0</Version>"), "1.2.0")

    def test_version_avec_espaces(self):
        self.assertEqual(scanner.version_courante("<Version> 1.2.0 </Version>"), "1.2.0")

    def test_csproj_sans_version(self):
        self.assertIsNone(scanner.version_courante("<PropertyGroup></PropertyGroup>"))

    def test_csproj_reel_du_depot(self):
        """La version de référence existe vraiment : sans elle le scanner est inconcluant."""
        texte = (ROOT / "src" / "AZERTYGlobal.csproj").read_text(encoding="utf-8")
        self.assertRegex(scanner.version_courante(texte) or "", r"^\d+\.\d+\.\d+$")

    def test_forme_a_quatre_champs_admise(self):
        self.assertEqual(scanner.formes_admises("1.2.0"), {"1.2.0", "1.2.0.0"})


class BlocTests(unittest.TestCase):
    def test_bloc_absent_est_une_erreur(self):
        anomalies = scanner.analyser("# Titre\n\nCorps sans bloc.\n", COURANTE)
        self.assertEqual(codes(anomalies), ["BLOC-ABSENT"])
        self.assertEqual(anomalies[0].niveau, scanner.ERREUR)

    def test_bloc_present_et_document_vide_est_conforme(self):
        self.assertEqual(scanner.analyser(doc(BLOC_NU), COURANTE), [])

    def test_version_declaree_perimee(self):
        bloc = "<!-- suivi-version\nversion-app: 1.1.0\n-->"
        self.assertEqual(codes(scanner.analyser(doc(bloc), COURANTE)),
                         ["VERSION-DECLAREE-PERIMEE"])

    def test_version_declaree_a_quatre_champs_acceptee(self):
        bloc = "<!-- suivi-version\nversion-app: 1.2.0.0\n-->"
        self.assertEqual(scanner.analyser(doc(bloc), COURANTE), [])

    def test_le_bloc_ne_se_scanne_pas_lui_meme(self):
        """Sans cette exclusion, `versions-historiques: 1.0.0` se dénoncerait tout seul."""
        self.assertEqual(scanner.analyser(doc(BLOC_HISTORIQUE), COURANTE), [])


class VersionsDuCorpsTests(unittest.TestCase):
    def test_version_inconnue_est_une_erreur(self):
        anomalies = scanner.analyser(doc(BLOC_NU, "Voir la v0.9.8 pour mémoire."), COURANTE)
        self.assertEqual(codes(anomalies), ["VERSION-CORPS-INCONNUE"])
        self.assertEqual(anomalies[0].niveau, scanner.ERREUR)

    def test_version_historique_declaree_est_muette(self):
        corps = "- v1.0.0 publiée le 2026-06-29, v1.1.0 le 2026-07-23."
        self.assertEqual(scanner.analyser(doc(BLOC_HISTORIQUE, corps), COURANTE), [])

    def test_version_courante_est_muette(self):
        self.assertEqual(scanner.analyser(doc(BLOC_NU, "La 1.2.0 corrige…"), COURANTE), [])

    def test_version_en_bascule_est_une_attente(self):
        bloc = "<!-- suivi-version\nversion-app: 1.2.0\nversions-en-bascule: 1.1.0.0\n-->"
        anomalies = scanner.analyser(doc(bloc, "- **Version** : `1.1.0.0`"), COURANTE)
        self.assertEqual(codes(anomalies), ["VERSION-EN-BASCULE"])
        self.assertEqual(anomalies[0].niveau, scanner.ATTENTE)

    def test_ni_licence_ni_runtime_ni_build_windows_ne_sont_des_versions(self):
        corps = "Sous licence EUPL 1.2, en .NET 8, à partir de Windows 10 1809."
        self.assertEqual(scanner.analyser(doc(BLOC_NU, corps), COURANTE), [])

    def test_la_ligne_est_reportee(self):
        # Titre 1, vide 2, bloc 3 à 5, vide 6, puis le corps : la version est en ligne 9.
        corps = "ligne un\nligne deux\nv0.9.8 ici"
        anomalies = scanner.analyser(doc(BLOC_NU, corps), COURANTE)
        self.assertEqual([a.ligne for a in anomalies], [9])


class NomDeBundleTests(unittest.TestCase):
    def test_nom_de_bundle_apres_un_souligne(self):
        """Faux négatif du 2026-08-21 : `\\b` ne voit rien entre `_` et `1`."""
        corps = "Double-cliquez sur `AZERTY_Global_1.1.0.msixbundle`."
        anomalies = scanner.analyser(doc(BLOC_NU, corps), COURANTE)
        self.assertEqual(codes(anomalies), ["NOM-BUNDLE-PERIME"])

    def test_nom_de_bundle_suivi_d_un_point(self):
        """Faux négatif du 2026-08-21 : la version est suivie du point de `.msixbundle`."""
        corps = "Get-FileHash .\\AZERTY_Global_1.1.0.msixbundle -Algorithm SHA256"
        self.assertEqual(codes(scanner.analyser(doc(BLOC_NU, corps), COURANTE)),
                         ["NOM-BUNDLE-PERIME"])

    def test_nom_de_bundle_perime_passe_avant_la_liste_historique(self):
        """Un nom de fichier n'est pas un fait daté : il nomme ce que le kit livre."""
        corps = "Fichier inclus : `AZERTY_Global_1.1.0.msixbundle`"
        anomalies = scanner.analyser(doc(BLOC_HISTORIQUE, corps), COURANTE)
        self.assertEqual(codes(anomalies), ["NOM-BUNDLE-PERIME"])
        self.assertEqual(anomalies[0].niveau, scanner.ATTENTE)

    def test_nom_de_bundle_a_jour_est_muet(self):
        corps = "Fichier inclus : `AZERTY_Global_1.2.0.msixbundle`"
        self.assertEqual(scanner.analyser(doc(BLOC_NU, corps), COURANTE), [])

    def test_chemin_d_archive_et_nom_sur_la_meme_ligne(self):
        bloc = "<!-- suivi-version\nversion-app: 1.2.0\nversions-en-bascule: 1.1.0.0\n-->"
        corps = "`Archives/artifact-signing/1.1.0.0/AZERTY_Global_1.1.0.msixbundle`"
        self.assertEqual(sorted(codes(scanner.analyser(doc(bloc, corps), COURANTE))),
                         ["NOM-BUNDLE-PERIME", "VERSION-EN-BASCULE"])


class EmpreinteTests(unittest.TestCase):
    def test_empreinte_non_declaree_est_une_erreur(self):
        anomalies = scanner.analyser(doc(BLOC_NU, EMPREINTE_A), COURANTE)
        self.assertEqual(codes(anomalies), ["EMPREINTE-NON-DECLAREE"])
        self.assertEqual(anomalies[0].niveau, scanner.ERREUR)

    def test_empreinte_declaree_est_muette(self):
        bloc = f"<!-- suivi-version\nversion-app: 1.2.0\nempreintes-attendues: {EMPREINTE_A}\n-->"
        self.assertEqual(scanner.analyser(doc(bloc, EMPREINTE_A), COURANTE), [])

    def test_empreinte_declaree_en_minuscules(self):
        bloc = ("<!-- suivi-version\nversion-app: 1.2.0\n"
                f"empreintes-attendues: {EMPREINTE_A.lower()}\n-->")
        self.assertEqual(scanner.analyser(doc(bloc, EMPREINTE_A), COURANTE), [])

    def test_une_seconde_empreinte_non_declaree_rougit(self):
        bloc = f"<!-- suivi-version\nversion-app: 1.2.0\nempreintes-attendues: {EMPREINTE_A}\n-->"
        anomalies = scanner.analyser(doc(bloc, f"{EMPREINTE_A}\n{EMPREINTE_B}"), COURANTE)
        self.assertEqual(codes(anomalies), ["EMPREINTE-NON-DECLAREE"])

    def test_en_attente_degrade_l_empreinte_en_simple_attente(self):
        anomalies = scanner.analyser(doc(BLOC_ATTENTE, EMPREINTE_A), COURANTE)
        self.assertEqual(codes(anomalies, scanner.ERREUR), [])
        self.assertEqual(sorted(codes(anomalies)),
                         ["EMPREINTE-EN-ATTENTE", "EMPREINTE-NON-DECLAREE"])

    def test_en_attente_se_signale_meme_sans_empreinte_dans_le_corps(self):
        self.assertEqual(codes(scanner.analyser(doc(BLOC_ATTENTE), COURANTE)),
                         ["EMPREINTE-EN-ATTENTE"])


class UrlTests(unittest.TestCase):
    def test_url_versionnee_est_une_erreur(self):
        corps = "`https://download.azerty.global/AZERTY_Global_1.1.0.msixbundle`"
        anomalies = scanner.analyser(doc(BLOC_NU, corps), COURANTE)
        self.assertIn("URL-VERSIONNEE", codes(anomalies))
        self.assertIn(scanner.ERREUR, [a.niveau for a in anomalies if a.code == "URL-VERSIONNEE"])

    def test_url_stable_est_muette(self):
        corps = "`https://download.azerty.global/AZERTY_Global.msixbundle`"
        self.assertEqual(scanner.analyser(doc(BLOC_NU, corps), COURANTE), [])

    def test_url_de_l_appinstaller_est_muette(self):
        corps = "`https://download.azerty.global/AZERTY_Global.appinstaller`"
        self.assertEqual(scanner.analyser(doc(BLOC_NU, corps), COURANTE), [])

    def test_une_url_hors_domaine_de_telechargement_n_est_pas_jugee(self):
        """Seul `download.azerty.global` sert des paquets : le reste du site est hors sujet."""
        corps = "`https://azerty.global/notes/1.1.0`"
        self.assertEqual(codes(scanner.analyser(doc(BLOC_NU, corps), COURANTE)),
                         ["VERSION-CORPS-INCONNUE"])


class DocumentsReelsTests(unittest.TestCase):
    """Non-régression sur les documents du parc : aucune ERREUR ne doit y subsister.

    Les documents hors dépôt sont sautés quand ils sont absents — clone nu du dépôt public.
    """

    def test_aucune_erreur_dans_les_documents_de_parc(self):
        courante = scanner.version_courante(
            (ROOT / "src" / "AZERTYGlobal.csproj").read_text(encoding="utf-8"))
        vus = 0
        for chemin, externe in scanner.DOCUMENTS:
            if not chemin.is_file():
                if externe:
                    continue
                self.fail(f"document interne introuvable : {chemin}")
            vus += 1
            erreurs = [a for a in scanner.analyser(
                chemin.read_text(encoding="utf-8"), courante) if a.niveau == scanner.ERREUR]
            self.assertEqual(erreurs, [], f"{chemin.name} : {erreurs}")
        self.assertGreaterEqual(vus, 1, "aucun document surveillé n'a été lu")

    def test_le_document_interne_porte_bien_un_bloc(self):
        interne = [c for c, externe in scanner.DOCUMENTS if not externe]
        self.assertTrue(interne, "aucun document interne dans la liste surveillée")
        for chemin in interne:
            champs, lignes = scanner.lire_bloc(chemin.read_text(encoding="utf-8"))
            self.assertIn("version-app", champs, f"{chemin.name} sans bloc de suivi")


if __name__ == "__main__":
    unittest.main()
