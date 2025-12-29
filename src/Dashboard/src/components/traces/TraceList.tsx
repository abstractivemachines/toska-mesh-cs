import { useState, useCallback } from 'react';
import { useTraces } from '../../hooks';
import { LoadingState, ErrorState } from '../common';
import { TraceRow } from './TraceRow';
import { TraceFilters } from './TraceFilters';
import type { TraceQueryParameters } from '../../types/api';

export function TraceList() {
  const [filters, setFilters] = useState<TraceQueryParameters>({});
  const { data, loading, error, refetch } = useTraces(filters);

  const handleFilterChange = useCallback((params: TraceQueryParameters) => {
    setFilters(params);
  }, []);

  return (
    <div className="trace-list">
      <TraceFilters onFilterChange={handleFilterChange} />

      {loading && <LoadingState message="Loading traces..." />}
      {!loading && error && <ErrorState message={error} onRetry={refetch} />}
      {!loading && !error && (!data || data.items.length === 0) && (
        <p className="muted">No traces found.</p>
      )}
      {!loading && !error && data && data.items.length > 0 && (
        <>
          <div className="table-container">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Trace ID</th>
                  <th>Service</th>
                  <th>Operation</th>
                  <th>Status</th>
                  <th>Duration</th>
                  <th>Spans</th>
                  <th>Started</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((trace) => (
                  <TraceRow key={trace.traceId} trace={trace} />
                ))}
              </tbody>
            </table>
          </div>
          <div className="pagination-info">
            Showing {data.items.length} of {data.total} traces (Page {data.page})
          </div>
        </>
      )}
    </div>
  );
}
