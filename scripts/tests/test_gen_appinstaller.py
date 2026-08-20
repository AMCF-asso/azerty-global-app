"""Témoins du `.appinstaller` : chaque mutation doit être vue.

    python -m unittest discover -s scripts/tests -v

Un `.appinstaller` faux reste un XML valide. Il s'ouvre sans erreur, se publie sans se
plaindre, et casse là où personne ne regarde : à l'installation sur le poste d'une DSI, ou —
bien pire — il s'installe et ne se met jamais à jour. Aucun de ces tests ne vérifie que le
fichier est bien formé ; tous vérifient qu'il dit la vérité sur le bundle qu'il décrit.

La chaîne d'éditeur employée ici n'est pas inventée. C'est celle du bundle AMCF réellement
signé, relevée dans son manifeste le 2026-08-20, accents compris. Un témoin bâti sur une chaîne
plausible mais fausse passe au vert en refusant tout, y compris le cas réel — d'où
`test_identite_du_bundle_reel`, qui confronte le littéral au fichier signé quand celui-ci est
là, et `test_fichier_genere_ne_signale_rien`, sa réciproque.
"""
import importlib.util
import unittest
import zipfile
from pathlib import Path
from tempfile import TemporaryDirectory

ROOT = Path(__file__).resolve().parent.parent.parent

# Bundle AMCF réellement signé. Il vit dans le composant `website`, hors de ce dépôt : les
# tests qui en dépendent se sautent quand il est absent, ils ne rougissent pas.
REAL_BUNDLE = (
    ROOT.parent / "website" / "assets" / "downloads" / "AZERTY_Global_1.1.0.msixbundle"
)

# Relevé le 2026-08-20 dans `AppxMetadata/AppxBundleManifest.xml` du bundle ci-dessus.
REAL_NAME = "AZERTYGlobal.AZERTYGlobal"
REAL_PUBLISHER = (
    "CN=Association pour la Modernisation du Clavier Français, "
    "O=Association pour la Modernisation du Clavier Français, "
    "L=Clermont-Ferrand, S=Puy-de-Dôme, C=FR"
)
REAL_VERSION = "1.1.0.0"

BUNDLE_MANIFEST = """<?xml version="1.0" encoding="UTF-8" standalone="no"?>
<Bundle SchemaVersion="5.0" xmlns="http://schemas.microsoft.com/appx/2013/bundle">
\t<Identity Name="{name}" Publisher="{publisher}" Version="{version}"/>
\t<Packages>
\t\t<Package Type="application" Version="{version}" Architecture="x64" FileName="a.msix" Offset="0" Size="1"/>
\t</Packages>
</Bundle>
"""


def _load_generator():
    """Le script porte un tiret dans son nom : import par chemin, pas par nom de module."""
    spec = importlib.util.spec_from_file_location(
        "gen_appinstaller", ROOT / "scripts" / "gen-appinstaller.py"
    )
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


gen = _load_generator()


def make_bundle(directory, name=REAL_NAME, publisher=REAL_PUBLISHER, version=REAL_VERSION):
    """Écrit un `.msixbundle` minimal dont seul le manifeste compte."""
    path = Path(directory) / "bundle.msixbundle"
    manifest = BUNDLE_MANIFEST.format(name=name, publisher=publisher, version=version)
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr(gen.BUNDLE_MANIFEST_ENTRY, manifest.encode("utf-8"))
    return path


