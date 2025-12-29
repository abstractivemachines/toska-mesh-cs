import type { TraceSpanDto } from '../../types/api';
import { useState } from 'react';

interface SpanWithLayout extends TraceSpanDto {
  left: number;
  width: number;
  depth: number;
  durationMs: number;
}

interface SpanBarProps {
  span: SpanWithLayout;
}

function formatDuration(ms: number): string {
  if (ms < 1) return '<1ms';
  if (ms < 1000) return `${ms.toFixed(1)}ms`;
  return `${(ms / 1000).toFixed(2)}s`;
}

export function SpanBar({ span }: SpanBarProps) {
  const [expanded, setExpanded] = useState(false);

  const isError = span.status === 'Error';
  const barColor = isError ? 'var(--color-error)' : 'var(--color-primary)';

  return (
    <div className="span-row" style={{ paddingLeft: `${span.depth * 20}px` }}>
      <div className="span-label">
        <button
          className="span-toggle"
          onClick={() => setExpanded(!expanded)}
          aria-expanded={expanded}
        >
          {expanded ? '−' : '+'}
        </button>
        <span className="span-service">{span.serviceName}</span>
        <span className="span-operation">{span.operationName}</span>
      </div>
      <div className="span-timeline">
        <div
          className="span-bar"
          style={{
            left: `${span.left}%`,
            width: `${Math.max(span.width, 0.5)}%`,
            backgroundColor: barColor,
          }}
          title={`${span.operationName}: ${formatDuration(span.durationMs)}`}
        />
        <span className="span-duration">{formatDuration(span.durationMs)}</span>
      </div>

      {expanded && (
        <div className="span-details">
          <dl className="span-attributes">
            <div>
              <dt>Span ID</dt>
              <dd><code>{span.spanId}</code></dd>
            </div>
            {span.parentSpanId && (
              <div>
                <dt>Parent Span</dt>
                <dd><code>{span.parentSpanId}</code></dd>
              </div>
            )}
            <div>
              <dt>Kind</dt>
              <dd>{span.kind || 'Internal'}</dd>
            </div>
            <div>
              <dt>Status</dt>
              <dd className={isError ? 'error' : ''}>{span.status}</dd>
            </div>
            {span.attributes && Object.keys(span.attributes).length > 0 && (
              <div className="span-attributes-section">
                <dt>Attributes</dt>
                <dd>
                  <dl className="nested-attributes">
                    {Object.entries(span.attributes).map(([key, value]) => (
                      <div key={key}>
                        <dt>{key}</dt>
                        <dd>{value || '-'}</dd>
                      </div>
                    ))}
                  </dl>
                </dd>
              </div>
            )}
          </dl>
        </div>
      )}
    </div>
  );
}

export type { SpanWithLayout };
