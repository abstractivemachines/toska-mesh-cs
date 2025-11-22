using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ToskaMesh.MetricsService.Data;
using ToskaMesh.MetricsService.Entities;
using ToskaMesh.MetricsService.Models;

namespace ToskaMesh.MetricsService.Services;

public interface IAlertRuleService
{
    Task<AlertRuleDto> CreateAsync(CreateAlertRuleRequest request, CancellationToken cancellationToken);
    Task<AlertRuleDto> UpdateAsync(Guid id, UpdateAlertRuleRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AlertRuleDto>> ListAsync(CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<AlertEvaluationResult> EvaluateAsync(Guid id, CancellationToken cancellationToken);
}

public class AlertRuleService : IAlertRuleService
{
    private readonly MetricsDbContext _dbContext;
    private readonly IMetricHistoryService _metricHistoryService;
    private readonly ILogger<AlertRuleService> _logger;

    public AlertRuleService(
        MetricsDbContext dbContext,
        IMetricHistoryService metricHistoryService,
        ILogger<AlertRuleService> logger)
    {
        _dbContext = dbContext;
        _metricHistoryService = metricHistoryService;
        _logger = logger;
    }

    public async Task<AlertRuleDto> CreateAsync(CreateAlertRuleRequest request, CancellationToken cancellationToken)
    {
        var entity = new AlertRule
        {
            Name = request.Name,
            Description = request.Description,
            MetricName = request.MetricName,
            Aggregation = request.Aggregation,
            Threshold = request.Threshold,
            Operator = request.Operator,
            Window = TimeSpan.FromMinutes(Math.Max(request.WindowMinutes, 1)),
            Enabled = request.Enabled,
            Severity = request.Severity,
            NotificationChannel = request.NotificationChannel,
            LabelFilters = request.LabelFilters,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.AlertRules.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<AlertRuleDto> UpdateAsync(Guid id, UpdateAlertRuleRequest request, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.AlertRules.SingleOrDefaultAsync(rule => rule.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Alert rule '{id}' was not found.");

        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.MetricName = request.MetricName;
        entity.Aggregation = request.Aggregation;
        entity.Threshold = request.Threshold;
        entity.Operator = request.Operator;
        entity.Window = TimeSpan.FromMinutes(Math.Max(request.WindowMinutes, 1));
        entity.Enabled = request.Enabled;
        entity.Severity = request.Severity;
        entity.NotificationChannel = request.NotificationChannel;
        entity.LabelFilters = request.LabelFilters;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<IReadOnlyCollection<AlertRuleDto>> ListAsync(CancellationToken cancellationToken)
    {
        var rules = await _dbContext.AlertRules.AsNoTracking()
            .OrderByDescending(rule => rule.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return rules.Select(ToDto).ToList();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.AlertRules.SingleOrDefaultAsync(rule => rule.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Alert rule '{id}' was not found.");

        _dbContext.AlertRules.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AlertEvaluationResult> EvaluateAsync(Guid id, CancellationToken cancellationToken)
    {
        var rule = await _dbContext.AlertRules.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"Alert rule '{id}' was not found.");

        var query = new MetricQuery
        {
            Name = rule.MetricName,
            Aggregation = rule.Aggregation == MetricAggregation.None ? MetricAggregation.Sum : rule.Aggregation,
            From = DateTimeOffset.UtcNow.Subtract(rule.Window),
            To = DateTimeOffset.UtcNow,
            LabelFilters = rule.LabelFilters,
            Limit = 1_000
        };

        var dataPoints = await _metricHistoryService.QueryAsync(query, cancellationToken);
        var observed = dataPoints.Count == 0 ? 0 : dataPoints.Last().Value;

        var triggered = Evaluate(rule.Operator, observed, rule.Threshold) && rule.Enabled;

        if (triggered)
        {
            _logger.LogWarning(
                "Alert rule {RuleName} triggered. Value {Observed} {Operator} {Threshold}",
                rule.Name,
                observed,
                rule.Operator,
                rule.Threshold);
        }

        return new AlertEvaluationResult(
            rule.Id,
            triggered,
            observed,
            rule.Threshold,
            rule.Operator,
            DateTimeOffset.UtcNow,
            dataPoints.LastOrDefault()?.Timestamp);
    }

    private static bool Evaluate(AlertComparisonOperator @operator, double observed, double threshold) =>
        @operator switch
        {
            AlertComparisonOperator.GreaterThan => observed > threshold,
            AlertComparisonOperator.GreaterThanOrEqual => observed >= threshold,
            AlertComparisonOperator.LessThan => observed < threshold,
            AlertComparisonOperator.LessThanOrEqual => observed <= threshold,
            AlertComparisonOperator.Equal => Math.Abs(observed - threshold) < double.Epsilon,
            AlertComparisonOperator.NotEqual => Math.Abs(observed - threshold) >= double.Epsilon,
            _ => false
        };

    private static AlertRuleDto ToDto(AlertRule entity) =>
        new(
            entity.Id,
            entity.Name,
            entity.MetricName,
            entity.Aggregation,
            entity.Threshold,
            entity.Operator,
            entity.Window,
            entity.Enabled,
            entity.Severity,
            entity.NotificationChannel,
            entity.LabelFilters,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc,
            entity.Description);
}
