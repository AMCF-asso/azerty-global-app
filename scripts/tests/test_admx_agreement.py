"""Accord entre le modèle d'administration et le code qui lit les stratégies.

    python -m unittest discover -s scripts/tests -v

Le modèle ADMX, ses deux fichiers de libellés et le `.reg` d'exemple sont lus par une DSI, dans
une console, à un moment où personne du projet n'est là pour rectifier. Rien dans Windows ne
signale un `valueName` qui ne correspond plus à rien : la stratégie s'applique, écrit sa valeur,
et l'application ne la lit jamais. Ces tests sont le seul endroit où ce silence devient une
erreur.

`test_le_scanner_voit_quelque_chose` porte la charge de preuve des autres : un motif qui ne
trouve plus aucune constante ferait passer tous les tests de correspondance, chacun comparant
alors deux ensembles vides.
"""
import re
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent

POLICY_MANAGER = ROOT / "src" / "PolicyManager.cs"
PRODUCT_IDENTITY = ROOT / "src" / "ProductIdentity.cs"
ADMX = ROOT / "entreprise" / "AZERTYGlobal.admx"
ADML = {
    "fr-FR": ROOT / "entreprise" / "fr-FR" / "AZERTYGlobal.adml",
    "en-US": ROOT / "entreprise" / "en-US" / "AZERTYGlobal.adml",
}
REG = ROOT / "entreprise" / "politiques-exemple.reg"

GP_NAMESPACE = "{http://schemas.microsoft.com/GroupPolicy/2006/07/PolicyDefinitions}"

# Les cinq valeurs sont déclarées dans PolicyManager.cs sous la forme
# `internal const string ValueXxx = "NomDansLeRegistre";`. C'est le nom du registre qui compte
# ici, pas celui de la constante.
VALUE_CONSTANT = re.compile(r'internal const string Value\w+\s*=\s*"([^"]+)"')
POLICIES_ROOT = re.compile(r'internal const string PoliciesRoot\s*=\s*@"([^"]+)"')
NAMESPACE_CONSTANT = re.compile(r'public const string Namespace\s*=\s*"([^"]+)"')

# Inversion assumée de la fenêtre de bienvenue : activer la stratégie écrit 0.
EXPECTED_VALUES = {
    "NotificationsEnabled": ("1", "0"),
    "UsageStatsEnabled": ("1", "0"),
    "ExternalLinksEnabled": ("1", "0"),
    "ShowOnboarding": ("0", "1"),
}


def read(path):
    return path.read_text(encoding="utf-8")


class ScannerTests(unittest.TestCase):
    def test_le_scanner_voit_quelque_chose(self):
        """Sans ce test, un motif aveugle rendrait tous les autres vacuement verts."""
        self.assertEqual(len(VALUE_CONSTANT.findall(read(POLICY_MANAGER))), 5)
        self.assertEqual(len(POLICIES_ROOT.findall(read(POLICY_MANAGER))), 1)
        self.assertEqual(len(NAMESPACE_CONSTANT.findall(read(PRODUCT_IDENTITY))), 1)
        self.assertTrue(ADMX.exists())
        for path in ADML.values():
            self.assertTrue(path.exists(), path)
        self.assertTrue(REG.exists())


class AdmxTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.root = ET.fromstring(ADMX.read_bytes())
        cls.policies = cls.root.findall(f"{GP_NAMESPACE}policies/{GP_NAMESPACE}policy")
        cls.code_values = set(VALUE_CONSTANT.findall(read(POLICY_MANAGER)))
        root_path = POLICIES_ROOT.search(read(POLICY_MANAGER)).group(1)
        namespace = NAMESPACE_CONSTANT.search(read(PRODUCT_IDENTITY)).group(1)
        cls.expected_key = root_path + namespace

    def test_cinq_strategies(self):
        self.assertEqual(len(self.policies), 5)

    def test_les_valeurs_du_modele_sont_celles_du_code(self):
        """Le cas qui justifie ce fichier : une clé renommée dans le code, un ADMX qui pose
        toujours l'ancienne. La stratégie s'appliquerait sans effet, en silence."""
        declared = set()
        for policy in self.policies:
            name = policy.get("valueName")
            if name is not None:
                declared.add(name)
            for enum in policy.findall(f"{GP_NAMESPACE}elements/{GP_NAMESPACE}enum"):
                declared.add(enum.get("valueName"))
        self.assertEqual(declared, self.code_values)

    def test_toutes_les_strategies_pointent_la_racine_du_code(self):
        for policy in self.policies:
            self.assertEqual(policy.get("key"), self.expected_key, policy.get("name"))

    def test_toutes_les_strategies_sont_machine(self):
        for policy in self.policies:
            self.assertEqual(policy.get("class"), "Machine", policy.get("name"))

    def test_valeurs_ecrites_par_chaque_strategie(self):
        for policy in self.policies:
            value_name = policy.get("valueName")
            if value_name not in EXPECTED_VALUES:
                continue
            enabled = policy.find(f"{GP_NAMESPACE}enabledValue/{GP_NAMESPACE}decimal")
            disabled = policy.find(f"{GP_NAMESPACE}disabledValue/{GP_NAMESPACE}decimal")
            expected_enabled, expected_disabled = EXPECTED_VALUES[value_name]
            self.assertEqual(enabled.get("value"), expected_enabled, value_name)
            self.assertEqual(disabled.get("value"), expected_disabled, value_name)

    def test_la_fenetre_de_bienvenue_reste_inversee(self):
        """Elle est le seul réglage dont « Activé » écrit 0. Un jour quelqu'un « corrigera »
        cette anomalie, et la stratégie se mettra à imposer la fenêtre au lieu de la retirer."""
        onboarding = [p for p in self.policies if p.get("valueName") == "ShowOnboarding"]
        self.assertEqual(len(onboarding), 1)
        enabled = onboarding[0].find(f"{GP_NAMESPACE}enabledValue/{GP_NAMESPACE}decimal")
        self.assertEqual(enabled.get("value"), "0")
        self.assertEqual(onboarding[0].get("name"), "RemoveOnboarding")

    def test_langues_acceptees_par_le_selecteur(self):
        language = [p for p in self.policies if p.get("name") == "Language"]
        self.assertEqual(len(language), 1)
        items = language[0].findall(
            f"{GP_NAMESPACE}elements/{GP_NAMESPACE}enum/{GP_NAMESPACE}item"
        )
        values = [item.find(f"{GP_NAMESPACE}value/{GP_NAMESPACE}string").text for item in items]
        self.assertEqual(values, ["fr", "en"])


class AdmlTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.admx_text = read(ADMX)
        cls.strings = {}
        cls.presentations = {}
        for culture, path in ADML.items():
            root = ET.fromstring(path.read_bytes())
            table = root.find(f"{GP_NAMESPACE}resources/{GP_NAMESPACE}stringTable")
            cls.strings[culture] = {
                element.get("id"): (element.text or "")
                for element in table.findall(f"{GP_NAMESPACE}string")
            }
            table = root.find(f"{GP_NAMESPACE}resources/{GP_NAMESPACE}presentationTable")
            cls.presentations[culture] = {
                element.get("id")
                for element in table.findall(f"{GP_NAMESPACE}presentation")
            }

    def test_chaque_reference_du_modele_existe_dans_les_deux_langues(self):
        referenced = set(re.findall(r"\$\(string\.([A-Za-z0-9_]+)\)", self.admx_text))
        self.assertTrue(referenced)
        for culture, table in self.strings.items():
            self.assertEqual(referenced - set(table), set(), culture)

    def test_aucun_libelle_orphelin(self):
        """Réciproque : une chaîne que le modèle n'appelle plus est du texte mort, et un
        libellé renommé d'un seul côté se verrait ici."""
        referenced = set(re.findall(r"\$\(string\.([A-Za-z0-9_]+)\)", self.admx_text))
        for culture, table in self.strings.items():
            self.assertEqual(set(table) - referenced, set(), culture)

    def test_les_deux_langues_portent_les_memes_identifiants(self):
        identifiers = [set(table) for table in self.strings.values()]
        self.assertEqual(identifiers[0], identifiers[1])

    def test_aucun_libelle_vide(self):
        for culture, table in self.strings.items():
            vides = [key for key, value in table.items() if not value.strip()]
            self.assertEqual(vides, [], culture)

    def test_les_presentations_referencees_existent(self):
        referenced = set(re.findall(r"\$\(presentation\.([A-Za-z0-9_]+)\)", self.admx_text))
        self.assertTrue(referenced)
        for culture, available in self.presentations.items():
            self.assertEqual(referenced - available, set(), culture)

    def test_le_francais_et_l_anglais_ne_sont_pas_le_meme_texte(self):
        """Un .adml recopié d'une langue sur l'autre passerait tous les tests ci-dessus.

        La comparaison ne porte que sur les identifiants communs aux deux tables. Itérer sur
        ceux du français lèverait un KeyError dès qu'une table en porte un de plus — un plantage
        au lieu d'un échec, qui masquait dans la sortie du témoin le test réellement
        informatif. Mesuré le 2026-08-20 sur la mutation « libellé orphelin ajouté ».
        """
        francais = self.strings["fr-FR"]
        anglais = self.strings["en-US"]
        communs = set(francais) & set(anglais)
        differents = [key for key in communs if francais[key] != anglais[key]]
        self.assertGreaterEqual(len(differents), 8, differents)


class RegTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.raw = REG.read_bytes()
        cls.text = cls.raw[2:].decode("utf-16-le")

    def test_encodage_utf16_avec_bom(self):
        """regedit importe sans discuter un .reg UTF-16 LE. En UTF-8 il lit les accents comme
        de l'ANSI, ou refuse le fichier selon la voie d'import."""
        self.assertEqual(self.raw[:2], b"\xff\xfe")

    def test_fins_de_ligne_crlf(self):
        self.assertEqual(self.text.count("\n") - self.text.count("\r\n"), 0)

    def test_entete_attendu_par_regedit(self):
        self.assertTrue(self.text.startswith("Windows Registry Editor Version 5.00"))

    def test_la_cle_est_celle_du_code(self):
        root_path = POLICIES_ROOT.search(read(POLICY_MANAGER)).group(1)
        namespace = NAMESPACE_CONSTANT.search(read(PRODUCT_IDENTITY)).group(1)
        self.assertIn(f"[HKEY_LOCAL_MACHINE\\{root_path}{namespace}]", self.text)

    def test_les_cinq_valeurs_sont_posees(self):
        for value in VALUE_CONSTANT.findall(read(POLICY_MANAGER)):
            self.assertIn(f'"{value}"=', self.text, value)

    def test_la_fenetre_de_bienvenue_est_posee_a_zero(self):
        self.assertIn('"ShowOnboarding"=dword:00000000', self.text)

    def test_la_langue_est_une_chaine_pas_un_dword(self):
        """Language est le seul REG_SZ des cinq ; un dword ici serait ignoré à la lecture."""
        self.assertRegex(self.text, r'"Language"="(fr|en)"')


if __name__ == "__main__":
    unittest.main()
