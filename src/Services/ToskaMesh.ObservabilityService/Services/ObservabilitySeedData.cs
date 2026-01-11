using ToskaMesh.ObservabilityService.Models;

namespace ToskaMesh.ObservabilityService.Services;

public static class ObservabilitySeedData
{
    public static IReadOnlyList<MetricSummary> MetricSummaries(DateTimeOffset now)
    {
        return new List<MetricSummary>
        {
            new("Gateway", 850.4, 0.012, 180, 0.62, now),
            new("AuthService", 410.8, 0.018, 220, 0.55, now),
            new("ConfigService", 190.3, 0.006, 140, 0.42, now),
            new("MetricsService", 75.9, 0.003, 120, 0.38, now),
            new("TracingService", 68.2, 0.009, 260, 0.47, now)
        };
    }

    public static IReadOnlyList<SpanRecord> SpanRecords()
    {
        return new List<SpanRecord>
        {
            new("Gateway", "AuthService", 45, false),
            new("Gateway", "AuthService", 72, false),
            new("Gateway", "AuthService", 110, true),
            new("Gateway", "ConfigService", 38, false),
            new("Gateway", "ConfigService", 52, false),
            new("Gateway", "MetricsService", 120, false),
            new("Gateway", "TracingService", 210, false),
            new("AuthService", "Postgres", 85, false),
            new("AuthService", "Postgres", 140, true),
            new("AuthService", "Redis", 28, false),
            new("ConfigService", "Postgres", 65, false),
            new("ConfigService", "Consul", 90, false),
            new("MetricsService", "Postgres", 72, false),
            new("MetricsService", "Grafana", 130, false),
            new("TracingService", "Postgres", 95, false),
            new("TracingService", "ObjectStorage", 180, true),
            new("Gateway", "Router", 60, false),
            new("Router", "ServiceA", 45, false),
            new("Router", "ServiceB", 55, true),
            new("ServiceA", "Payments", 80, false),
            new("ServiceB", "Payments", 140, true)
        };
    }

    public static IReadOnlyList<SloDefinition> SloDefinitions()
    {
        return new List<SloDefinition>
        {
            new(
                "gateway-availability",
                "Gateway",
                "Gateway availability",
                "Maintain gateway request success rate above 99.5%.",
                0.995,
                TimeSpan.FromDays(30),
                2.0),
            new(
                "auth-availability",
                "AuthService",
                "Auth service availability",
                "Maintain auth service success rate above 99%.",
                0.99,
                TimeSpan.FromDays(30),
                2.0),
            new(
                "config-availability",
                "ConfigService",
                "Config service availability",
                "Keep configuration reads above 99.9% success.",
                0.999,
                TimeSpan.FromDays(30),
                1.5)
        };
    }

    public static IReadOnlyList<SloWindowData> SloWindows(DateTimeOffset now)
    {
        return new List<SloWindowData>
        {
            new("gateway-availability", "1h", TimeSpan.FromHours(1), 120_000, 280, now),
            new("gateway-availability", "6h", TimeSpan.FromHours(6), 660_000, 1_920, now),
            new("gateway-availability", "28d", TimeSpan.FromDays(28), 18_400_000, 32_400, now),
            new("auth-availability", "1h", TimeSpan.FromHours(1), 62_000, 1_020, now),
            new("auth-availability", "6h", TimeSpan.FromHours(6), 320_000, 5_400, now),
            new("auth-availability", "28d", TimeSpan.FromDays(28), 8_200_000, 78_000, now),
            new("config-availability", "1h", TimeSpan.FromHours(1), 22_000, 40, now),
            new("config-availability", "6h", TimeSpan.FromHours(6), 140_000, 300, now),
            new("config-availability", "28d", TimeSpan.FromDays(28), 3_400_000, 4_100, now)
        };
    }

