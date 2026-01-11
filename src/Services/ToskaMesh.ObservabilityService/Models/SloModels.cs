namespace ToskaMesh.ObservabilityService.Models;

public sealed record SloDefinition(
    string Id,
    string Service,
    string Name,
    string Description,
    double Objective,
    TimeSpan ErrorBudgetWindow,
    double BurnRateThreshold);

public sealed record SloWindowData(
    string SloId,
    string WindowLabel,
    TimeSpan Window,
    long TotalRequests,
    long ErrorRequests,
    DateTimeOffset WindowEnd);

public sealed record SloWindowStatus(
    string WindowLabel,
    double Sli,
    double BurnRate,
    double ErrorBudgetRemaining,
    double ErrorBudgetRemainingMinutes,
    long TotalRequests,
    long ErrorRequests);

public sealed record SloStatus(
    SloDefinition Definition,
    IReadOnlyList<SloWindowStatus> Windows,
    double MaxBurnRate,
    bool IsBurnRateAlert,
    string Severity);

public sealed record BurnRateAlert(
    string SloId,
    string Service,
    double BurnRate,
    string Severity,
    IReadOnlyList<SloWindowStatus> Windows);
