import type { DashboardHealthSnapshot } from '../../types/api';
import { StatusBadge } from '../common';

interface HealthHistoryProps {
  snapshots: DashboardHealthSnapshot[];
}

function isValidDate(dateStr: string): boolean {
  if (!dateStr) return false;
  const date = new Date(dateStr);
  if (isNaN(date.getTime())) return false;
  if (date.getFullYear() <= 1) return false;
  return true;
}

function formatDate(dateStr: string): string {
  if (!isValidDate(dateStr)) {
    return '-';
  }
  try {
    const date = new Date(dateStr);
    return date.toLocaleString();
  } catch {
    return '-';
  }
}

export function HealthHistory({ snapshots }: HealthHistoryProps) {
  if (snapshots.length === 0) {
    return <p className="muted">No health snapshots available.</p>;
  }

  // Sort by lastProbe descending (most recent first)
  const sorted = [...snapshots].sort(
    (a, b) => new Date(b.lastProbe).getTime() - new Date(a.lastProbe).getTime()
  );

  return (
    <div className="table-container">
      <table className="data-table">
        <thead>
          <tr>
            <th>Instance</th>
            <th>Status</th>
            <th>Last Probe</th>
            <th>Probe Type</th>
            <th>Message</th>
          </tr>
        </thead>
        <tbody>
          {sorted.map((snapshot, index) => (
            <tr key={`${snapshot.serviceId}-${index}`}>
              <td>
                <code>{snapshot.address}:{snapshot.port}</code>
              </td>
              <td>
                <StatusBadge status={snapshot.status} size="small" />
              </td>
              <td>{formatDate(snapshot.lastProbe)}</td>
              <td>{snapshot.lastProbeType}</td>
              <td>{snapshot.message || <span className="muted">-</span>}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
