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
                          "results": results if results is not None else [{"kind": "pass", "message": {"text": "Contrôle réussi"}}]}]}

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

    def test_message_par_identifiant_seul_refuse_pour_github(self):
        with self.assertRaisesRegex(ValueError, "Message SARIF"):
            module.validate_report(self.report(results=[{"kind": "pass", "message": {"id": "Pass", "arguments": ["app.exe"]}}]))

    def test_texte_resolu_sans_changer_un_verdict_en_echec(self):
        result = {"ruleId": "BA1234", "kind": "fail", "level": "error",
                  "message": {"id": "Error", "arguments": ["app.exe"]}}
        report = self.report(results=[result])
        report["runs"][0]["tool"] = {"driver": {"rules": [{"id": "BA1234", "messageStrings": {"Error": {"text": "Échec pour '{0}'."}}}]}}
        module.inline_result_messages(report)
        self.assertEqual("Échec pour 'app.exe'.", result["message"]["text"])
        self.assertEqual("fail", result["kind"])
        self.assertEqual("error", result["level"])
        self.assertEqual("Error", result["message"]["id"])

    def test_modele_absent_refuse_sans_inventer_un_message(self):
        with self.assertRaisesRegex(ValueError, "Modèle SARIF introuvable"):
            module.inline_result_messages(self.report(results=[{"kind": "pass", "message": {"id": "Missing"}}]))

    def test_kind_absent_signifie_echec_meme_avec_un_autre_controle_reussi(self):
        for level in (None, "warning"):
            with self.subTest(level=level), self.assertRaisesRegex(ValueError, "Contrôles en échec"):
                result = {"ruleId": "BA2001", "message": {"text": "Alerte"}}
                if level is not None:
                    result["level"] = level
                module.validate_report(self.report(results=[{"kind": "pass"}, result]))

    def common_template_report(self, texts):
        result = {"ruleId": "BA4002", "kind": "notApplicable", "level": "none",
                  "message": {"id": "NotApplicable_InvalidMetadata", "arguments": ["app.exe", "ELF", "PE"]}}
        report = self.report(results=[result])
        rules = [{"id": "BA4002"}] + [
            {"id": f"BA{i}", "messageStrings": {"NotApplicable_InvalidMetadata": {"text": text}}}
            for i, text in enumerate(texts)]
        report["runs"][0]["tool"] = {"driver": {"rules": rules}}
        return report

    def test_ba4002_reutilise_le_modele_commun_identique_fourni_par_binskim(self):
        report = self.common_template_report(["{0} / {1} / {2}", "{0} / {1} / {2}"])
        module.inline_result_messages(report)
        result = report["runs"][0]["results"][0]
        self.assertEqual("app.exe / ELF / PE", result["message"]["text"])
        self.assertEqual("notApplicable", result["kind"])

    def test_ba4002_refuse_des_modeles_communs_ambigus(self):
        with self.assertRaisesRegex(ValueError, "Modèle SARIF introuvable"):
            module.inline_result_messages(self.common_template_report(["{0}", "Autre {0}"]))
