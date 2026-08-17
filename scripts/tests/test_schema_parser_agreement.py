"""Accord entre le schéma et le parseur, dans le sens que les autres témoins ne prouvent pas.

`test_validate_layout.py` prouve un seul sens : le schéma rejette ce qui casserait le parseur.
L'autre sens ne se testait nulle part — un fichier que le schéma accepte doit se lire — et ce
n'est pas gratuit :

- une propriété que le parseur exige sans qu'elle soit `required` laisse passer un fichier
  valide qui lève au démarrage ;
- une propriété que le parseur lit sans qu'elle soit déclarée dans `properties` est morte,
  puisque `additionalProperties: false` interdit à un fichier valide de la porter.

Le contrôle lit la source du parseur plutôt qu'une liste tenue à la main. Un nouveau receveur
dans `LayoutJsonParser.Parse` fait échouer `test_aucun_receveur_inconnu` au lieu de passer en
silence : c'est ce qui empêche ce témoin de devenir faux sans qu'on le voie.

    python -m unittest discover -s scripts/tests -v
"""
import json
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
SCHEMA = ROOT / "schemas" / "azerty-layout.schema.json"
PARSER = ROOT / "src" / "TypingEngine.Core" / "LayoutJsonParser.cs"

# Le receveur C# et l'endroit du schéma qui le décrit. Trois entrées, et le test qui suit
# refuse tout receveur absent de cette table.
LOCATIONS = {
    "root": ("(racine)", lambda schema: schema),
    "row": ("rows.items", lambda schema: schema["properties"]["rows"]["items"]),
    "key": ("$defs.key", lambda schema: schema["$defs"]["key"]),
    "property.Value": ("$defs.deadKey", lambda schema: schema["$defs"]["deadKey"]),
}

# Les deux helpers qui lèvent quand la propriété manque : ce que le parseur exige.
REQUIRED_READ = re.compile(r"\b(?:RequireArray|RequireString)\(\s*([A-Za-z_][\w.]*)\s*,\s*\"([^\"]+)\"")
# Les lectures tolérantes : absentes, elles ne lèvent pas, mais elles doivent rester déclarées.
OPTIONAL_READ = re.compile(
    r"\bGetStringOrNull\(\s*([A-Za-z_][\w.]*)\s*,\s*\"([^\"]+)\"|"
    r"\b([A-Za-z_][\w.]*)\.TryGetProperty\(\s*\"([^\"]+)\""
)


def _reads():
    source = PARSER.read_text(encoding="utf-8")
    required = {(receiver, name) for receiver, name in REQUIRED_READ.findall(source)}
    optional = set()
    for a_receiver, a_name, b_receiver, b_name in OPTIONAL_READ.findall(source):
        optional.add((a_receiver or b_receiver, a_name or b_name))
    return required, optional


class SchemaParserAgreementTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.schema = json.loads(SCHEMA.read_text(encoding="utf-8"))
        cls.required_reads, cls.optional_reads = _reads()

    def location(self, receiver):
        label, resolve = LOCATIONS[receiver]
        return label, resolve(self.schema)

    def test_le_parseur_lit_bien_quelque_chose(self):
        """Un motif qui ne trouve plus rien passerait tous les autres tests de ce fichier."""
        self.assertIn(("root", "rows"), self.required_reads)
        self.assertIn(("key", "scancode"), self.required_reads)
        self.assertIn(("key", "base"), self.optional_reads)
        self.assertGreaterEqual(len(self.required_reads), 4)
        self.assertGreaterEqual(len(self.optional_reads), 9)

    def test_aucun_receveur_inconnu(self):
        """Un nouveau niveau dans le parseur doit rejoindre LOCATIONS, pas passer inaperçu."""
        receivers = {receiver for receiver, _ in self.required_reads | self.optional_reads}
        unknown = receivers - set(LOCATIONS)
        self.assertEqual(
            set(),
            unknown,
            f"receveur(s) sans endroit connu dans le schéma : {sorted(unknown)}",
        )

    def test_ce_que_le_parseur_exige_est_required(self):
        for receiver, name in sorted(self.required_reads):
            with self.subTest(receiver=receiver, propriete=name):
                label, node = self.location(receiver)
                self.assertIn(
                    name,
                    node.get("required", []),
                    f"le parseur exige {name} mais le schéma ne l'impose pas en {label} : "
                    "un fichier valide lèverait au démarrage",
                )

    def test_ce_que_le_parseur_lit_est_declare(self):
        for receiver, name in sorted(self.required_reads | self.optional_reads):
            with self.subTest(receiver=receiver, propriete=name):
                label, node = self.location(receiver)
                self.assertIn(
                    name,
                    node.get("properties", {}),
                    f"le parseur lit {name}, que le schéma ne déclare pas en {label} : "
                    "additionalProperties false interdit à un fichier valide de la porter",
                )


if __name__ == "__main__":
    unittest.main()
