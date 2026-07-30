from __future__ import annotations

from dataclasses import dataclass, field


@dataclass(frozen=True)
class Dataset:
    name: str
    endpoint: str
    groupby: tuple[str, ...] = ()
    orderby: str | None = None
    filter_expression: str | None = None
    max_lookback_days: int | None = None
    paginated: bool = True
    extra_params: dict[str, str] = field(default_factory=dict)


DATASETS: tuple[Dataset, ...] = (
    Dataset(
        "acquisitions_total",
        "appacquisitions",
        ("date",),
        "date",
    ),
    Dataset(
        "acquisitions_detail",
        "appacquisitions",
        (
            "date",
            "applicationName",
            "acquisitionType",
            "storeClient",
            "market",
            "osVersion",
            "deviceType",
        ),
        "date",
    ),
    Dataset("installs_total", "installs", ("date",), "date"),
    Dataset(
        "installs_detail",
        "installs",
        (
            "applicationName",
            "date",
            "deviceType",
            "market",
            "osVersion",
            "packageVersion",
        ),
        "date",
    ),
    Dataset("channels_total", "appchannelconversions", ("date",), "date"),
    Dataset(
        "channels_detail",
        "appchannelconversions",
        (
            "date",
            "applicationName",
            "customCampaignId",
            "referrerUriDomain",
            "channelType",
            "storeClient",
            "deviceType",
            "market",
        ),
        "date",
    ),
    Dataset(
        "usage_daily_total",
        "usagedaily",
        ("date",),
        "date",
        max_lookback_days=90,
    ),
    Dataset(
        "usage_daily_detail",
        "usagedaily",
        (
            "applicationName",
            "subscriptionName",
            "deviceType",
            "packageVersion",
            "market",
            "date",
        ),
        "date",
        max_lookback_days=90,
    ),
    Dataset(
        "usage_monthly_total",
        "usagemonthly",
        ("date",),
        "date",
        max_lookback_days=90,
    ),
    Dataset(
        "usage_monthly_detail",
        "usagemonthly",
        (
            "applicationName",
            "subscriptionName",
            "deviceType",
            "packageVersion",
            "market",
            "date",
        ),
        "date",
        max_lookback_days=90,
    ),
    Dataset(
        "health_total",
        "failurehits",
        ("eventType",),
        "date",
        max_lookback_days=30,
    ),
    Dataset(
        "health_detail",
        "failurehits",
        (
            "failureName",
            "failureHash",
            "symbol",
            "osVersion",
            "eventType",
            "market",
            "deviceType",
            "packageName",
            "packageVersion",
        ),
        "date",
        max_lookback_days=30,
    ),
    Dataset(
        "ratings_total",
        "ratings",
        ("date",),
        "date",
    ),
    Dataset(
        "ratings_detail",
        "ratings",
        (
            "date",
            "applicationName",
            "market",
            "osVersion",
            "deviceType",
            "isRevised",
        ),
        "date",
    ),
    Dataset("reviews", "reviews", orderby="date desc"),
    Dataset(
        "insights",
        "insights",
        filter_expression=(
            "dataType eq 'acquisition' or dataType eq 'health' "
            "or dataType eq 'usage'"
        ),
        max_lookback_days=30,
        paginated=False,
    ),
)


METRICS: dict[str, tuple[str, str]] = {
    "acquisitions": ("acquisitions_total", "acquisitionQuantity"),
    "installs": ("installs_total", "successfulInstallCount"),
    "store_clicks": ("channels_total", "clickCount"),
    "store_conversions": ("channels_total", "conversionCount"),
    "daily_active_users": ("usage_daily_total", "dailyActiveUsers"),
    "daily_active_devices": ("usage_daily_total", "dailyActiveDevices"),
    "daily_sessions": ("usage_daily_total", "dailySessionCount"),
    "engagement_minutes": ("usage_daily_total", "engagementDurationMinutes"),
    "monthly_active_users": ("usage_monthly_total", "monthlyActiveUsers"),
    "monthly_active_devices": ("usage_monthly_total", "monthlyActiveDevices"),
    "health_events": ("health_total", "eventCount"),
}
