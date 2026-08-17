"""Témoins du schéma de disposition : chaque mutation doit être rejetée.

Un contrôle qui n'a jamais échoué n'est pas un contrôle. Ces tests partent de la
disposition réelle, la cassent d'une façon précise, et vérifient que la violation est vue.
Le cas qui justifie à lui seul un schéma fermé est `test_cle_racine_mal_orthographiee` :
sans lui, un `deadkeys` fautif se lit comme un fichier sans touches mortes.

    python -m unittest discover -s scripts/tests -v
"""
import copy
import importlib.util
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent


def _load_validator():
    """Le script porte un tiret dans son nom : import par chemin, pas par nom de module."""
    spec = importlib.util.spec_from_file_location(
        "validate_layout", ROOT / "scripts" / "validate-layout.py"
    )
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


validator = _load_validator()


class LayoutSchemaTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.schema = json.loads(validator.SCHEMA.read_text(encoding="utf-8"))
        cls.layout = json.loads(validator.LAYOUT.read_text(encoding="utf-8"))

    def mutate(self, change):
        layout = copy.deepcopy(self.layout)
        change(layout)
        return layout

    def assertRejected(self, change, fragment):
        problems = validator.check_schema(self.mutate(change), self.schema)
        self.assertTrue(problems, "la mutation est passée alors qu'elle devait être rejetée")
        self.assertTrue(
            any(fragment in problem for problem in problems),
            f"rejet obtenu, mais aucun message ne mentionne {fragment!r} : {problems}",
        )

    def assertAccepted(self, change):
        self.assertEqual([], validator.check_schema(self.mutate(change), self.schema))

    # --- le fichier réel, tel qu'il est

    def test_disposition_reelle_conforme(self):
        self.assertEqual([], validator.check_schema(self.layout, self.schema))
        self.assertEqual([], validator.check_counters(self.layout))
        self.assertEqual([], validator.check_references(self.layout))

    # --- schéma fermé

    def test_cle_racine_mal_orthographiee(self):
        def change(layout):
            layout["deadkeys"] = layout.pop("dead_keys")

        self.assertRejected(change, "deadkeys")

    def test_cle_racine_inconnue(self):
        self.assertRejected(lambda l: l.update(couleur="bleu"), "couleur")

    def test_champ_de_touche_inconnu(self):
        self.assertRejected(
            lambda l: l["rows"][0]["keys"][0].update(doigt="gauche"), "doigt"
        )

    # --- scancode : les trois formes acceptées par ParseScancode, et rien d'autre

    def test_scancode_non_numerique_rejete(self):
        self.assertRejected(lambda l: l["rows"][0]["keys"][0].update(scancode="ZZZ"), "ZZZ")

    def test_scancode_hexa_sans_prefixe_rejete(self):
        """ABCD passerait un motif trop permissif, et ferait lever uint.Parse en décimal."""
        self.assertRejected(lambda l: l["rows"][0]["keys"][0].update(scancode="ABCD"), "ABCD")

    def test_scancode_prefixe_0x_accepte(self):
        self.assertAccepted(lambda l: l["rows"][0]["keys"][0].update(scancode="0x12"))

    def test_scancode_decimal_accepte(self):
        self.assertAccepted(lambda l: l["rows"][0]["keys"][0].update(scancode="18"))

    # --- autres formes contraintes

    def test_position_hors_forme_rejetee(self):
        self.assertRejected(lambda l: l["rows"][0]["keys"][0].update(position="Z9"), "Z9")

    def test_doigt_hors_enumeration_rejete(self):
        self.assertRejected(
            lambda l: l["rows"][0]["keys"][0].update(finger="left_thumb"), "left_thumb"
        )

    def test_nom_de_touche_morte_hors_convention_rejete(self):
        def change(layout):
            layout["dead_keys"]["circonflexe"] = layout["dead_keys"].pop("dk_circumflex")

        self.assertRejected(change, "circonflexe")

    def test_declencheur_de_table_a_deux_caracteres_rejete(self):
        """Une clé de deux caractères ne serait jamais atteinte par une frappe."""

        def change(layout):
            table = layout["dead_keys"]["dk_circumflex"]["table"]
            table["ee"] = table.pop("e")

        self.assertRejected(change, "ee")

    def test_couche_numerique_rejetee(self):
        self.assertRejected(lambda l: l["rows"][0]["keys"][0].update(base=1), "1")

    def test_couche_nulle_acceptee(self):
        self.assertAccepted(lambda l: l["rows"][0]["keys"][0].update(alt_gr=None))


class LayoutCounterTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.layout = json.loads(validator.LAYOUT.read_text(encoding="utf-8"))

    def mutate(self, change):
        layout = copy.deepcopy(self.layout)
        change(layout)
        return layout

    def test_compteur_faux_detecte(self):
        problems = validator.check_counters(
            self.mutate(lambda l: l["statistics"].update(physical_keys=48))
        )
        self.assertEqual(1, len(problems))
        self.assertIn("physical_keys", problems[0])

    def test_touche_retiree_desaccorde_les_compteurs(self):
        """Retirer une touche doit se voir : c'est le mode de panne d'une édition manuelle."""
        problems = validator.check_counters(self.mutate(lambda l: l["rows"][0]["keys"].pop()))
        self.assertTrue(any("physical_keys" in problem for problem in problems))

    def test_touche_morte_retiree_desaccorde_les_compteurs(self):
        """Le nombre de touches mortes etait tenu a la main dans le validateur Node du
        script de synchronisation, et dans ResourceAlignmentTests. Il se recalcule."""
        problems = validator.check_counters(self.mutate(lambda l: l["dead_keys"].pop("dk_horn")))
        self.assertTrue(any("dead_keys_count" in problem for problem in problems))

    def test_caractere_direct_ajoute_desaccorde_les_compteurs(self):
        """Poser un caractere inedit sur une couche libre : le mode de panne d'un ajout a la
        main, que la constante direct_characters du validateur Node attrapait."""

        def change(layout):
            layout["rows"][0]["keys"][0]["shift_alt_gr"] = ""

        problems = validator.check_counters(self.mutate(change))
        self.assertTrue(any("direct_characters" in problem for problem in problems))

    def test_caractere_produit_inedit_desaccorde_les_compteurs(self):
        """Une combinaison qui produit un caractere absent partout ailleurs deplace le total
        unique : c'est ce que la constante total_unique_characters attrapait."""

        def change(layout):
            layout["dead_keys"]["dk_horn"]["table"][""] = ""

        problems = validator.check_counters(self.mutate(change))
        self.assertTrue(any("total_unique_characters" in problem for problem in problems))

    def test_combinaison_retiree_desaccorde_les_compteurs(self):
        def change(layout):
            table = layout["dead_keys"]["dk_circumflex"]["table"]
            table.pop(next(iter(table)))

        problems = validator.check_counters(self.mutate(change))
        self.assertTrue(any("dead_key_combinations" in problem for problem in problems))


class LayoutReferenceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.layout = json.loads(validator.LAYOUT.read_text(encoding="utf-8"))

    def mutate(self, change):
        layout = copy.deepcopy(self.layout)
        change(layout)
        return layout

    def test_declencheur_non_declare_detecte(self):
        problems = validator.check_references(
            self.mutate(lambda l: l["rows"][0]["keys"][0].update(alt_gr="dk_inexistante"))
        )
        self.assertTrue(any("dk_inexistante" in problem for problem in problems))

    def test_touche_morte_jamais_posee_detectee(self):
        def change(layout):
            layout["dead_keys"]["dk_orpheline"] = {
                "description": "jamais posée sur une couche",
                "example": "—",
                "table": {"a": "á"},
            }

        problems = validator.check_references(self.mutate(change))
        self.assertTrue(any("dk_orpheline" in problem for problem in problems))

    def test_scancode_en_double_detecte(self):
        def change(layout):
            keys = layout["rows"][0]["keys"]
            keys[1]["scancode"] = keys[0]["scancode"]

        problems = validator.check_references(self.mutate(change))
        self.assertTrue(any("scancode" in problem for problem in problems))

    def test_position_en_double_detectee(self):
        def change(layout):
            keys = layout["rows"][0]["keys"]
            keys[1]["position"] = keys[0]["position"]

        problems = validator.check_references(self.mutate(change))
        self.assertTrue(any("position" in problem for problem in problems))


if __name__ == "__main__":
    unittest.main()
