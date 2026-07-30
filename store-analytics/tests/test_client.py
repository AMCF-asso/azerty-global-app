from datetime import date
import unittest

from azerty_store_analytics.client import build_params, effective_start_date
from azerty_store_analytics.config import DATASETS


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
