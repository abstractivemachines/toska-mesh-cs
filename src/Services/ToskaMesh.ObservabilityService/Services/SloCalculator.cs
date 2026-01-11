using ToskaMesh.ObservabilityService.Models;

namespace ToskaMesh.ObservabilityService.Services;

public sealed class SloCalculator
{
    public SloStatus Evaluate(SloDefinition definition, IReadOnlyList<SloWindowData> windows)
    {
        var windowStatuses = windows
            .Select(window => EvaluateWindow(definition, window))
            .OrderBy(status => status.WindowLabel)
            .ToList();

        var maxBurnRate = windowStatuses.Count == 0 ? 0 : windowStatuses.Max(status => status.BurnRate);
        var isAlert = maxBurnRate >= definition.BurnRateThreshold;
        var severity = maxBurnRate >= definition.BurnRateThreshold * 2
            ? "critical"
            : isAlert
                ? "warning"
                : "ok";

        return new SloStatus(definition, windowStatuses, Math.Round(maxBurnRate, 2), isAlert, severity);
    }

    private static SloWindowStatus EvaluateWindow(SloDefinition definition, SloWindowData window)
    {
        if (window.TotalRequests <= 0)
        {
            return new SloWindowStatus(
                window.WindowLabel,
                1,
                0,
                1,
                definition.ErrorBudgetWindow.TotalMinutes,
                0,
                0);
        }

        var errorRate = window.ErrorRequests / (double)window.TotalRequests;
        var sli = Math.Clamp(1 - errorRate, 0, 1);
        var errorBudget = Math.Max(0, 1 - definition.Objective);
        var burnRate = errorBudget <= 0 ? 0 : errorRate / errorBudget;
        var budgetRemaining = errorBudget <= 0 ? 1 : Math.Clamp(1 - (errorRate / errorBudget), 0, 1);
        var budgetMinutes = definition.ErrorBudgetWindow.TotalMinutes * budgetRemaining;

        return new SloWindowStatus(
            window.WindowLabel,
            Math.Round(sli, 4),
            Math.Round(burnRate, 2),
            Math.Round(budgetRemaining, 4),
            Math.Round(budgetMinutes, 2),
            window.TotalRequests,
            window.ErrorRequests);
    }
}
