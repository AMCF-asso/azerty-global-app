"""Rapatrie localement le dernier snapshot Microsoft Store collecté par GitHub Actions.

Source : l'artefact du dernier run **réussi** du workflow, qui contient
exactement l'arborescence archivée dans Azure Blob. Aucun secret n'est stocké :
le jeton GitHub est lu à la volée dans le gestionnaire d'identifiants Windows,
celui-là même qui sert à `git push`.

    python tools/refresh_snapshot.py

Le serveur MCP lit ensuite `out/latest` via `STORE_ANALYTICS_SOURCE`.

Portage de `search-analytics/tools/refresh_snapshot.py` (2026-08-16). Son absence
ici est ce qui rendait ce pipeline invisible : le serveur MCP existait mais
n'avait aucune source locale à lire, donc n'était câblé nulle part — et un agent
à qui on demandait les chiffres du Store finissait par les reconstruire depuis
les pixels du dashboard Partner Center.
"""

from __future__ import annotations

import io
import json
import os
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path
from urllib.error import HTTPError
from urllib.request import HTTPRedirectHandler, Request, build_opener, urlopen

REPO = os.environ.get("STORE_GITHUB_REPO", "AZERTYGlobal/app")
WORKFLOW = os.environ.get("STORE_WORKFLOW_FILE", "store-analytics.yml")
API = "https://api.github.com"
DESTINATION = Path(__file__).resolve().parents[1] / "out"
USER_AGENT = "StoreAnalytics-refresh/0.1"


def use_utf8_console() -> None:
    for stream in (sys.stdout, sys.stderr):
        reconfigure = getattr(stream, "reconfigure", None)
        if reconfigure is not None:
            try:
                reconfigure(encoding="utf-8", errors="backslashreplace")
            except (OSError, ValueError):
                pass


def _credential_password(query: str) -> str:
    for _ in range(3):
        try:
            result = subprocess.run(
                ["git", "credential", "fill"],
                input=query,
                capture_output=True,
                text=True,
                timeout=30,
            )
        except (OSError, subprocess.TimeoutExpired):
            continue
        for line in result.stdout.splitlines():
            if line.startswith("password="):
                return line[len("password=") :].strip()
    return ""


def github_token() -> str:
    """Jeton du gestionnaire d'identifiants, jamais écrit sur le disque.

    Le compte est **épinglé** sur le propriétaire de REPO. Le gestionnaire
    Windows garde plusieurs identités github.com et, interrogé sans
    `username=`, il rend la dernière utilisée — d'où un 404 muet sur un dépôt
    privé dès qu'un autre compte a touché GitHub entre-temps. Ne jamais retirer
    le `username=` de la requête.
    """

    token = os.environ.get("GITHUB_TOKEN", "").strip()
    if token:
        return token
    owner = REPO.split("/")[0]
    token = _credential_password(
        f"protocol=https\nhost=github.com\nusername={owner}\n\n"
    )
    if token:
        return token
    token = _credential_password("protocol=https\nhost=github.com\n\n")
    if token:
        return token
    raise SystemExit(
        f"Aucun jeton GitHub disponible pour le compte « {owner} ». Lancer une "
        "fois `git fetch` dans le dépôt pour réamorcer le gestionnaire "
        "d'identifiants, ou définir GITHUB_TOKEN."
    )


def api_get(path: str, token: str) -> bytes:
    request = Request(
        f"{API}{path}",
        headers={
            "Authorization": f"Bearer {token}",
            "Accept": "application/vnd.github+json",
            "User-Agent": USER_AGENT,
        },
    )
    try:
        with urlopen(request, timeout=120) as response:
            return response.read()
    except HTTPError as error:
        detail = error.read().decode("utf-8", errors="replace")[:300]
        hint = ""
        if error.code == 404:
            owner = REPO.split("/")[0]
            hint = (
                f"\nSur un dépôt privé, 404 veut dire « jeton du mauvais compte », "
                f"pas « chemin inexistant ». Vérifier lequel répond :\n"
                f'  printf "protocol=https\\nhost=github.com\\nusername={owner}\\n\\n" '
                f"| git credential fill"
            )
        raise SystemExit(
            f"GitHub a répondu {error.code} sur {path}: {detail}{hint}"
        ) from error


