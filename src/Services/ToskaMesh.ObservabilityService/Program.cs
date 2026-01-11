using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using ToskaMesh.ObservabilityService.Models;
using ToskaMesh.ObservabilityService.Services;
using ToskaMesh.Telemetry;

var builder = WebApplication.CreateBuilder(args);

const string ServiceName = "ObservabilityService";

builder.Services.AddMeshTelemetry(builder.Configuration, ServiceName);

builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeFormattedMessage = true;
    options.ParseStateValues = true;
    options.IncludeScopes = true;
    options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(ServiceName));
    options.AddConsoleExporter();
});

builder.Services.AddSingleton(new ObservabilityMetrics(ServiceName));
builder.Services.AddSingleton<ObservabilityStore>();
builder.Services.AddSingleton<TopologyGraphBuilder>();
builder.Services.AddSingleton<SloCalculator>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    var metrics = context.RequestServices.GetRequiredService<ObservabilityMetrics>();
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    try
    {
        await next();
    }
    catch
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        throw;
    }
    finally
    {
        stopwatch.Stop();
        metrics.RecordRequest(context, stopwatch.Elapsed);
    }
});

app.MapGet("/", () => Results.Ok(new { service = ServiceName, status = "ready" }));
app.MapGet("/observability/portal", (ObservabilityStore store) => Results.Ok(store.GetPortal()));

app.MapGet("/observability/metrics/summary", (ObservabilityStore store) =>
    Results.Ok(store.GetMetricSummaries()));

app.MapGet("/observability/topology", (ObservabilityStore store, TopologyGraphBuilder builder) =>
    Results.Ok(builder.Build(store.GetSpanRecords())));

app.MapGet("/observability/slo", (ObservabilityStore store, SloCalculator calculator) =>
    Results.Ok(store.GetSloStatuses(calculator)));

app.MapGet("/observability/slo/{service}", (string service, ObservabilityStore store, SloCalculator calculator) =>
{
    var statuses = store.GetSloStatuses(calculator)
        .Where(status => status.Definition.Service.Equals(service, StringComparison.OrdinalIgnoreCase))
        .ToList();
    return Results.Ok(statuses);
});

app.MapGet("/observability/alerts/burn-rate", (ObservabilityStore store, SloCalculator calculator) =>
    Results.Ok(store.GetBurnRateAlerts(calculator)));

app.MapGet("/observability/releases", (ObservabilityStore store) => Results.Ok(store.GetReleases()));

app.MapGet("/observability/releases/recent", (ObservabilityStore store) => Results.Ok(store.GetRecentReleases()));

app.MapGet("/observability/releases/{id}", (string id, ObservabilityStore store) =>
{
    var release = store.GetRelease(id);
    return release is null ? Results.NotFound() : Results.Ok(release);
});

app.MapPost("/observability/releases", (ReleaseIngestRequest request, ObservabilityStore store) =>
{
    var release = store.AddRelease(request);
    return Results.Created($"/observability/releases/{release.Id}", release);
});

app.MapPost("/observability/releases/{id}/rollback", (string id, ObservabilityStore store) =>
{
    var rollback = store.RequestRollback(id);
    return rollback is null ? Results.NotFound() : Results.Ok(rollback);
});

app.MapGet("/observability/playbooks", (ObservabilityStore store) => Results.Ok(store.GetPlaybooks()));

app.MapGet("/observability/playbooks/{id}", (string id, ObservabilityStore store) =>
{
    var playbook = store.GetPlaybook(id);
    return playbook is null ? Results.NotFound() : Results.Ok(playbook);
});

app.MapGet("/observability/dashboards/service/{service}",
    (string service, ObservabilityStore store, SloCalculator calculator, TopologyGraphBuilder builder) =>
    {
        var dashboard = store.GetServiceDashboard(service, calculator, builder);
        return dashboard is null ? Results.NotFound() : Results.Ok(dashboard);
    });

app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.Run();
