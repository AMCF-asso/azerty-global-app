from __future__ import annotations

import json
import os
from collections import defaultdict
from datetime import date
from pathlib import Path
from typing import Any
from urllib.parse import quote
from urllib.request import Request, urlopen

from .config import METRICS


def _record_date(record: dict[str, Any]) -> date | None:
    raw = record.get("date") or record.get("insightDate")
    if not raw:
        return None
    try:
        return date.fromisoformat(str(raw)[:10])
    except ValueError:
        return None


def _in_period(
    record: dict[str, Any],
    start_date: str | None,
    end_date: str | None,
) -> bool:
    row_date = _record_date(record)
    if row_date is None:
        return True
    if start_date and row_date < date.fromisoformat(start_date):
        return False
    if end_date and row_date > date.fromisoformat(end_date):
        return False
    return True


class SnapshotSource:
    def __init__(self, location: str | None = None, sas_token: str | None = None):
        self.location = (
            location
            or os.environ.get("STORE_ANALYTICS_SOURCE")
            or "store-analytics/out/latest"
        ).rstrip("/")
        self.sas_token = (
            sas_token
            if sas_token is not None
            else os.environ.get("STORE_ANALYTICS_SAS_TOKEN", "")
        ).lstrip("?")
        self._cache: dict[str, dict[str, Any]] = {}

    @property
    def remote(self) -> bool:
        return self.location.startswith(("https://", "http://"))

    def load(self, filename: str) -> dict[str, Any]:
        if filename in self._cache:
            return self._cache[filename]
        if self.remote:
            url = f"{self.location}/{quote(filename)}"
            if self.sas_token:
                url = f"{url}?{self.sas_token}"
            request = Request(
                url,
                headers={"User-Agent": "AZERTYGlobal-StoreAnalytics-MCP/0.1"},
            )
            with urlopen(request, timeout=30) as response:
                payload = json.loads(response.read().decode("utf-8-sig"))
        else:
            payload = json.loads(
                (Path(self.location) / filename).read_text(encoding="utf-8")
            )
        if not isinstance(payload, dict):
            raise ValueError(f"{filename} ne contient pas un objet JSON")
        self._cache[filename] = payload
        return payload

    def records(self, dataset: str) -> list[dict[str, Any]]:
        payload = self.load(f"{dataset}.json")
        rows = payload.get("records", [])
        if not isinstance(rows, list):
            raise ValueError(f"records absent ou invalide dans {dataset}")
        return [row for row in rows if isinstance(row, dict)]


class AnalyticsQueries:
    def __init__(self, source: SnapshotSource | None = None):
        self.source = source or SnapshotSource()

    def available_datasets(self) -> dict[str, Any]:
        return self.source.load("manifest.json")

    def timeseries(
        self,
        metric: str,
        start_date: str | None = None,
        end_date: str | None = None,
    ) -> list[dict[str, Any]]:
        if metric not in METRICS:
            raise ValueError(f"Métrique inconnue: {metric}")
        dataset, field = METRICS[metric]
        totals: dict[str, float] = defaultdict(float)
        for row in self.source.records(dataset):
            if not _in_period(row, start_date, end_date):
                continue
            row_date = _record_date(row)
            if row_date is None:
                continue
            value = row.get(field)
            if isinstance(value, (int, float)):
                totals[row_date.isoformat()] += float(value)
        return [
            {"date": day, "value": int(value) if value.is_integer() else value}
            for day, value in sorted(totals.items())
        ]

    def summary(
        self,
        start_date: str | None = None,
        end_date: str | None = None,
    ) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for metric in METRICS:
            series = self.timeseries(metric, start_date, end_date)
            result[metric] = sum(item["value"] for item in series)
        result["period"] = {"start": start_date, "end": end_date}
        result["warning"] = (
            "Les utilisateurs actifs et appareils actifs sont des métriques "
            "de période; ne pas interpréter leur somme comme un nombre unique."
        )
        return result

    def breakdown(
        self,
        metric: str,
        dimension: str,
        start_date: str | None = None,
        end_date: str | None = None,
        limit: int = 20,
    ) -> list[dict[str, Any]]:
        detail_map = {
            "acquisitions": ("acquisitions_detail", "acquisitionQuantity"),
            "installs": ("installs_detail", "successfulInstallCount"),
            "store_clicks": ("channels_detail", "clickCount"),
            "store_conversions": ("channels_detail", "conversionCount"),
            "health_events": ("health_detail", "eventCount"),
        }
        if metric not in detail_map:
            raise ValueError(
                "Les ventilations sont disponibles pour acquisitions, installs, "
                "store_clicks, store_conversions et health_events"
            )
        dataset, field = detail_map[metric]
        totals: dict[str, float] = defaultdict(float)
        for row in self.source.records(dataset):
            if not _in_period(row, start_date, end_date):
                continue
            key = str(row.get(dimension, "Unknown") or "Unknown")
            value = row.get(field)
            if isinstance(value, (int, float)):
                totals[key] += float(value)
        ordered = sorted(totals.items(), key=lambda item: item[1], reverse=True)
        return [
            {
                dimension: key,
                "value": int(value) if value.is_integer() else value,
            }
            for key, value in ordered[: max(1, min(limit, 100))]
        ]

    def reviews(
        self,
        limit: int = 20,
        market: str | None = None,
        minimum_rating: int | None = None,
    ) -> list[dict[str, Any]]:
        rows = self.source.records("reviews")
        filtered = [
            row
            for row in rows
            if (not market or str(row.get("market", "")).upper() == market.upper())
            and (
                minimum_rating is None
                or int(row.get("rating", 0) or 0) >= minimum_rating
            )
        ]
        filtered.sort(key=lambda row: str(row.get("date", "")), reverse=True)
        return filtered[: max(1, min(limit, 100))]

    def health(
        self,
        start_date: str | None = None,
        end_date: str | None = None,
        limit: int = 20,
    ) -> list[dict[str, Any]]:
        rows = [
            row
            for row in self.source.records("health_detail")
            if _in_period(row, start_date, end_date)
        ]
        rows.sort(
            key=lambda row: float(row.get("eventCount", 0) or 0),
            reverse=True,
        )
        return rows[: max(1, min(limit, 100))]

    def insights(self) -> list[dict[str, Any]]:
        return self.source.records("insights")
