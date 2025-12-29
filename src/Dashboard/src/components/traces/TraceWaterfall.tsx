import type { TraceSpanDto, TraceDetailDto } from '../../types/api';
import { SpanBar, type SpanWithLayout } from './SpanBar';

interface TraceWaterfallProps {
  trace: TraceDetailDto;
}

function calculateDepth(span: TraceSpanDto, allSpans: TraceSpanDto[], cache: Map<string, number>): number {
  if (cache.has(span.spanId)) {
    return cache.get(span.spanId)!;
  }

  if (!span.parentSpanId) {
    cache.set(span.spanId, 0);
    return 0;
  }

  const parent = allSpans.find((s) => s.spanId === span.parentSpanId);
  if (!parent) {
    cache.set(span.spanId, 0);
    return 0;
  }

  const depth = 1 + calculateDepth(parent, allSpans, cache);
  cache.set(span.spanId, depth);
  return depth;
}

function calculateSpanLayout(spans: TraceSpanDto[]): SpanWithLayout[] {
  if (spans.length === 0) return [];

  // Find trace bounds
  const startTimes = spans.map((s) => new Date(s.startTime).getTime());
  const endTimes = spans.map((s) => new Date(s.endTime).getTime());
  const traceStart = Math.min(...startTimes);
  const traceEnd = Math.max(...endTimes);
  const traceDuration = traceEnd - traceStart;

  if (traceDuration === 0) {
    // All spans have same start/end
    return spans.map((span) => ({
      ...span,
      left: 0,
      width: 100,
      depth: 0,
      durationMs: 0,
    }));
  }

  const depthCache = new Map<string, number>();

  return spans.map((span) => {
    const spanStart = new Date(span.startTime).getTime();
    const spanEnd = new Date(span.endTime).getTime();
    const duration = spanEnd - spanStart;

    return {
      ...span,
      left: ((spanStart - traceStart) / traceDuration) * 100,
      width: (duration / traceDuration) * 100,
      depth: calculateDepth(span, spans, depthCache),
      durationMs: duration,
    };
  });
}

function sortSpansByHierarchy(spans: SpanWithLayout[]): SpanWithLayout[] {
  // Sort by start time first, then by depth
  const sorted = [...spans].sort((a, b) => {
    const startDiff = new Date(a.startTime).getTime() - new Date(b.startTime).getTime();
    if (startDiff !== 0) return startDiff;
    return a.depth - b.depth;
  });

  // Re-organize to show children under parents
  const result: SpanWithLayout[] = [];
  const added = new Set<string>();

  function addWithChildren(span: SpanWithLayout) {
    if (added.has(span.spanId)) return;
    added.add(span.spanId);
    result.push(span);

    // Find and add children
    const children = sorted.filter((s) => s.parentSpanId === span.spanId);
    for (const child of children) {
      addWithChildren(child);
    }
  }

  // Start with root spans
  const roots = sorted.filter((s) => !s.parentSpanId);
  for (const root of roots) {
    addWithChildren(root);
  }

  // Add any orphaned spans
  for (const span of sorted) {
    if (!added.has(span.spanId)) {
      result.push(span);
    }
  }

  return result;
}

export function TraceWaterfall({ trace }: TraceWaterfallProps) {
  const layout = calculateSpanLayout(trace.spans);
  const sortedSpans = sortSpansByHierarchy(layout);

  return (
    <div className="trace-waterfall">
      <div className="waterfall-header">
        <div className="waterfall-label-header">Service / Operation</div>
        <div className="waterfall-timeline-header">
          <span>0ms</span>
          <span>{trace.summary.durationMs.toFixed(1)}ms</span>
        </div>
      </div>
      <div className="waterfall-body">
        {sortedSpans.map((span) => (
          <SpanBar key={span.spanId} span={span} />
        ))}
      </div>
    </div>
  );
}
