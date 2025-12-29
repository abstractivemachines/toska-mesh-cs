import type { HealthStatus } from '../../types/api';

interface StatusBadgeProps {
  status: HealthStatus | string;
  size?: 'small' | 'medium';
}

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
  const colorClass = STATUS_COLORS[status] || 'status-unknown';
  const sizeClass = size === 'small' ? 'pill-small' : '';
  const displayLabel = STATUS_LABELS[status] || status;

  return (
    <span className={`pill ${colorClass} ${sizeClass}`.trim()} title={`Status: ${status}`}>
      {displayLabel}
    </span>
  );
}
