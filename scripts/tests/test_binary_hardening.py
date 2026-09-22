"""Le garde BinSkim distingue absence d'alerte et analyse réellement terminée."""
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("binary_hardening", Path(__file__).resolve().parents[1] / "check-binary-hardening.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class BinaryHardeningTests(unittest.TestCase):
    def report(self, successful=True, results=None):
        return {"runs": [{"invocations": [{"executionSuccessful": successful}],
                          "results": results if results is not None else [{"kind": "pass"}]}]}

    def test_analyse_complete_acceptee(self):
        module.validate_report(self.report())

    def test_rapport_absent_refuse(self):
        with self.assertRaises(ValueError):
            module.validate_report({})

    def test_analyse_incomplete_refusee_meme_avec_controles_reussis(self):
        with self.assertRaises(ValueError):
            module.validate_report(self.report(successful=False))

    def test_zero_controle_refuse(self):
        with self.assertRaises(ValueError):
            module.validate_report(self.report(results=[]))

    def test_echec_refuse(self):
        with self.assertRaises(ValueError):
            module.validate_report(self.report(results=[{"kind": "pass"}, {"kind": "fail", "ruleId": "BA2001"}]))
