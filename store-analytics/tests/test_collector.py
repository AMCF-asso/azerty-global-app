import contextlib
import io
import json
import os
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from azerty_store_analytics import collector
from azerty_store_analytics.client import StoreAnalyticsError
from azerty_store_analytics.config import DATASETS


class CollectorExitTests(unittest.TestCase):
    """Un jeu manquant rend 2 après écriture des autres ; le workflow archive puis signale."""

    def lancer(self, echoue=()):
        def collecter(_client, dataset, start, end):
            if dataset.name in echoue:
                raise StoreAnalyticsError("The read operation timed out")
            return {"totalCount": 0, "records": [], "startDate": start.isoformat(), "endDate": end.isoformat()}

        with tempfile.TemporaryDirectory() as dossier:
            env = {
                "STORE_ANALYTICS_OUTPUT": dossier,
                "STORE_ANALYTICS_START_DATE": "2026-09-01",
                "STORE_ANALYTICS_END_DATE": "2026-09-29",
                "PARTNER_CENTER_TENANT_ID": "t",
                "PARTNER_CENTER_CLIENT_ID": "c",
                "PARTNER_CENTER_CLIENT_SECRET": "s",
            }
            with mock.patch.dict(os.environ, env, clear=False), \
                    mock.patch.dict(os.environ, {"GITHUB_STEP_SUMMARY": ""}), \
                    mock.patch.object(collector, "obtain_access_token", return_value="jeton"), \
                    mock.patch.object(collector.StoreAnalyticsClient, "collect_dataset", collecter), \
                    contextlib.redirect_stdout(io.StringIO()), \
                    contextlib.redirect_stderr(io.StringIO()) as erreurs:
                code = collector.main()
            manifeste = json.loads((Path(dossier) / "manifest.json").read_text(encoding="utf-8"))
            ecrits = {p.name for p in (Path(dossier) / "latest").glob("*.json")}
        return code, manifeste, ecrits, erreurs.getvalue()

    def test_collecte_complete_rend_zero(self):
        code, manifeste, ecrits, _ = self.lancer()
        self.assertEqual(0, code)
        self.assertTrue(manifeste["complete"])
        self.assertEqual(len(DATASETS), len(manifeste["datasets"]))

    def test_jeu_manquant_rend_deux_et_garde_les_autres(self):
        code, manifeste, ecrits, erreurs = self.lancer(echoue={"health_detail"})
        self.assertEqual(collector.INCOMPLETE, code)
        self.assertFalse(manifeste["complete"])
        self.assertEqual(["health_detail"], [f["dataset"] for f in manifeste["failures"]])
        self.assertIn("installs_total.json", ecrits)
        self.assertNotIn("health_detail.json", ecrits)
        self.assertIn("Collecte incomplète: 1 jeu(x) manquant(s)", erreurs)


if __name__ == "__main__":
    unittest.main()
