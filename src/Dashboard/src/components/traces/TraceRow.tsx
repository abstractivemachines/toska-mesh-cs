import type { TraceSummaryDto } from '../../types/api';
import { StatusBadge } from '../common';
import { navigate } from '../../router';

interface TraceRowProps {
  trace: TraceSummaryDto;
}

function formatDate(dateStr: string): string {
  try {
    const date = new Date(dateStr);
    return date.toLocaleString();
  } catch {
    return dateStr;
  }
}

function formatDuration(ms: number): string {
  if (ms < 1) return '<1ms';
  if (ms < 1000) return `${ms.toFixed(1)}ms`;
  return `${(ms / 1000).toFixed(2)}s`;
}

export function TraceRow({ trace }: TraceRowProps) {
  const handleClick = () => {
    navigate(`/traces/${encodeURIComponent(trace.traceId)}`);
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      handleClick();
    }
  };

  return (
    <tr
      className="trace-row clickable"
      onClick={handleClick}
      onKeyDown={handleKeyDown}
      tabIndex={0}
      role="button"
      aria-label={`View trace ${trace.traceId}`}
    >
      <td>
        <code className="trace-id">{trace.traceId.slice(0, 16)}...</code>
      </td>
      <td>{trace.serviceName}</td>
      <td>{trace.operation}</td>
      <td>
        <StatusBadge status={trace.status} size="small" />
      </td>
      <td className="numeric">{formatDuration(trace.durationMs)}</td>
      <td className="numeric">{trace.spanCount}</td>
      <td>{formatDate(trace.startTimeUtc)}</td>
    </tr>
  );
}
