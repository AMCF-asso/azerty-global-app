from __future__ import annotations

import json
import time
from dataclasses import dataclass
from datetime import date, timedelta
from typing import Any
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode, urljoin
from urllib.request import Request, urlopen

from .config import Dataset


TOKEN_URL = "https://login.microsoftonline.com/{tenant_id}/oauth2/token"
API_BASE = "https://manage.devcenter.microsoft.com/v1.0/my/analytics/"


class StoreAnalyticsError(RuntimeError):
    pass


def _http_json(
    request: Request,
    *,
    timeout: int = 60,
    retries: int = 3,
) -> dict[str, Any]:
    for attempt in range(retries):
        try:
            with urlopen(request, timeout=timeout) as response:
                return json.loads(response.read().decode("utf-8-sig"))
        except HTTPError as error:
            body = error.read().decode("utf-8", errors="replace")
            if error.code not in {429, 500, 502, 503, 504} or attempt == retries - 1:
                raise StoreAnalyticsError(
                    f"HTTP {error.code} pour {request.full_url}: {body[:1000]}"
                ) from error
            retry_after = error.headers.get("Retry-After")
            delay = int(retry_after) if retry_after and retry_after.isdigit() else 2**attempt
            time.sleep(delay)
        except URLError as error:
            if attempt == retries - 1:
                raise StoreAnalyticsError(
                    f"Erreur réseau pour {request.full_url}: {error.reason}"
                ) from error
            time.sleep(2**attempt)
    raise AssertionError("boucle de retry impossible")


def obtain_access_token(
    tenant_id: str,
    client_id: str,
    client_secret: str,
) -> str:
    body = urlencode(
        {
            "grant_type": "client_credentials",
            "client_id": client_id,
            "client_secret": client_secret,
            "resource": "https://manage.devcenter.microsoft.com",
        }
    ).encode("ascii")
    request = Request(
        TOKEN_URL.format(tenant_id=tenant_id),
        data=body,
        headers={"Content-Type": "application/x-www-form-urlencoded"},
        method="POST",
    )
    payload = _http_json(request)
    token = payload.get("access_token")
    if not token:
        raise StoreAnalyticsError("Microsoft Entra n'a renvoyé aucun access_token")
    return str(token)


def effective_start_date(
    requested_start: date,
    end: date,
    max_lookback_days: int | None,
) -> date:
    if max_lookback_days is None:
        return requested_start
    retained_start = end - timedelta(days=max_lookback_days - 1)
    return max(requested_start, retained_start)


def build_params(
    dataset: Dataset,
    store_id: str,
    start: date,
    end: date,
    *,
    top: int = 10_000,
    skip: int = 0,
) -> dict[str, str | int]:
    effective_start = effective_start_date(start, end, dataset.max_lookback_days)
    params: dict[str, str | int] = {
        "applicationId": store_id,
        "startDate": effective_start.strftime("%m/%d/%Y"),
        "endDate": end.strftime("%m/%d/%Y"),
    }
    if dataset.paginated:
        params["top"] = top
        params["skip"] = skip
    if dataset.groupby:
        params["groupby"] = ",".join(dataset.groupby)
    if dataset.orderby:
        params["orderby"] = dataset.orderby
    if dataset.filter_expression:
        params["filter"] = dataset.filter_expression
    params.update(dataset.extra_params)
    return params


@dataclass
class StoreAnalyticsClient:
    access_token: str
    store_id: str

    def collect_dataset(
        self,
        dataset: Dataset,
        start: date,
        end: date,
    ) -> dict[str, Any]:
        records: list[dict[str, Any]] = []
        skip = 0
        freshness: str | None = None
        params = build_params(dataset, self.store_id, start, end, skip=skip)
        url = f"{API_BASE}{dataset.endpoint}?{urlencode(params)}"

        while True:
            request = Request(
                url,
                headers={
                    "Authorization": f"Bearer {self.access_token}",
                    "Accept": "application/json",
                    "User-Agent": "AZERTYGlobal-StoreAnalytics/0.1",
                },
            )
            payload = _http_json(request)
            page = payload.get("Value", payload.get("value", []))
            if not isinstance(page, list):
                raise StoreAnalyticsError(
                    f"Réponse inattendue pour {dataset.name}: Value n'est pas une liste"
                )
            records.extend(item for item in page if isinstance(item, dict))
            freshness = payload.get("DataFreshnessTimestamp", freshness)
            next_link = payload.get("@nextLink") or payload.get("nextLink")
            total = int(payload.get("TotalCount", payload.get("totalCount", len(records))))

            if next_link:
                url = urljoin(API_BASE, str(next_link))
                continue

            if not dataset.paginated or len(records) >= total or not page:
                break
            skip = len(records)
            params = build_params(dataset, self.store_id, start, end, skip=skip)
            url = f"{API_BASE}{dataset.endpoint}?{urlencode(params)}"

        actual_start = effective_start_date(start, end, dataset.max_lookback_days)
        return {
            "dataset": dataset.name,
            "endpoint": dataset.endpoint,
            "storeId": self.store_id,
            "startDate": actual_start.isoformat(),
            "endDate": end.isoformat(),
            "dataFreshnessTimestamp": freshness,
            "totalCount": len(records),
            "records": records,
        }