class IdentityReadingTests(unittest.TestCase):
    def test_identite_lue_dans_un_bundle_synthetique(self):
        with TemporaryDirectory() as directory:
            identity = gen.read_bundle_identity(make_bundle(directory))
        self.assertEqual(identity.name, REAL_NAME)
        self.assertEqual(identity.publisher, REAL_PUBLISHER)
        self.assertEqual(identity.version, REAL_VERSION)

    def test_identite_du_bundle_reel(self):
        """Le littéral accentué des autres tests est-il celui du fichier signé ?

        Sans ce test, tous les autres pourraient s'accorder sur une chaîne fausse.
        """
        if not REAL_BUNDLE.exists():
            self.skipTest(f"Bundle signé absent : {REAL_BUNDLE}")
        identity = gen.read_bundle_identity(REAL_BUNDLE)
        self.assertEqual(identity.name, REAL_NAME)
        self.assertEqual(identity.publisher, REAL_PUBLISHER)
        self.assertEqual(identity.version, REAL_VERSION)
        self.assertIn("Français", identity.publisher)
        self.assertIn("Puy-de-Dôme", identity.publisher)

    def test_archive_sans_manifeste_de_bundle(self):
        with TemporaryDirectory() as directory:
            path = Path(directory) / "vide.msixbundle"
            with zipfile.ZipFile(path, "w") as archive:
                archive.writestr("AppxManifest.xml", "<Package/>")
            with self.assertRaises(SystemExit):
                gen.read_bundle_identity(path)


class BuildRefusalTests(unittest.TestCase):
    """Ce que le générateur doit refuser de produire."""

    def test_refus_de_l_identite_store(self):
        identity = gen.BundleIdentity(REAL_NAME, gen.STORE_PUBLISHER, "1.2.0.0")
        with self.assertRaises(SystemExit):
            gen.build(identity)

    def test_refus_d_une_version_a_trois_segments(self):
        identity = gen.BundleIdentity(REAL_NAME, REAL_PUBLISHER, "1.2.0")
        with self.assertRaises(SystemExit):
            gen.build(identity)

    def test_refus_d_un_intervalle_hors_bornes(self):
        identity = gen.BundleIdentity(REAL_NAME, REAL_PUBLISHER, REAL_VERSION)
        with self.assertRaises(SystemExit):
            gen.build(identity, hours=256)

    def test_refus_d_un_editeur_qui_exigerait_un_echappement(self):
        identity = gen.BundleIdentity(
            REAL_NAME, 'CN="Association, Inc.", C=FR', REAL_VERSION
        )
        with self.assertRaises(SystemExit):
            gen.build(identity)

    def test_intervalle_a_zero_accepte(self):
        """Réciproque des bornes : 0 est une valeur légale, pas une valeur absente."""
        identity = gen.BundleIdentity(REAL_NAME, REAL_PUBLISHER, REAL_VERSION)
        self.assertIn('HoursBetweenUpdateChecks="0"', gen.build(identity, hours=0))


