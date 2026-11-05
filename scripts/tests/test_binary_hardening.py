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

    def test_plusieurs_analyses_dans_un_fichier_refusees(self):
        report = self.report()
        report["runs"].append(report["runs"][0])
        with self.assertRaisesRegex(ValueError, "une seule par fichier"):
            module.validate_report(report)

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

    def common_template_report(self, texts, rule="BA4002"):
        result = {"ruleId": rule, "kind": "notApplicable", "level": "none",
                  "message": {"id": "NotApplicable_InvalidMetadata", "arguments": ["app.exe", "ELF", "PE"]}}
        report = self.report(results=[result])
        rules = [{"id": rule}] + [
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

    def test_ba2027_non_applicable_au_pdb_rust_reutilise_le_modele_commun(self):
        report = self.common_template_report(["{0} / {1} / {2}", "{0} / {1} / {2}"], rule="BA2027")
        module.inline_result_messages(report)
        self.assertEqual("app.exe / ELF / PE", report["runs"][0]["results"][0]["message"]["text"])

    def test_modele_commun_jamais_emprunte_pour_une_autre_regle(self):
        with self.assertRaisesRegex(ValueError, "Modèle SARIF introuvable"):
            module.inline_result_messages(self.common_template_report(["{0}", "{0}"], rule="BA2008"))


class DerogationTests(unittest.TestCase):
    """Arbitrage du 2026-09-22 : une dérogation n'existe que si sa condition est prouvée."""

    SPECTRE = ("The following modules were compiled with a toolset that supports /Qspectre but the switch was not enabled on the command-line:\r\n"
               "bootstrapper.obj,cxx,19.44.35228.0 (bootstrapper.obj)\r\n"
               "LIBCMT.lib,c,19.44.35228.0 (a.obj,b.obj)\r\n")

    def warning(self, rule, arguments=None):
        return {"ruleId": rule, "kind": "fail", "level": "warning",
                "message": {"text": "Alerte " + rule, "arguments": arguments or ["app.exe"]}}

    def report(self, *failures):
        return {"runs": [{"invocations": [{"executionSuccessful": True}],
                          "results": [{"kind": "pass", "message": {"text": "ok"}}, *failures]}]}

    def spectre(self, text=None):
        return self.warning("BA2024", ["app.exe", text or self.SPECTRE])

    def test_quatre_regles_acceptees_quand_leurs_conditions_tiennent(self):
        report = self.report(self.spectre(), self.warning("BA2026"), self.warning("BA2027"), self.warning("BA6006"))
        accepted = module.validate_report(report, {"cxx_objects": 3})
        self.assertEqual(["BA2024", "BA2026", "BA2027", "BA6006"], [a["rule"] for a in accepted])

    def test_sans_contexte_aucune_derogation(self):
        with self.assertRaisesRegex(ValueError, "BA2027"):
            module.validate_report(self.report(self.warning("BA2027")))

    def test_cet_jamais_deroge(self):
        with self.assertRaisesRegex(ValueError, "BA2025"):
            module.validate_report(self.report(self.warning("BA2025")), {"cxx_objects": 3})

    def test_ba2014_deroge_pour_la_dll_science_seule(self):
        result = self.warning("BA2014", ["science_ffi.dll"])
        result["level"] = "error"
        accepted = module.validate_report(self.report(result), {"cxx_objects": 3, "binary": "science_ffi.dll"})
        self.assertEqual(["BA2014"], [a["rule"] for a in accepted])
        self.assertIn("rustc stable", accepted[0]["justification"])

    def test_ba2014_jamais_deroge_pour_l_exe(self):
        result = self.warning("BA2014")
        result["level"] = "error"
        for context in ({"cxx_objects": 3, "binary": "AZERTY Global.exe"}, {"cxx_objects": 3}):
            with self.assertRaisesRegex(ValueError, "BA2014"):
                module.validate_report(self.report(result), context)

    CRT = ("The following modules were compiled with a toolset that supports /Qspectre but the switch was not enabled on the command-line:\r\n"
           "libcmt.lib,c,19.44.35207.1 (a.obj,b.obj)\r\n"
           "libvcruntime.lib,cxx,19.44.35207.1 (c.obj)\r\n")
    LOCK = ('version = 4\n\n[[package]]\nname = "science-core"\nversion = "0.1.0"\n\n'
            '[[package]]\nname = "science-ffi"\nversion = "0.1.0"\ndependencies = [\n "science-core",\n]\n')

    def dll(self, lock=LOCK):
        return {"cxx_objects": 197, "binary": "science_ffi.dll", "cargo_lock": lock}

    def test_ba2024_deroge_pour_la_dll_quand_crt_seul_et_lock_du_depot(self):
        accepted = module.validate_report(self.report(self.warning("BA2024", ["science_ffi.dll", self.CRT])), self.dll())
        self.assertEqual(["BA2024"], [a["rule"] for a in accepted])
        self.assertIn("Cargo.lock", accepted[0]["justification"])

    def test_ba2024_dll_refuse_un_module_hors_crt(self):
        autre = self.CRT + "zlib.lib,c,19.44.35207.1 (inflate.obj)\r\n"
        with self.assertRaisesRegex(ValueError, "BA2024"):
            module.validate_report(self.report(self.warning("BA2024", ["science_ffi.dll", autre])), self.dll())

    def test_ba2024_dll_refuse_une_crate_externe_au_lock(self):
        externe = self.LOCK + ('\n[[package]]\nname = "cc"\nversion = "1.2.0"\n'
                               'source = "registry+https://github.com/rust-lang/crates.io-index"\n')
        with self.assertRaisesRegex(ValueError, "BA2024"):
            module.validate_report(self.report(self.warning("BA2024", ["science_ffi.dll", self.CRT])), self.dll(externe))

    def test_ba2024_dll_refuse_sans_lock(self):
        for lock in (None, "version = 4\n"):
            with self.assertRaisesRegex(ValueError, "BA2024"):
                module.validate_report(self.report(self.warning("BA2024", ["science_ffi.dll", self.CRT])), self.dll(lock))

    def test_ba2024_exe_garde_la_preuve_d_egalite(self):
        with self.assertRaisesRegex(ValueError, "BA2024"):
            module.validate_report(self.report(self.warning("BA2024", ["app.exe", self.CRT])),
                                   {"cxx_objects": 197, "binary": "AZERTY Global.exe", "cargo_lock": self.LOCK})

    def test_dll_science_n_elargit_pas_les_autres_regles(self):
        with self.assertRaisesRegex(ValueError, "BA2025"):
            module.validate_report(self.report(self.warning("BA2025", ["science_ffi.dll"])),
                                   {"cxx_objects": 3, "binary": "science_ffi.dll"})

    def test_niveau_erreur_jamais_deroge(self):
        result = self.warning("BA6006")
        result["level"] = "error"
        with self.assertRaisesRegex(ValueError, "BA6006"):
            module.validate_report(self.report(result), {"cxx_objects": 3})

    def test_spectre_refuse_si_une_bibliotheque_n_est_pas_microsoft(self):
        text = self.SPECTRE + "maLib.lib,cxx,19.44.35228.0 (c.obj)\r\n"
        with self.assertRaisesRegex(ValueError, "BA2024"):
            module.validate_report(self.report(self.spectre(text)), {"cxx_objects": 4})

    def test_spectre_et_sdl_refuses_si_le_compte_d_objets_differe(self):
        with self.assertRaisesRegex(ValueError, "BA2024, BA2026"):
            module.validate_report(self.report(self.spectre(), self.warning("BA2026")), {"cxx_objects": 4})

    def test_sdl_refuse_sans_liste_spectre_qui_l_atteste(self):
        with self.assertRaisesRegex(ValueError, "BA2026"):
            module.validate_report(self.report(self.warning("BA2026")), {"cxx_objects": 3})

    def with_rules(self, report, **levels):
        report["runs"][0]["tool"] = {"driver": {"rules": [
            {"id": rule, "defaultConfiguration": {"level": level}} for rule, level in levels.items()]}}
        return report

    def test_niveau_herite_de_la_regle_warning_accepte(self):
        result = self.warning("BA2027")
        del result["level"]
        report = self.with_rules(self.report(result), BA2027="warning")
        self.assertEqual(["BA2027"], [a["rule"] for a in module.validate_report(report, {"cxx_objects": 3})])

    def test_niveau_herite_de_la_regle_error_refuse(self):
        result = self.warning("BA2027")
        del result["level"]
        report = self.with_rules(self.report(result), BA2027="error")
        with self.assertRaisesRegex(ValueError, "BA2027"):
            module.validate_report(report, {"cxx_objects": 3})

    def test_controle_reussi_d_une_regle_error_reste_reussi(self):
        report = self.with_rules(self.report({"ruleId": "BA2008", "kind": "pass", "message": {"text": "ok"}}), BA2008="error")
        self.assertEqual([], module.validate_report(report, {"cxx_objects": 3}))

    def test_ligne_spectre_illisible_refusee(self):
        with self.assertRaisesRegex(ValueError, "illisible"):
            module.validate_report(self.report(self.spectre("entete\r\nligne sans format\r\n")), {"cxx_objects": 3})
