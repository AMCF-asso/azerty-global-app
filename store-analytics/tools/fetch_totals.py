"""Relève les totaux agrégés du Store dans le journal du dernier run réussi.

    python tools/fetch_totals.py

Pourquoi passer par le journal plutôt que par l'artefact : le commit `1013d2b`
du 2026-09-05 (« Keep Store analytics archives in private storage only ») a
supprimé l'étape `upload-artifact`, qui était la seule source de
`refresh_snapshot.py`. La décision est bonne — l'archive détaillée n'a rien à
faire dans un artefact GitHub — mais elle a coupé le seul consommateur local
sans le remplacer, et la copie locale est restée figée au 2026-09-02 pendant
deux semaines de runs verts.

Ce script rétablit la lecture **sans rouvrir l'archive** : le collecteur émet un
bloc `STORE_TOTALS_BEGIN` / `STORE_TOTALS_END` sur stdout, qui ne contient que
des cumuls. Les journaux Actions sont conservés 90 jours, contre 14 pour
l'ancien artefact.

Écrit `out/totals.json` et affiche les cumuls. ⛔ Refuse un run incomplet : un
jeu de données manquant donne un cumul sous-évalué, et un chiffre sous-évalué
publié sur le site est exactement le problème qu'on est en train de réparer.
"""

from __future__ import annotations

import io
import json
import subprocess
import sys
import zipfile
from pathlib import Path
from urllib.error import HTTPError
from urllib.request import Request, build_opener

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "src"))

from azerty_store_analytics import summary  # noqa: E402

from refresh_snapshot import (  # noqa: E402
    API,
    REPO,
    USER_AGENT,
    _DropAuthorizationOnRedirect,
    github_token,
    latest_successful_run,
    use_utf8_console,
)

DESTINATION = Path(__file__).resolve().parents[1] / "out" / "totals.json"


def logs_via_gh(run_id: int) -> str:
    """Journal par la CLI `gh`, essayée en premier.

    ⚠️ Mesuré le 2026-09-20 : l'API REST `/actions/runs/{id}/logs` répond
    « 403 Must have admin rights to Repository » avec le jeton du gestionnaire
    d'identifiants, celui-là même qui sert à `git push` et qui suffit pour tout
    le reste. `gh` s'authentifie avec son propre jeton, qui porte les droits.
    Donc `gh` d'abord, API en repli — et pas l'inverse.
    """
    try:
        result = subprocess.run(
            ["gh", "run", "view", str(run_id), "--repo", REPO, "--log"],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=300,
        )
    except (OSError, subprocess.TimeoutExpired) as error:
        raise RuntimeError(f"gh indisponible: {error}") from error
    if result.returncode != 0:
        raise RuntimeError(
            f"gh a échoué ({result.returncode}): {result.stderr.strip()[:300]}"
        )
    return result.stdout


def download_logs(run_id: int, token: str) -> bytes:
    request = Request(
        f"{API}/repos/{REPO}/actions/runs/{run_id}/logs",
        headers={
            "Authorization": f"Bearer {token}",
            "Accept": "application/vnd.github+json",
            "User-Agent": USER_AGENT,
        },
    )
    opener = build_opener(_DropAuthorizationOnRedirect())
    try:
        with opener.open(request, timeout=300) as response:
            return response.read()
    except HTTPError as error:
        detail = error.read().decode("utf-8", errors="replace")[:300]
        if error.code == 410:
            raise SystemExit(
                "Le journal de ce run a été purgé (rétention 90 jours). "
                "Relancer le workflow pour en produire un nouveau."
            ) from error
        raise SystemExit(
            f"Téléchargement du journal refusé ({error.code}): {detail}"
        ) from error


def log_text(archive: bytes) -> str:
    """Concatène les journaux de l'archive, en commençant par l'étape de collecte.

    ⚠️ L'archive contient un fichier par étape, plus une copie à plat. On lit
    tout : le bloc de totaux n'apparaît que dans l'étape de collecte, et les
    doublons sont sans effet puisque `summary.extract` retient le dernier bloc.
    """
    parts: list[str] = []
    with zipfile.ZipFile(io.BytesIO(archive)) as bundle:
        for name in sorted(bundle.namelist()):
            if not name.lower().endswith(".txt"):
                continue
            parts.append(bundle.read(name).decode("utf-8", errors="replace"))
    return "\n".join(parts)


def main() -> None:
    use_utf8_console()
    token = github_token()
    run = latest_successful_run(token)
    print(
        f"Run {run['run_number']} ({run['event']}, {run['created_at']}) — "
        f"{run['html_url']}"
    )

    try:
        raw = logs_via_gh(run["id"])
    except RuntimeError as error:
        print(f"⚠️ {error} — repli sur l'API REST.", file=sys.stderr)
        raw = log_text(download_logs(run["id"], token))

    try:
        totals = summary.extract(raw)
    except ValueError as error:
        raise SystemExit(
            f"{error}. Ce run est antérieur à l'ajout des totaux agrégés "
            "(2026-09-20) : relancer le workflow pour en produire un nouveau."
        ) from error

    if not totals.get("complete"):
        raise SystemExit(
            "⛔ Run incomplet : au moins un jeu de données n'a pas été collecté, "
            "donc les cumuls sont sous-évalués. Ne pas publier ce chiffre. "
            "Attendre le run du lendemain, qui repart de 2015-01-01."
        )

    DESTINATION.parent.mkdir(parents=True, exist_ok=True)
    DESTINATION.write_text(
        json.dumps(totals, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )

    print(f"Collecté le {totals.get('collectedAt')} (run {totals.get('runId')})")
    for key, _dataset, _field, label in summary.CUMULATIVE:
        entry = totals["metrics"].get(key)
        if not entry:
            print(f"  {label}: indisponible")
            continue
        print(
            f"  {label}: {entry['total']} "
            f"({entry['firstDate']} → {entry['lastDate']})"
        )
    print(f"Écrit dans {DESTINATION}")


if __name__ == "__main__":
    main()
