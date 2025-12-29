import type { HealthStatus } from '../../types/api';

interface StatusBadgeProps {
  status: HealthStatus | string | number;
  size?: 'small' | 'medium';
}

// Map numeric enum values to string names
// Based on HealthStatus enum: Unknown=0, Healthy=1, Unhealthy=2, Degraded=3
const NUMERIC_STATUS_MAP: Record<number, string> = {
  0: 'Unknown',
  1: 'Healthy',
  2: 'Unhealthy',
  3: 'Degraded',
};

const STATUS_COLORS: Record<string, string> = {
  Healthy: 'status-healthy',
  Unhealthy: 'status-unhealthy',
  Degraded: 'status-degraded',
  Unknown: 'status-unknown',
  Ok: 'status-healthy',
  Error: 'status-unhealthy',
  Unset: 'status-healthy', // OpenTelemetry "Unset" = no error
};

// Display-friendly status labels
const STATUS_LABELS: Record<string, string> = {
  Unset: 'OK',
  Unknown: 'Pending',
};

export function StatusBadge({ status, size = 'medium' }: StatusBadgeProps) {
  // Convert numeric status to string if needed
  const statusStr = typeof status === 'number'
    ? (NUMERIC_STATUS_MAP[status] || 'Unknown')
    : status;

  const colorClass = STATUS_COLORS[statusStr] || 'status-unknown';
  const sizeClass = size === 'small' ? 'pill-small' : '';
  const displayLabel = STATUS_LABELS[statusStr] || statusStr;

  return (
    <span className={`pill ${colorClass} ${sizeClass}`.trim()} title={`Status: ${statusStr}`}>
      {displayLabel}
    </span>
  );
}
