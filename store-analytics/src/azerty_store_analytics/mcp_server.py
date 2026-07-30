from __future__ import annotations

from typing import Any

from .query import AnalyticsQueries


def create_server() -> Any:
    try:
        from mcp.server.fastmcp import FastMCP
    except ImportError as error:
        raise RuntimeError(
            "Installez l'extra MCP avec: pip install -e 'store-analytics[mcp]'"
        ) from error

    server = FastMCP(
        "AZERTY Global — Microsoft Store",
        instructions=(
            "Serveur en lecture seule pour les statistiques Microsoft Store "
            "d'AZERTY Global. Les avis et données de santé sont privés."
        ),
    )
    queries = AnalyticsQueries()

    @server.tool()
    def get_store_summary(
        start_date: str | None = None,
        end_date: str | None = None,
    ) -> dict[str, Any]:
        """Résume les principaux indicateurs pour une période AAAA-MM-JJ."""
        return queries.summary(start_date, end_date)

    @server.tool()
    def get_store_timeseries(
        metric: str,
        start_date: str | None = None,
        end_date: str | None = None,
    ) -> list[dict[str, Any]]:
        """Renvoie une série quotidienne pour une métrique disponible."""
        return queries.timeseries(metric, start_date, end_date)

    @server.tool()
    def get_store_breakdown(
        metric: str,
        dimension: str,
        start_date: str | None = None,
        end_date: str | None = None,
        limit: int = 20,
    ) -> list[dict[str, Any]]:
        """Ventile acquisitions, installations, conversions ou erreurs."""
        return queries.breakdown(
            metric,
            dimension,
            start_date,
            end_date,
            limit,
        )

    @server.tool()
    def get_store_reviews(
        limit: int = 20,
        market: str | None = None,
        minimum_rating: int | None = None,
    ) -> list[dict[str, Any]]:
        """Renvoie les avis récents. Données privées, ne pas republier en bloc."""
        return queries.reviews(limit, market, minimum_rating)

    @server.tool()
    def get_store_health(
        start_date: str | None = None,
        end_date: str | None = None,
        limit: int = 20,
    ) -> list[dict[str, Any]]:
        """Renvoie les principaux crashs, blocages et erreurs des 30 derniers jours."""
        return queries.health(start_date, end_date, limit)

    @server.tool()
    def get_store_insights() -> list[dict[str, Any]]:
        """Renvoie les insights détectés par Microsoft sur 30 jours."""
        return queries.insights()

    @server.tool()
    def get_store_datasets() -> dict[str, Any]:
        """Décrit la fraîcheur, la couverture et les éventuels échecs de collecte."""
        return queries.available_datasets()

    return server


def main() -> None:
    create_server().run(transport="stdio")


if __name__ == "__main__":
    main()
