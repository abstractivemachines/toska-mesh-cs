import { Sparkline } from './Sparkline';
import type { PrometheusResult } from '../../types/api';

interface MetricCardProps {
  title: string;
  data: PrometheusResult | null;
  loading: boolean;
  error: string | null;
  unit?: string;
  color?: string;
  formatValue?: (value: number) => string;
}

function extractChartData(data: PrometheusResult | null): [number[], number[]] {
  if (!data || data.status !== 'success' || data.data.result.length === 0) {
    return [[], []];
  }

  const result = data.data.result[0];

  // Range query (matrix)
  if (result.values) {
    const timestamps: number[] = [];
    const values: number[] = [];

    for (const [ts, val] of result.values) {
      timestamps.push(ts);
      values.push(parseFloat(val) || 0);
    }

    return [timestamps, values];
  }

  // Instant query (vector)
  if (result.value) {
    const [ts, val] = result.value;
    return [[ts], [parseFloat(val) || 0]];
  }

  return [[], []];
}

function getCurrentValue(data: PrometheusResult | null): number | null {
  if (!data || data.status !== 'success' || data.data.result.length === 0) {
    return null;
  }

  const result = data.data.result[0];

  if (result.values && result.values.length > 0) {
    const lastValue = result.values[result.values.length - 1];
    return parseFloat(lastValue[1]) || 0;
  }

  if (result.value) {
    return parseFloat(result.value[1]) || 0;
  }

  return null;
}

const defaultFormatValue = (value: number): string => {
  if (value >= 1000000) return `${(value / 1000000).toFixed(1)}M`;
  if (value >= 1000) return `${(value / 1000).toFixed(1)}K`;
  if (value < 0.01 && value > 0) return value.toExponential(1);
  return value.toFixed(2);
};

export function MetricCard({
  title,
  data,
  loading,
  error,
  unit = '',
  color = '#3b82f6',
  formatValue = defaultFormatValue,
}: MetricCardProps) {
  const chartData = extractChartData(data);
  const currentValue = getCurrentValue(data);

  return (
    <div className="metric-card">
      <div className="metric-header">
        <span className="metric-title">{title}</span>
      </div>
      <div className="metric-body">
        {loading && <span className="muted">Loading...</span>}
        {!loading && error && (
          <span className="error" title={error}>
            No data
          </span>
        )}
        {!loading && !error && currentValue !== null && (
          <>
            <span className="metric-value">
              {formatValue(currentValue)}
              {unit && <span className="metric-unit">{unit}</span>}
            </span>
            <Sparkline data={chartData} color={color} width={120} height={32} />
          </>
        )}
        {!loading && !error && currentValue === null && (
          <span className="muted">N/A</span>
        )}
      </div>
    </div>
  );
}
