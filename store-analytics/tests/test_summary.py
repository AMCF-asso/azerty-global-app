import json
import os
import tempfile
import unittest
from pathlib import Path

from azerty_store_analytics import summary


MANIFEST = {
    "storeId": "9N4BTS43SSSZ",
    "runId": "20260920T094543Z",
    "collectedAt": "2026-09-20T09:45:43+00:00",
    "complete": True,
}


class BuildTotalsTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.latest = self.root / "latest"
        self.latest.mkdir()

    def write(self, dataset: str, records: list) -> None:
        (self.latest / f"{dataset}.json").write_text(
            json.dumps({"records": records}), encoding="utf-8"
        )

    def test_somme_et_plage_de_dates(self) -> None:
        self.write(
            "acquisitions_total",
            [
                {"date": "2026-03-24", "acquisitionQuantity": 4},
                {"date": "2026-03-25", "acquisitionQuantity": 1},
            ],
        )
        self.write("installs_total", [{"date": "2026-03-24", "successfulInstallCount": 7}])
        totals = summary.build_totals(self.root, MANIFEST)
        self.assertEqual(totals["metrics"]["acquisitions"]["total"], 5)
        self.assertEqual(totals["metrics"]["acquisitions"]["firstDate"], "2026-03-24")
        self.assertEqual(totals["metrics"]["acquisitions"]["lastDate"], "2026-03-25")
        self.assertEqual(totals["metrics"]["installs"]["total"], 7)

    def test_records_dans_le_desordre(self) -> None:
        """⚠️ `installs_total` arrive antichronologique : min/max, pas premier/dernier."""
        self.write("acquisitions_total", [])
        self.write(
            "installs_total",
            [
                {"date": "2026-08-31", "successfulInstallCount": 11},
                {"date": "2026-03-23", "successfulInstallCount": 2},
            ],
        )
        installs = summary.build_totals(self.root, MANIFEST)["metrics"]["installs"]
        self.assertEqual(installs["firstDate"], "2026-03-23")
        self.assertEqual(installs["lastDate"], "2026-08-31")
        self.assertEqual(installs["total"], 13)

    def test_jeu_manquant_rend_none_sans_lever(self) -> None:
        self.write("acquisitions_total", [{"date": "2026-03-24", "acquisitionQuantity": 4}])
        totals = summary.build_totals(self.root, MANIFEST)
        self.assertIsNone(totals["metrics"]["installs"])
        self.assertEqual(totals["metrics"]["acquisitions"]["total"], 4)

    def test_valeurs_nulles_ignorees(self) -> None:
        self.write(
            "acquisitions_total",
            [
                {"date": "2026-03-24", "acquisitionQuantity": None},
                {"date": "2026-03-25", "acquisitionQuantity": 3},
            ],
        )
        self.write("installs_total", [])
        acquisitions = summary.build_totals(self.root, MANIFEST)["metrics"]["acquisitions"]
        self.assertEqual(acquisitions["total"], 3)

    def test_manifeste_incomplet_est_reporte(self) -> None:
        """Le drapeau doit survivre : c'est lui qui fait refuser un cumul sous-évalué."""
        self.write("acquisitions_total", [])
        self.write("installs_total", [])
        totals = summary.build_totals(self.root, {**MANIFEST, "complete": False})
        self.assertFalse(totals["complete"])


class ExtractTests(unittest.TestCase):
    """Aller-retour émission → journal → relecture.

    ⛔ Témoin négatif obligatoire : un journal sans bloc doit lever, sinon
    `fetch_totals.py` publierait un chiffre venu de nulle part.
    """

    def test_journal_prefixe_par_le_runner(self) -> None:
        payload = {"complete": True, "metrics": {"acquisitions": {"total": 1050}}}
        log = "\n".join(
            [
                "collect\tUNKNOWN STEP\t2026-09-20T09:46:00.1234567Z Collecte terminée",
                f"collect\tUNKNOWN STEP\t2026-09-20T09:46:01.1234567Z {summary.BEGIN_MARKER}",
                "collect\tUNKNOWN STEP\t2026-09-20T09:46:01.2234567Z "
                + json.dumps(payload, ensure_ascii=False, sort_keys=True),
                f"collect\tUNKNOWN STEP\t2026-09-20T09:46:01.3234567Z {summary.END_MARKER}",
                "collect\tUNKNOWN STEP\t2026-09-20T09:46:02.1234567Z Post job cleanup.",
            ]
        )
        self.assertEqual(summary.extract(log)["metrics"]["acquisitions"]["total"], 1050)

    def test_journal_sans_prefixe(self) -> None:
        payload = {"complete": True, "metrics": {}}
        log = "\n".join(
            [summary.BEGIN_MARKER, json.dumps(payload), summary.END_MARKER]
        )
        self.assertEqual(summary.extract(log), payload)

    def test_dernier_bloc_gagne(self) -> None:
        """L'archive de journaux contient l'étape et sa copie à plat : deux blocs."""
        old = json.dumps({"complete": True, "metrics": {"a": 1}})
        new = json.dumps({"complete": True, "metrics": {"a": 2}})
        log = "\n".join(
            [
                summary.BEGIN_MARKER,
                old,
                summary.END_MARKER,
                summary.BEGIN_MARKER,
                new,
                summary.END_MARKER,
            ]
        )
        self.assertEqual(summary.extract(log)["metrics"]["a"], 2)

    def test_journal_sans_bloc_leve(self) -> None:
        with self.assertRaises(ValueError):
            summary.extract("collect\tUNKNOWN STEP\t2026-09-20T09:46:02Z rien ici")

    def test_bloc_ouvert_jamais_ferme_leve(self) -> None:
        with self.assertRaises(ValueError):
            summary.extract(summary.BEGIN_MARKER + "\n{}")


class EmitTests(unittest.TestCase):
    def test_resume_de_job_ecrit_quand_la_variable_existe(self) -> None:
        totals = {
            "runId": "20260920T094543Z",
            "complete": True,
            "metrics": {
                "acquisitions": {
                    "total": 1050,
                    "firstDate": "2026-03-24",
                    "lastDate": "2026-08-31",
                },
                "installs": None,
            },
        }
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "summary.md"
            previous = os.environ.get("GITHUB_STEP_SUMMARY")
            os.environ["GITHUB_STEP_SUMMARY"] = str(path)
            try:
                summary.emit(totals)
            finally:
                if previous is None:
                    os.environ.pop("GITHUB_STEP_SUMMARY", None)
                else:
                    os.environ["GITHUB_STEP_SUMMARY"] = previous
            written = path.read_text(encoding="utf-8")
        self.assertIn("| Acquisitions | 1050 | 2026-03-24 → 2026-08-31 |", written)
        self.assertIn("| Installations réussies | indisponible | — |", written)

    def test_sans_variable_rien_n_est_ecrit_et_rien_ne_leve(self) -> None:
        previous = os.environ.pop("GITHUB_STEP_SUMMARY", None)
        try:
            summary.emit({"runId": "x", "complete": True, "metrics": {}})
        finally:
            if previous is not None:
                os.environ["GITHUB_STEP_SUMMARY"] = previous


if __name__ == "__main__":
    unittest.main()
