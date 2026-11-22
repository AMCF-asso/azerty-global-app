"""Témoins du contrôle d'inventaire des ressources (étape « ressources » de verify.ps1).

Le contrôle doit échouer sur une création sans libération connue, accepter un verdict lu,
garder ce verdict quand du code s'ajoute au-dessus de l'appel, et le déclarer périmé quand
l'appel lui-même change. Le contrôle sur le vrai src/ est l'étape « ressources » de verify.ps1.

    python -m unittest discover -s scripts/tests -v
"""
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent.parent
SCRIPT = ROOT / "scripts" / "audit-ressources-inventaire.py"

SOURCE = """namespace Banc;

internal static class Peintre
{
    internal static void Peindre()
    {
        IntPtr police = Win32.CreateFontW(-12, 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");
    }
}
"""
APPEL = 'IntPtr police = Win32.CreateFontW(-12, 0, 0, 0, 400, 0, 0, 0, 0, 0, 0, 5, 0, "Segoe UI");'
ENTETE = "fichier\tapi\toccurrence\tappel\tverdict\n"


class ControleInventaireTests(unittest.TestCase):
    def setUp(self):
        self._dir = tempfile.TemporaryDirectory()
        self.dir = Path(self._dir.name)
        self.src = self.dir / "src"
        self.src.mkdir()
        self.verdicts = self.dir / "verdicts.tsv"

    def tearDown(self):
        self._dir.cleanup()

    def check(self, source, verdicts=None):
        (self.src / "Peintre.cs").write_text(source, encoding="utf-8")
        if verdicts is not None:
            self.verdicts.write_text(ENTETE + verdicts, encoding="utf-8")
        return subprocess.run(
            [sys.executable, str(SCRIPT), "--check", "--src", str(self.src), "--verdicts", str(self.verdicts)],
            capture_output=True, text=True, encoding="utf-8",
        )

    def test_creation_sans_liberation_ni_verdict_echoue(self):
        r = self.check(SOURCE)
        self.assertEqual(r.returncode, 1, r.stdout)
        self.assertIn("sans verdict : Peintre.cs:7", r.stdout)

    def test_liberation_dans_la_methode_passe(self):
        libere = SOURCE.replace(APPEL, APPEL + "\n        Win32.DeleteObject(police);")
        r = self.check(libere)
        self.assertEqual(r.returncode, 0, r.stdout)

    def test_verdict_lu_passe(self):
        r = self.check(SOURCE, f"Peintre.cs\tCreateFontW\t1\t{APPEL}\tjamais : banc\n")
        self.assertEqual(r.returncode, 0, r.stdout)

    def test_verdict_lu_survit_au_code_ajoute_au_dessus(self):
        decale = SOURCE.replace("    internal static void Peindre()", "    // ajout\n    // ajout\n    internal static void Peindre()")
        r = self.check(decale, f"Peintre.cs\tCreateFontW\t1\t{APPEL}\tjamais : banc\n")
        self.assertEqual(r.returncode, 0, r.stdout)

    def test_appel_modifie_perime_le_verdict(self):
        change = SOURCE.replace("-12,", "-14,")
        r = self.check(change, f"Peintre.cs\tCreateFontW\t1\t{APPEL}\tjamais : banc\n")
        self.assertEqual(r.returncode, 1, r.stdout)
        self.assertIn("verdict lu sans appel", r.stdout)
        self.assertIn("sans verdict : Peintre.cs:7", r.stdout)

    def test_second_appel_identique_demande_son_propre_verdict(self):
        double = SOURCE.replace(APPEL, APPEL + "\n        " + APPEL.replace("police", "autre"))
        double = double.replace(APPEL.replace("police", "autre"), APPEL)
        r = self.check(double, f"Peintre.cs\tCreateFontW\t1\t{APPEL}\tjamais : banc\n")
        self.assertEqual(r.returncode, 1, r.stdout)
        self.assertIn("sans verdict : Peintre.cs:8", r.stdout)

    def test_ligne_de_verdict_malformee_echoue(self):
        r = self.check(SOURCE, "Peintre.cs:7\tjamais : banc\n")
        self.assertNotEqual(r.returncode, 0)
        self.assertIn("attendu fichier, api, occurrence, appel, verdict", r.stderr)


if __name__ == "__main__":
    unittest.main()
