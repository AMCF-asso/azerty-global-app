"""Totaux agrégés du Store, publiés hors de l'archive privée.

Pourquoi ce module existe : le commit `1013d2b` du 2026-09-05 (« Keep Store
analytics archives in private storage only ») a supprimé l'artefact GitHub du
workflow. C'était la seule source de `tools/refresh_snapshot.py`, donc la copie
locale est restée figée au run du 2026-09-02 pendant que la pipeline, elle,
continuait de tourner tous les jours sans rien casser. Panne muette : le
workflow au vert, la donnée locale périmée de deux semaines, et le site qui
affichait toujours un chiffre de l'époque d'avant.

⛔ Ce module ne rouvre pas l'archive. Il ne publie que des **totaux agrégés** —
des sommes sur toutes les dates, sans marché, sans appareil, sans version, sans
aucune ligne de détail. Le contrat de la décision du 2026-09-05 tient : les
archives restent dans le conteneur privé, seuls les cumuls en sortent.

Deux sorties, la même donnée :

- `out/latest/totals.json`, archivé avec le reste ;
- un bloc délimité sur **stdout**, encadré par `STORE_TOTALS_BEGIN` et
  `STORE_TOTALS_END`, que `tools/fetch_totals.py` relit dans le journal du run
  par l'API GitHub. Les journaux sont conservés 90 jours, contre 14 pour
  l'ancien artefact.

⚠️ Délimiteurs, pas expression régulière sur le JSON : un journal Actions
préfixe chaque ligne d'un horodatage et peut la découper, donc on borne le bloc
et on recolle ce qui se trouve entre les deux marqueurs.
"""

from __future__ import annotations

import json
import os
import sys
from pathlib import Path
from typing import Any

BEGIN_MARKER = "STORE_TOTALS_BEGIN"
END_MARKER = "STORE_TOTALS_END"

# (clé publiée, fichier de totaux, champ à sommer, libellé humain)
CUMULATIVE: tuple[tuple[str, str, str, str], ...] = (
    ("acquisitions", "acquisitions_total", "acquisitionQuantity", "Acquisitions"),
    ("installs", "installs_total", "successfulInstallCount", "Installations réussies"),
)


def _load(path: Path) -> dict[str, Any] | None:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return None


def _sum_field(
    payload: dict[str, Any], field: str
) -> tuple[int, str | None, str | None]:
    """Somme un champ sur tous les enregistrements, et rend la plage de dates.

    ⚠️ Les enregistrements ne sont pas toujours triés par date : `installs_total`
    arrive à l'envers (2026-08-31 en tête, 2026-03-23 en queue). On prend donc
    le min et le max, jamais le premier et le dernier.
    """
    total = 0
    dates: list[str] = []
    for record in payload.get("records") or ():
        value = record.get(field)
        if isinstance(value, bool):
            continue
        if isinstance(value, (int, float)):
            total += int(value)
        day = record.get("date")
        if isinstance(day, str) and day:
            dates.append(day)
    if not dates:
        return total, None, None
    return total, min(dates), max(dates)


def build_totals(output_root: Path, manifest: dict[str, Any]) -> dict[str, Any]:
    """Construit les cumuls depuis les jeux `*_total` déjà écrits par le run."""
    latest = output_root / "latest"
    metrics: dict[str, Any] = {}
    for key, dataset, field, _label in CUMULATIVE:
        payload = _load(latest / f"{dataset}.json")
        if payload is None:
            metrics[key] = None
            continue
        total, first_day, last_day = _sum_field(payload, field)
        metrics[key] = {
            "total": total,
            "firstDate": first_day,
            "lastDate": last_day,
        }
    return {
        "schemaVersion": 1,
        "storeId": manifest.get("storeId"),
        "runId": manifest.get("runId"),
        "collectedAt": manifest.get("collectedAt"),
        "complete": manifest.get("complete"),
        "metrics": metrics,
    }


def emit(totals: dict[str, Any]) -> None:
    """Publie les totaux sur stdout et, sous Actions, dans le résumé du job."""
    encoded = json.dumps(totals, ensure_ascii=False, sort_keys=True)
    print(BEGIN_MARKER)
    print(encoded)
    print(END_MARKER)
    sys.stdout.flush()

    summary_path = os.environ.get("GITHUB_STEP_SUMMARY", "").strip()
    if not summary_path:
        return
    lines = [
        "## Totaux Microsoft Store",
        "",
        "| Mesure | Cumul | Période couverte |",
        "|---|---:|---|",
    ]
    for key, _dataset, _field, label in CUMULATIVE:
        entry = totals["metrics"].get(key)
        if not entry:
            lines.append(f"| {label} | indisponible | — |")
            continue
        if entry["lastDate"]:
            period = f"{entry['firstDate']} → {entry['lastDate']}"
        else:
            period = "—"
        lines.append(f"| {label} | {entry['total']} | {period} |")
    lines.extend(
        ["", f"Run `{totals.get('runId')}` — collecte complète : {totals.get('complete')}."]
    )
    try:
        with open(summary_path, "a", encoding="utf-8") as handle:
            handle.write("\n".join(lines) + "\n")
    except OSError as error:  # un résumé manquant ne doit pas faire échouer la collecte
        print(f"Résumé du job non écrit: {error}", file=sys.stderr)


def extract(log_text: str) -> dict[str, Any]:
    """Relit le bloc délimité dans un journal de run Actions.

    Chaque ligne du journal est préfixée par l'horodatage du runner
    (`2026-09-20T09:45:43.8054440Z `), et `gh run view --log` ajoute encore
    `<job>\\t<step>\\t` devant. On repère les marqueurs, puis on retient ce qui
    suit le dernier préfixe d'horodatage de chaque ligne intermédiaire.
    """
    lines = log_text.splitlines()
    begin = end = None
    for index, line in enumerate(lines):
        if BEGIN_MARKER in line:
            begin = index
            end = None
        elif END_MARKER in line and begin is not None:
            end = index
    if begin is None or end is None:
        raise ValueError("Bloc de totaux absent du journal")

    payload_lines = []
    for line in lines[begin + 1 : end]:
        _head, separator, tail = line.rpartition("Z ")
        payload_lines.append(tail if separator else line)
    return json.loads("".join(payload_lines).strip())
