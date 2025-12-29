import type { ServiceInstance } from '../../types/api';
import { StatusBadge } from '../common';

interface InstanceTableProps {
  instances: ServiceInstance[];
}

function isValidDate(dateStr: string): boolean {
  if (!dateStr) return false;
  const date = new Date(dateStr);
  // Check for invalid date or default .NET DateTime (0001-01-01)
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

export function InstanceTable({ instances }: InstanceTableProps) {
  if (instances.length === 0) {
    return <p className="muted">No instances registered.</p>;
  }

  return (
    <div className="table-container">
      <table className="data-table">
        <thead>
          <tr>
            <th>Address</th>
            <th>Status</th>
            <th>Registered</th>
            <th>Last Health Check</th>
            <th>Metadata</th>
          </tr>
        </thead>
        <tbody>
          {instances.map((instance) => (
            <tr key={instance.serviceId}>
              <td>
                <code>{instance.address}:{instance.port}</code>
              </td>
              <td>
                <StatusBadge status={instance.status} size="small" />
              </td>
              <td>{formatDate(instance.registeredAt)}</td>
              <td>{formatDate(instance.lastHealthCheck)}</td>
              <td>
                {Object.keys(instance.metadata).length > 0 ? (
                  <details className="metadata-details">
                    <summary>{Object.keys(instance.metadata).length} keys</summary>
                    <dl className="metadata-list">
                      {Object.entries(instance.metadata).map(([key, value]) => (
                        <div key={key}>
                          <dt>{key}</dt>
                          <dd>{value}</dd>
                        </div>
                      ))}
                    </dl>
                  </details>
                ) : (
                  <span className="muted">None</span>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
