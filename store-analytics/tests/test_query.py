import json
import tempfile
from pathlib import Path
import unittest

from azerty_store_analytics.query import AnalyticsQueries, SnapshotSource


class QueryTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        (self.root / "manifest.json").write_text(
            json.dumps({"complete": True, "datasets": []}),
            encoding="utf-8",
        )
        (self.root / "installs_total.json").write_text(
            json.dumps(
                {
                    "records": [
                        {"date": "2026-07-01", "successfulInstallCount": 3},
                        {"date": "2026-07-02", "successfulInstallCount": 5},
                    ]
                }
            ),
            encoding="utf-8",
        )
        (self.root / "installs_detail.json").write_text(
            json.dumps(
                {
                    "records": [
                        {
                            "date": "2026-07-01",
                            "market": "FR",
                            "successfulInstallCount": 2,
                        },
                        {
                            "date": "2026-07-01",
                            "market": "BE",
                            "successfulInstallCount": 1,
                        },
                        {
                            "date": "2026-07-02",
                            "market": "FR",
                            "successfulInstallCount": 5,
                        },
                    ]
                }
            ),
            encoding="utf-8",
        )
        self.queries = AnalyticsQueries(SnapshotSource(str(self.root)))

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def test_timeseries_filters_period(self) -> None:
        self.assertEqual(
            self.queries.timeseries("installs", "2026-07-02", "2026-07-02"),
            [{"date": "2026-07-02", "value": 5}],
        )

    def test_breakdown_orders_descending(self) -> None:
        self.assertEqual(
            self.queries.breakdown("installs", "market"),
            [{"market": "FR", "value": 7}, {"market": "BE", "value": 1}],
        )


if __name__ == "__main__":
    unittest.main()
