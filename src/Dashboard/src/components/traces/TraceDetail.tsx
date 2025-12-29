import { useTraceDetail } from '../../hooks';
import { Panel, LoadingState, ErrorState, StatusBadge } from '../common';
import { TraceWaterfall } from './TraceWaterfall';
import { Link } from '../../router';

interface TraceDetailProps {
  traceId: string;
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

export function TraceDetail({ traceId }: TraceDetailProps) {
  const { trace, loading, error, refetch } = useTraceDetail(traceId);

  if (loading) {
    return <LoadingState message="Loading trace..." />;
  }

  if (error) {
    return <ErrorState message={error} onRetry={refetch} />;
  }

  if (!trace) {
    return <ErrorState message={`Trace "${traceId}" not found`} />;
  }

  const { summary } = trace;

  return (
    <div className="trace-detail">
      <div className="trace-detail-header">
        <Link to="/traces" className="back-link">&larr; Back to Traces</Link>
        <div className="trace-title-row">
          <h2>Trace: {summary.traceId.slice(0, 16)}...</h2>
          <StatusBadge status={summary.status} />
        </div>
      </div>

      <Panel title="Summary" className="detail-panel">
        <dl className="trace-summary">
          <div>
            <dt>Trace ID</dt>
            <dd><code>{summary.traceId}</code></dd>
          </div>
          <div>
            <dt>Root Service</dt>
            <dd>{summary.serviceName}</dd>
          </div>
          <div>
            <dt>Operation</dt>
            <dd>{summary.operation}</dd>
          </div>
          <div>
            <dt>Duration</dt>
            <dd>{formatDuration(summary.durationMs)}</dd>
          </div>
          <div>
            <dt>Span Count</dt>
            <dd>{summary.spanCount}</dd>
          </div>
          <div>
            <dt>Started</dt>
            <dd>{formatDate(summary.startTimeUtc)}</dd>
          </div>
          <div>
            <dt>Ended</dt>
            <dd>{formatDate(summary.endTimeUtc)}</dd>
          </div>
          {summary.correlationId && (
            <div>
              <dt>Correlation ID</dt>
              <dd><code>{summary.correlationId}</code></dd>
            </div>
          )}
        </dl>
      </Panel>

      <Panel title="Waterfall" className="detail-panel waterfall-panel">
        <TraceWaterfall trace={trace} />
      </Panel>
    </div>
  );
}