class _DropAuthorizationOnRedirect(HTTPRedirectHandler):
    """Le téléchargement d'artefact redirige vers Azure, qui refuse l'en-tête
    d'autorisation GitHub — et le jeton n'a rien à faire chez un tiers."""

    def redirect_request(self, req, fp, code, msg, headers, newurl):
        new = super().redirect_request(req, fp, code, msg, headers, newurl)
        if new is not None:
            new.headers.pop("Authorization", None)
            new.unredirected_hdrs.pop("Authorization", None)
        return new


def download_artifact(artifact_id: int, token: str) -> bytes:
    request = Request(
        f"{API}/repos/{REPO}/actions/artifacts/{artifact_id}/zip",
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
        raise SystemExit(
            f"Téléchargement de l'artefact refusé ({error.code}): {detail}"
        ) from error


def latest_successful_run(token: str) -> dict:
    payload = json.loads(
        api_get(
            f"/repos/{REPO}/actions/workflows/{WORKFLOW}/runs"
            "?status=success&per_page=1",
            token,
        )
    )
    runs = payload.get("workflow_runs") or []
    if not runs:
        raise SystemExit("Aucun run réussi : lancer le workflow avant de rafraîchir.")
    return runs[0]


def main() -> None:
    use_utf8_console()
    token = github_token()
    run = latest_successful_run(token)
    print(
        f"Run {run['run_number']} ({run['event']}, {run['created_at']}) — "
        f"{run['html_url']}"
    )

    artifacts = json.loads(
        api_get(f"/repos/{REPO}/actions/runs/{run['id']}/artifacts", token)
    ).get("artifacts") or []
    live = [a for a in artifacts if not a.get("expired")]
    if not live:
        raise SystemExit(
            "L'artefact de ce run a expiré (rétention 14 jours). "
            "Relancer le workflow pour en produire un nouveau."
        )
    artifact = live[0]

    archive = download_artifact(artifact["id"], token)
    with zipfile.ZipFile(io.BytesIO(archive)) as bundle:
        names = bundle.namelist()
        if "manifest.json" not in names:
            raise SystemExit("Artefact inattendu : pas de manifest.json à la racine.")
        if DESTINATION.exists():
            shutil.rmtree(DESTINATION)
        DESTINATION.mkdir(parents=True)
        for name in names:
            # Une entrée d'archive ne doit jamais sortir du dossier de destination.
            target = (DESTINATION / name).resolve()
            if not str(target).startswith(str(DESTINATION.resolve())):
                raise SystemExit(f"Chemin d'archive suspect: {name}")
            if name.endswith("/"):
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(bundle.read(name))

    manifest = json.loads((DESTINATION / "manifest.json").read_text(encoding="utf-8"))
    state = "complet" if manifest["complete"] else "INCOMPLET"
    print(
        f"Snapshot {manifest['runId']} écrit dans {DESTINATION}\n"
        f"  store {manifest['storeId']}: {state}, "
        f"{len(manifest['datasets'])} jeux de données, "
        f"collecté le {manifest['collectedAt']}"
    )
    for entry in manifest["datasets"]:
        print(
            f"    {entry['name']:<34} {entry['records']:>7} lignes  "
            f"{entry['startDate']} → {entry['endDate']}"
        )
    for failure in manifest["failures"]:
        print(f"    ÉCHEC {failure['dataset']}: {failure['error']}")

    print(f"\n  $env:STORE_ANALYTICS_SOURCE = \"{DESTINATION / 'latest'}\"")


if __name__ == "__main__":
    main()
