from datetime import date
import io
import json
import unittest
from email.message import Message
from unittest import mock
from urllib.error import HTTPError, URLError
from urllib.request import Request

from azerty_store_analytics import client
from azerty_store_analytics.client import build_params, effective_start_date
from azerty_store_analytics.config import DATASETS


class _Reponse(io.BytesIO):
    def __enter__(self):
        return self

    def __exit__(self, *exc):
        self.close()
        return False


def _ok(payload):
    return _Reponse(json.dumps(payload).encode("utf-8"))


def _http(code, retry_after=None):
    headers = Message()
    if retry_after is not None:
        headers["Retry-After"] = retry_after
    return HTTPError("https://exemple.test", code, "x", headers, io.BytesIO(b"corps"))


class RetryTests(unittest.TestCase):
    """Le 2026-09-29, un TimeoutError de lecture a perdu `health_detail` sans nouvel essai."""

    def appeler(self, *effets, retries=4):
        attentes = []
        with mock.patch.object(client, "urlopen", side_effect=list(effets)) as urlopen:
            try:
                resultat = client._http_json(
                    Request("https://exemple.test"), retries=retries, sleep=attentes.append
                )
            finally:
                self.appels = urlopen.call_count
        return resultat, attentes

    def test_delai_de_lecture_depasse_est_reessaye(self):
        resultat, attentes = self.appeler(TimeoutError("The read operation timed out"), _ok({"Value": [1]}))
        self.assertEqual({"Value": [1]}, resultat)
        self.assertEqual([5.0], attentes)

    def test_erreurs_reseau_et_5xx_attendent_de_plus_en_plus(self):
        _, attentes = self.appeler(URLError("reset"), ConnectionResetError(), _http(503), _ok({}))
        self.assertEqual([5.0, 10.0, 20.0], attentes)

    def test_quota_429_respecte_retry_after(self):
        _, attentes = self.appeler(_http(429, "42"), _ok({}))
        self.assertEqual([42.0], attentes)

    def test_retry_after_borne(self):
        self.assertEqual(300.0, client.backoff_delay(0, "86400"))
        self.assertEqual(60.0, client.backoff_delay(10))

    def test_erreur_de_requete_non_reessayee(self):
        with self.assertRaisesRegex(client.StoreAnalyticsError, "HTTP 400"):
            self.appeler(_http(400), _ok({}))
        self.assertEqual(1, self.appels)

    def test_reseau_toujours_en_panne_leve_un_message_clair(self):
        with self.assertRaisesRegex(client.StoreAnalyticsError, "après 3 tentatives: The read operation timed out"):
            self.appeler(*[TimeoutError("The read operation timed out")] * 3, retries=3)
        self.assertEqual(3, self.appels)


class ClientConfigurationTests(unittest.TestCase):
    def test_retention_window_is_applied(self) -> None:
        self.assertEqual(
            effective_start_date(date(2015, 1, 1), date(2026, 7, 28), 30),
            date(2026, 6, 29),
        )

    def test_unlimited_dataset_keeps_requested_start(self) -> None:
        self.assertEqual(
            effective_start_date(date(2015, 1, 1), date(2026, 7, 28), None),
            date(2015, 1, 1),
        )

    def test_store_id_and_groupby_are_encoded_in_params(self) -> None:
        dataset = next(item for item in DATASETS if item.name == "installs_detail")
        params = build_params(
            dataset,
            "9N4BTS43SSSZ",
            date(2026, 5, 1),
            date(2026, 7, 28),
        )
        self.assertEqual(params["applicationId"], "9N4BTS43SSSZ")
        self.assertIn("packageVersion", params["groupby"])
        self.assertEqual(params["top"], 10_000)

    def test_insights_omits_unsupported_pagination_params(self) -> None:
        dataset = next(item for item in DATASETS if item.name == "insights")
        params = build_params(
            dataset,
            "9N4BTS43SSSZ",
            date(2026, 5, 1),
            date(2026, 7, 28),
        )
        self.assertNotIn("top", params)
        self.assertNotIn("skip", params)


if __name__ == "__main__":
    unittest.main()
