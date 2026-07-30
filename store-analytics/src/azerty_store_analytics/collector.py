from __future__ import annotations

import json
import os
import shutil
import sys
from datetime import date, datetime, timezone
from pathlib import Path
from typing import Any

from .client import StoreAnalyticsClient, obtain_access_token
from .config import DATASETS


def required_env(name: str) -> str:
    value = os.environ.get(name, "").strip()
    if not value:
        raise RuntimeError(f"Variable d'environnement obligatoire absente: {name}")
    return value


def parse_iso_date(value: str, name: str) -> date:
    try:
        return date.fromisoformat(value)
    except ValueError as error:
        raise RuntimeError(f"{name} doit être au format AAAA-MM-JJ") from error


def write_json(path: Path, payload: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    temporary.replace(path)


def main() -> None:
    store_id = os.environ.get("STORE_ID", "9N4BTS43SSSZ").strip()
    start = parse_iso_date(
        os.environ.get("STORE_ANALYTICS_START_DATE", "2015-01-01"),
        "STORE_ANALYTICS_START_DATE",
    )
    end = parse_iso_date(
        os.environ.get("STORE_ANALYTICS_END_DATE", date.today().isoformat()),
        "STORE_ANALYTICS_END_DATE",
    )
    if start > end:
        raise RuntimeError("La date de début doit précéder la date de fin")

    output_root = Path(
        os.environ.get("STORE_ANALYTICS_OUTPUT", "store-analytics/out")
    ).resolve()
    collected_at = datetime.now(timezone.utc)
    run_id = collected_at.strftime("%Y%m%dT%H%M%SZ")
    run_dir = output_root / "runs" / run_id
    latest_dir = output_root / "latest"

    token = obtain_access_token(
        required_env("PARTNER_CENTER_TENANT_ID"),
        required_env("PARTNER_CENTER_CLIENT_ID"),
        required_env("PARTNER_CENTER_CLIENT_SECRET"),
    )
    client = StoreAnalyticsClient(token, store_id)

    datasets: list[dict[str, Any]] = []
    failures: list[dict[str, str]] = []
    for dataset in DATASETS:
        print(f"Collecte {dataset.name}…", file=sys.stderr)
        try:
            payload = client.collect_dataset(dataset, start, end)
            payload["collectedAt"] = collected_at.isoformat()
            write_json(run_dir / f"{dataset.name}.json", payload)
            datasets.append(
                {
                    "name": dataset.name,
                    "file": f"{dataset.name}.json",
                    "records": payload["totalCount"],
                    "startDate": payload["startDate"],
                    "endDate": payload["endDate"],
                }
            )
        except Exception as error:  # le manifeste conserve précisément l'échec
            failures.append({"dataset": dataset.name, "error": str(error)})

    manifest = {
        "schemaVersion": 1,
        "storeId": store_id,
        "collectedAt": collected_at.isoformat(),
        "runId": run_id,
        "requestedStartDate": start.isoformat(),
        "requestedEndDate": end.isoformat(),
        "datasets": datasets,
        "failures": failures,
        "complete": not failures,
    }
    write_json(run_dir / "manifest.json", manifest)

    if latest_dir.exists():
        shutil.rmtree(latest_dir)
    shutil.copytree(run_dir, latest_dir)
    write_json(output_root / "manifest.json", manifest)

    if failures:
        for failure in failures:
            print(
                f"ÉCHEC {failure['dataset']}: {failure['error']}",
                file=sys.stderr,
            )
        raise RuntimeError(
            f"{len(failures)} jeu(x) de données n'ont pas pu être collectés"
        )

    print(
        f"Collecte complète: {len(datasets)} jeux de données, run {run_id}",
        file=sys.stderr,
    )


if __name__ == "__main__":
    main()
