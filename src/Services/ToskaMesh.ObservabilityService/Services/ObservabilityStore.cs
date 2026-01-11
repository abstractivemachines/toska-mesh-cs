using ToskaMesh.ObservabilityService.Models;

namespace ToskaMesh.ObservabilityService.Services;

public sealed class ObservabilityStore
{
    private readonly List<MetricSummary> metricSummaries;
    private readonly List<SpanRecord> spanRecords;
    private readonly List<SloDefinition> sloDefinitions;
    private readonly List<SloWindowData> sloWindows;
    private readonly List<ReleaseRecord> releases;
    private readonly List<PlaybookDefinition> playbooks;
    private readonly List<ObservabilityPortalEntry> portalEntries;

    public ObservabilityStore()
    {
        var now = DateTimeOffset.UtcNow;
        metricSummaries = ObservabilitySeedData.MetricSummaries(now).ToList();
        spanRecords = ObservabilitySeedData.SpanRecords().ToList();
        sloDefinitions = ObservabilitySeedData.SloDefinitions().ToList();
        sloWindows = ObservabilitySeedData.SloWindows(now).ToList();
        releases = ObservabilitySeedData.Releases(now).ToList();
        playbooks = ObservabilitySeedData.Playbooks().ToList();
        portalEntries = ObservabilitySeedData.PortalEntries().ToList();
    }

    public IReadOnlyList<MetricSummary> GetMetricSummaries()
    {
        return metricSummaries.OrderByDescending(summary => summary.CapturedAt).ToList();
    }

    public IReadOnlyList<SpanRecord> GetSpanRecords() => spanRecords;

    public IReadOnlyList<SloStatus> GetSloStatuses(SloCalculator calculator)
    {
        return sloDefinitions
            .Select(definition => calculator.Evaluate(definition, GetWindows(definition.Id)))
            .ToList();
    }

    public IReadOnlyList<BurnRateAlert> GetBurnRateAlerts(SloCalculator calculator)
    {
        return GetSloStatuses(calculator)
            .Where(status => status.IsBurnRateAlert)
            .Select(status => new BurnRateAlert(
                status.Definition.Id,
                status.Definition.Service,
                status.MaxBurnRate,
                status.Severity,
                status.Windows))
            .OrderByDescending(alert => alert.BurnRate)
            .ToList();
    }

    public IReadOnlyList<ReleaseRecord> GetReleases()
    {
        return releases
            .OrderByDescending(release => release.DeployedAt)
            .ToList();
    }

    public IReadOnlyList<ReleaseRecord> GetRecentReleases()
    {
        return releases
            .OrderByDescending(release => release.DeployedAt)
            .Take(5)
            .ToList();
    }

    public ReleaseRecord? GetRelease(string id)
    {
        return releases.FirstOrDefault(release => string.Equals(release.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public ReleaseRecord AddRelease(ReleaseIngestRequest request)
    {
        var release = new ReleaseRecord(
            $"rel-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{request.Service.ToLowerInvariant()}",
            request.Service,
            request.Version,
            request.Environment,
            DateTimeOffset.UtcNow,
            request.DiffUrl,
            $"https://deployments.toskamesh.local/rollback/{request.Service}/{request.Version}",
            request.TriggeredBy,
            request.BlastRadiusServices);

        releases.Insert(0, release);
        return release;
    }

    public RollbackResult? RequestRollback(string id)
    {
        var release = GetRelease(id);
        if (release is null)
        {
            return null;
        }

        return new RollbackResult(
            release.Id,
            release.Service,
            release.RollbackUrl,
            "queued",
            DateTimeOffset.UtcNow);
    }

    public IReadOnlyList<PlaybookDefinition> GetPlaybooks()
    {
        return playbooks.ToList();
    }

    public PlaybookDefinition? GetPlaybook(string id)
    {
        return playbooks.FirstOrDefault(playbook => string.Equals(playbook.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public ServiceDashboard? GetServiceDashboard(string service, SloCalculator calculator, TopologyGraphBuilder graphBuilder)
    {
        var definitionMatch = sloDefinitions.Any(definition => string.Equals(definition.Service, service, StringComparison.OrdinalIgnoreCase));
        if (!definitionMatch)
        {
            return null;
        }

        var metrics = metricSummaries.FirstOrDefault(summary => string.Equals(summary.Service, service, StringComparison.OrdinalIgnoreCase));
        var sloStatuses = GetSloStatuses(calculator)
            .Where(status => string.Equals(status.Definition.Service, service, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var recentReleases = releases
            .Where(release => string.Equals(release.Service, service, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(release => release.DeployedAt)
            .Take(5)
            .ToList();
        var playbookItems = playbooks
            .Where(playbook => string.Equals(playbook.Service, service, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var graph = graphBuilder.Build(spanRecords);
        var filteredGraph = FilterGraph(graph, service);

        return new ServiceDashboard(service, metrics, sloStatuses, recentReleases, playbookItems, filteredGraph);
    }

    public IReadOnlyList<ObservabilityPortalEntry> GetPortal() => portalEntries;

    private IReadOnlyList<SloWindowData> GetWindows(string sloId)
    {
        return sloWindows.Where(window => string.Equals(window.SloId, sloId, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private static TopologyGraph FilterGraph(TopologyGraph graph, string service)
    {
        var relevantEdges = graph.Edges
            .Where(edge => string.Equals(edge.From, service, StringComparison.OrdinalIgnoreCase)
                || string.Equals(edge.To, service, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var nodeIds = relevantEdges
            .SelectMany(edge => new[] { edge.From, edge.To })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var nodes = graph.Nodes
            .Where(node => nodeIds.Contains(node.Id))
            .ToList();

        return new TopologyGraph(graph.GeneratedAt, nodes, relevantEdges);
    }
}
