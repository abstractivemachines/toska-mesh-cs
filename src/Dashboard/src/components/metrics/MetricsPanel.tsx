import { useTimeRange, TimeRangeProvider } from '../../context';
import { usePrometheusRangeQuery } from '../../hooks';
import { Panel } from '../common';
import { TimeRangeSelector } from './TimeRangeSelector';
import { MetricCard } from './MetricCard';

interface MetricsPanelProps {
  serviceName: string;
}

function MetricsPanelContent({ serviceName }: MetricsPanelProps) {
  const { range } = useTimeRange();

  // Request rate (using histogram count as request counter)
  const requestRateQuery = `sum(rate(http_server_request_duration_seconds_count{service="${serviceName}"}[5m]))`;
  const requestRate = usePrometheusRangeQuery(requestRateQuery, range.start, range.end, range.step);

  // Error rate (5xx responses)
  const errorRateQuery = `sum(rate(http_server_request_duration_seconds_count{service="${serviceName}",http_response_status_code=~"5.."}[5m])) / sum(rate(http_server_request_duration_seconds_count{service="${serviceName}"}[5m]))`;
  const errorRate = usePrometheusRangeQuery(errorRateQuery, range.start, range.end, range.step);

  // Latency P95
  const latencyQuery = `histogram_quantile(0.95, sum(rate(http_server_request_duration_seconds_bucket{service="${serviceName}"}[5m])) by (le))`;
  const latency = usePrometheusRangeQuery(latencyQuery, range.start, range.end, range.step);

  const formatRate = (value: number) => `${value.toFixed(1)}/s`;
  const formatPercent = (value: number) => `${(value * 100).toFixed(1)}%`;
  const formatLatency = (value: number) => {
    if (value < 0.001) return `${(value * 1000000).toFixed(0)}µs`;
    if (value < 1) return `${(value * 1000).toFixed(1)}ms`;
    return `${value.toFixed(2)}s`;
  };

  return (
    <Panel title="Metrics" className="metrics-panel">
      <div className="metrics-header">
        <TimeRangeSelector />
      </div>
      <div className="metrics-grid">
        <MetricCard
          title="Request Rate"
          data={requestRate.data}
          loading={requestRate.loading}
          error={requestRate.error}
          color="#3b82f6"
          formatValue={formatRate}
        />
        <MetricCard
          title="Error Rate"
          data={errorRate.data}
          loading={errorRate.loading}
          error={errorRate.error}
          color="#ef4444"
          formatValue={formatPercent}
        />
        <MetricCard
          title="Latency (P95)"
          data={latency.data}
          loading={latency.loading}
          error={latency.error}
          color="#10b981"
          formatValue={formatLatency}
        />
      </div>
    </Panel>
  );
}

export function MetricsPanel({ serviceName }: MetricsPanelProps) {
  return (
    <TimeRangeProvider>
      <MetricsPanelContent serviceName={serviceName} />
    </TimeRangeProvider>
  );
}
