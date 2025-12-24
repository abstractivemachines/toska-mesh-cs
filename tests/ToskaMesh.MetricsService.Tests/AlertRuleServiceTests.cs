using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ToskaMesh.MetricsService.Data;
using ToskaMesh.MetricsService.Models;
using ToskaMesh.MetricsService.Services;
using Xunit;

namespace ToskaMesh.MetricsService.Tests;

public class AlertRuleServiceTests
{
    [Fact]
    public async Task EvaluateAsync_ReturnsTriggeredResult_WhenThresholdExceeded()
    {
        using var context = CreateDbContext();
        var history = new MetricHistoryService(context);
        var service = new AlertRuleService(context, history, NullLogger<AlertRuleService>.Instance);

        await history.RecordSampleAsync("mesh_latency_seconds", 0.5, MetricType.Histogram, null, "seconds", CancellationToken.None);
        await history.RecordSampleAsync("mesh_latency_seconds", 1.5, MetricType.Histogram, null, "seconds", CancellationToken.None);

        var rule = await service.CreateAsync(new CreateAlertRuleRequest
        {
            Name = "Latency high",
            MetricName = "mesh_latency_seconds",
            Aggregation = MetricAggregation.Max,
            Threshold = 1.0,
            Operator = AlertComparisonOperator.GreaterThan,
            WindowMinutes = 10
        }, CancellationToken.None);

        var result = await service.EvaluateAsync(rule.Id, CancellationToken.None);

        result.Triggered.Should().BeTrue();
        result.ObservedValue.Should().Be(1.5);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsNotTriggered_WhenBelowThreshold()
    {
        using var context = CreateDbContext();
        var history = new MetricHistoryService(context);
        var service = new AlertRuleService(context, history, NullLogger<AlertRuleService>.Instance);

        await history.RecordSampleAsync("error_rate", 0.02, MetricType.Gauge, null, "ratio", CancellationToken.None);
        await history.RecordSampleAsync("error_rate", 0.03, MetricType.Gauge, null, "ratio", CancellationToken.None);

        var rule = await service.CreateAsync(new CreateAlertRuleRequest
        {
            Name = "Error rate high",
            MetricName = "error_rate",
            Aggregation = MetricAggregation.Max,
            Threshold = 0.05,
            Operator = AlertComparisonOperator.GreaterThan,
            WindowMinutes = 10
        }, CancellationToken.None);

        var result = await service.EvaluateAsync(rule.Id, CancellationToken.None);

        result.Triggered.Should().BeFalse();
        result.ObservedValue.Should().Be(0.03);
    }

    [Fact]
    public async Task EvaluateAsync_WithLessThanOperator_TriggersWhenBelowThreshold()
    {
        using var context = CreateDbContext();
        var history = new MetricHistoryService(context);
        var service = new AlertRuleService(context, history, NullLogger<AlertRuleService>.Instance);

        await history.RecordSampleAsync("available_memory_gb", 0.5, MetricType.Gauge, null, "GB", CancellationToken.None);

        var rule = await service.CreateAsync(new CreateAlertRuleRequest
        {
            Name = "Low memory",
            MetricName = "available_memory_gb",
            Aggregation = MetricAggregation.Min,
            Threshold = 1.0,
            Operator = AlertComparisonOperator.LessThan,
            WindowMinutes = 10
        }, CancellationToken.None);

        var result = await service.EvaluateAsync(rule.Id, CancellationToken.None);

        result.Triggered.Should().BeTrue();
        result.ObservedValue.Should().Be(0.5);
    }

    [Fact]
    public async Task EvaluateAsync_WithEqualOperator_TriggersOnExactMatch()
    {
        using var context = CreateDbContext();
        var history = new MetricHistoryService(context);
        var service = new AlertRuleService(context, history, NullLogger<AlertRuleService>.Instance);

        await history.RecordSampleAsync("active_connections", 100, MetricType.Gauge, null, "count", CancellationToken.None);

        var rule = await service.CreateAsync(new CreateAlertRuleRequest
        {
            Name = "Connection limit reached",
            MetricName = "active_connections",
            Aggregation = MetricAggregation.Max,
            Threshold = 100,
            Operator = AlertComparisonOperator.Equal,
            WindowMinutes = 10
        }, CancellationToken.None);

        var result = await service.EvaluateAsync(rule.Id, CancellationToken.None);

        result.Triggered.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_WithGreaterThanOrEqual_TriggersOnEqualValue()
    {
        using var context = CreateDbContext();
        var history = new MetricHistoryService(context);
        var service = new AlertRuleService(context, history, NullLogger<AlertRuleService>.Instance);

        await history.RecordSampleAsync("queue_depth", 50, MetricType.Gauge, null, "messages", CancellationToken.None);

        var rule = await service.CreateAsync(new CreateAlertRuleRequest
        {
            Name = "Queue backing up",
            MetricName = "queue_depth",
            Aggregation = MetricAggregation.Max,
            Threshold = 50,
            Operator = AlertComparisonOperator.GreaterThanOrEqual,
            WindowMinutes = 10
        }, CancellationToken.None);

        var result = await service.EvaluateAsync(rule.Id, CancellationToken.None);

        result.Triggered.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_WithNoMatchingMetrics_ReturnsNotTriggered()
    {
        using var context = CreateDbContext();
        var history = new MetricHistoryService(context);
        var service = new AlertRuleService(context, history, NullLogger<AlertRuleService>.Instance);

        var rule = await service.CreateAsync(new CreateAlertRuleRequest
        {
            Name = "Missing metric alert",
            MetricName = "nonexistent_metric",
            Aggregation = MetricAggregation.Sum,
            Threshold = 100,
            Operator = AlertComparisonOperator.GreaterThan,
            WindowMinutes = 10
        }, CancellationToken.None);

        var result = await service.EvaluateAsync(rule.Id, CancellationToken.None);

        result.Triggered.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_PersistsRuleCorrectly()
    {
        using var context = CreateDbContext();
        var history = new MetricHistoryService(context);
        var service = new AlertRuleService(context, history, NullLogger<AlertRuleService>.Instance);

        var rule = await service.CreateAsync(new CreateAlertRuleRequest
        {
            Name = "Test Alert",
            MetricName = "test_metric",
            Aggregation = MetricAggregation.Average,
            Threshold = 75,
            Operator = AlertComparisonOperator.GreaterThan,
            WindowMinutes = 15
        }, CancellationToken.None);

        rule.Should().NotBeNull();
        rule.Name.Should().Be("Test Alert");
        rule.MetricName.Should().Be("test_metric");
        rule.Threshold.Should().Be(75);
        rule.Window.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task ListAsync_ReturnsAllRules()
    {
        using var context = CreateDbContext();
        var history = new MetricHistoryService(context);
        var service = new AlertRuleService(context, history, NullLogger<AlertRuleService>.Instance);

        await service.CreateAsync(new CreateAlertRuleRequest
        {
            Name = "Rule 1",
            MetricName = "metric_a",
            Aggregation = MetricAggregation.Sum,
            Threshold = 10,
            Operator = AlertComparisonOperator.GreaterThan,
            WindowMinutes = 5
        }, CancellationToken.None);

        await service.CreateAsync(new CreateAlertRuleRequest
        {
            Name = "Rule 2",
            MetricName = "metric_b",
            Aggregation = MetricAggregation.Max,
            Threshold = 20,
            Operator = AlertComparisonOperator.LessThan,
            WindowMinutes = 10
        }, CancellationToken.None);

        var rules = await service.ListAsync(CancellationToken.None);

        rules.Should().HaveCount(2);
    }

    private static MetricsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MetricsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MetricsDbContext(options);
    }
}