    public static IReadOnlyList<ReleaseRecord> Releases(DateTimeOffset now)
    {
        return new List<ReleaseRecord>
        {
            new(
                "rel-2026-01-08-gateway",
                "Gateway",
                "2.4.1",
                "prod",
                now.AddDays(-2),
                "https://github.com/abstractivemachines/toskamesh/compare/gateway-2.4.0...gateway-2.4.1",
                "https://deployments.toskamesh.local/rollback/gateway/2.4.1",
                "release-bot",
                new List<string> { "AuthService", "ConfigService", "Router" }),
            new(
                "rel-2026-01-09-auth",
                "AuthService",
                "1.9.0",
                "prod",
                now.AddDays(-1),
                "https://github.com/abstractivemachines/toskamesh/compare/auth-1.8.4...auth-1.9.0",
                "https://deployments.toskamesh.local/rollback/auth/1.9.0",
                "oncall@toskamesh",
                new List<string> { "Gateway", "Router" }),
            new(
                "rel-2026-01-10-config",
                "ConfigService",
                "3.2.5",
                "prod",
                now.AddHours(-6),
                "https://github.com/abstractivemachines/toskamesh/compare/config-3.2.4...config-3.2.5",
                "https://deployments.toskamesh.local/rollback/config/3.2.5",
                "release-bot",
                new List<string> { "Gateway", "AuthService" })
        };
    }

    public static IReadOnlyList<PlaybookDefinition> Playbooks()
    {
        return new List<PlaybookDefinition>
        {
            new(
                "pb-gateway-errors",
                "Gateway",
                "Gateway 5xx surge",
                "Investigate elevated 5xx responses impacting ingress traffic.",
                "high",
                new List<string> { "rel-2026-01-08-gateway" },
                new List<PlaybookStep>
                {
                    new(
                        "Confirm error pattern",
                        new List<string>
                        {
                            "level:error service=gateway status>=500 | summarize count by route",
                            "traceId=* | filter span.kind=server"
                        },
                        new List<string>
                        {
                            "http_request_error_rate",
                            "http_request_latency_p95"
                        },
                        new List<string>
                        {
                            "Check if spike aligns with deploy or config change",
                            "Inspect top routes for regressions"
                        }),
                    new(
                        "Check downstream dependencies",
                        new List<string>
                        {
                            "service=authservice level:warn OR level:error",
                            "dependency=redis latency>200"
                        },
                        new List<string>
                        {
                            "dependency_error_rate",
                            "dependency_latency"
                        },
                        new List<string>
                        {
                            "Validate auth service health",
                            "Consider routing traffic to healthy zones"
                        })
                }),
            new(
                "pb-auth-latency",
                "AuthService",
                "Auth latency regression",
                "Investigate latency spikes on auth requests.",
                "medium",
                new List<string> { "rel-2026-01-09-auth" },
                new List<PlaybookStep>
                {
                    new(
                        "Inspect DB saturation",
                        new List<string>
                        {
                            "service=authservice event=db duration>100",
                            "db.instance=auth-primary error=*"
                        },
                        new List<string>
                        {
                            "db_latency_p95",
                            "db_connections_in_use"
                        },
                        new List<string>
                        {
                            "Check Postgres CPU and connection pool",
                            "Review recent index migrations"
                        })
                })
        };
    }

    public static IReadOnlyList<ObservabilityPortalEntry> PortalEntries()
    {
        return new List<ObservabilityPortalEntry>
        {
            new("Metrics summary", "/observability/metrics/summary", "Latency, error rate, saturation snapshots"),
            new("Topology graph", "/observability/topology", "Live dependency map with traffic and alerts"),
            new("SLO statuses", "/observability/slo", "Error budgets and burn-rate windows"),
            new("Burn-rate alerts", "/observability/alerts/burn-rate", "Active SLO burn alerts"),
            new("Release history", "/observability/releases", "Deploy timeline with diff links"),
            new("Playbooks", "/observability/playbooks", "Guided troubleshooting playbooks"),
            new("Service dashboard", "/observability/dashboards/service/{service}", "Service-level lens"),
            new("Prometheus scrape", "/metrics", "OpenTelemetry Prometheus exporter")
        };
    }
}