class MutationTests(unittest.TestCase):
    """Chaque mutation part du fichier correct et doit produire au moins un écart."""

    @classmethod
    def setUpClass(cls):
        cls.identity = gen.BundleIdentity(REAL_NAME, REAL_PUBLISHER, REAL_VERSION)
        cls.text = gen.build(cls.identity)

    def problems_for(self, text):
        return gen.validate(text.encode("utf-8"), self.identity)

    def assert_seen(self, mutated, label):
        self.assertNotEqual(mutated, self.text, f"{label} : la mutation n'a rien changé")
        problems = self.problems_for(mutated)
        self.assertTrue(problems, f"{label} : aucun écart signalé")

    def test_fichier_genere_ne_signale_rien(self):
        """Réciproque de toutes les mutations : sans mutation, aucun écart."""
        self.assertEqual(self.problems_for(self.text), [])

    def test_accent_retire_de_l_editeur(self):
        self.assert_seen(self.text.replace("Français", "Francais"), "accent retiré")

    def test_circonflexe_retire_de_l_editeur(self):
        self.assert_seen(self.text.replace("Puy-de-Dôme", "Puy-de-Dome"), "circonflexe retiré")

    def test_version_du_bundle_divergente(self):
        mutated = self.text.replace(
            f'Version="{REAL_VERSION}"\n              Uri=', 'Version="1.2.0.0"\n              Uri='
        )
        self.assert_seen(mutated, "version du MainBundle")

    def test_version_de_la_racine_divergente(self):
        self.assert_seen(
            self.text.replace(f'Version="{REAL_VERSION}">', 'Version="9.9.9.9">'),
            "version de la racine",
        )

    def test_nom_de_paquet_divergent(self):
        self.assert_seen(self.text.replace(REAL_NAME, "AZERTYGlobal.Autre"), "nom de paquet")

    def test_url_du_bundle_divergente(self):
        self.assert_seen(
            self.text.replace(gen.BUNDLE_URI, "https://example.invalid/b.msixbundle"),
            "URL du bundle",
        )

    def test_url_du_fichier_divergente(self):
        self.assert_seen(
            self.text.replace(gen.SELF_URI, "https://example.invalid/a.appinstaller"),
            "URL du fichier",
        )

    def test_intervalle_absent(self):
        self.assert_seen(
            self.text.replace(' HoursBetweenUpdateChecks="24"', ""), "intervalle absent"
        )

    def test_intervalle_different(self):
        self.assert_seen(
            self.text.replace('HoursBetweenUpdateChecks="24"', 'HoursBetweenUpdateChecks="8"'),
            "intervalle différent",
        )

    def test_bloc_de_mise_a_jour_absent(self):
        start = self.text.index("  <UpdateSettings>")
        end = self.text.index("</UpdateSettings>\n") + len("</UpdateSettings>\n")
        self.assert_seen(self.text[:start] + self.text[end:], "bloc UpdateSettings absent")

    def test_tache_de_fond_ajoutee(self):
        self.assert_seen(
            self.text.replace("  </UpdateSettings>", "    <AutomaticBackgroundTask />\n  </UpdateSettings>"),
            "tâche de fond ajoutée",
        )

    def test_activation_bloquante_sans_invite(self):
        self.assert_seen(
            self.text.replace(
                'HoursBetweenUpdateChecks="24"',
                'HoursBetweenUpdateChecks="24" UpdateBlocksActivation="true"',
            ),
            "activation bloquante sans invite",
        )

    def test_paquet_simple_au_lieu_du_bundle(self):
        self.assert_seen(
            self.text.replace("MainBundle", "MainPackage"), "MainPackage au lieu de MainBundle"
        )

    def test_echappement_introduit(self):
        self.assert_seen(
            self.text.replace("Français", "Fran&#231;ais"), "séquence d'échappement"
        )

    def test_element_racine_renomme(self):
        self.assert_seen(self.text.replace("AppInstaller", "AppInstaller2"), "racine renommée")

    def test_fins_de_ligne_crlf(self):
        self.assert_seen(self.text.replace("\n", "\r\n"), "fins de ligne CRLF")

    def test_bom_en_tete(self):
        raw = "﻿".encode("utf-8") + self.text.encode("utf-8")
        self.assertTrue(gen.validate(raw, self.identity), "BOM : aucun écart signalé")

    def test_xml_casse(self):
        self.assert_seen(self.text.replace("</AppInstaller>", ""), "XML tronqué")


class NonAsciiReportTests(unittest.TestCase):
    def test_les_trois_caracteres_accentues_sont_traces(self):
        identity = gen.BundleIdentity(REAL_NAME, REAL_PUBLISHER, REAL_VERSION)
        report = gen.non_ascii_report(gen.build(identity))
        self.assertEqual(len(report), 2, report)
        self.assertTrue(any("U+00E7" in line and "× 2" in line for line in report), report)
        self.assertTrue(any("U+00F4" in line and "× 1" in line for line in report), report)

    def test_un_fichier_sans_accent_ne_trace_rien(self):
        identity = gen.BundleIdentity(REAL_NAME, "CN=Ascii Only, C=FR", REAL_VERSION)
        self.assertEqual(gen.non_ascii_report(gen.build(identity)), [])


if __name__ == "__main__":
    unittest.main()
