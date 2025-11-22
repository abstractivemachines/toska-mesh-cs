namespace ToskaMesh.MetricsService.Models;

public enum MetricAggregation
{
    None = 0,
    Sum,
    Average,
    Min,
    Max,
    Latest
}

public enum MetricType
{
    Counter,
    Gauge,
    Histogram,
    Summary
}

public enum AlertComparisonOperator
{
    GreaterThan = 0,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Equal,
    NotEqual
}
