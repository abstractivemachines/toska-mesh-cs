import { useEffect, useState, useCallback } from 'react';
import { useConfig } from '../context';
import { getTrace } from '../api/traces';
import type { TraceDetailDto } from '../types/api';

interface UseTraceDetailResult {
  trace: TraceDetailDto | null;
  loading: boolean;
  error: string | null;
  refetch: () => void;
}

export function useTraceDetail(traceId: string): UseTraceDetailResult {
  const { gatewayBaseUrl } = useConfig();
  const [trace, setTrace] = useState<TraceDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [refetchCount, setRefetchCount] = useState(0);

  const refetch = useCallback(() => {
    setRefetchCount((c) => c + 1);
  }, []);

  useEffect(() => {
    if (!traceId) {
      setTrace(null);
      setLoading(false);
      return;
    }

    const controller = new AbortController();
    setLoading(true);
    setError(null);

    getTrace(gatewayBaseUrl, traceId, controller.signal)
      .then((data) => {
        setTrace(data);
        setError(null);
      })
      .catch((err) => {
        if (err.name === 'AbortError') return;
        setError(err.message || 'Failed to load trace');
      })
      .finally(() => {
        setLoading(false);
      });

    return () => controller.abort();
  }, [gatewayBaseUrl, traceId, refetchCount]);

  return { trace, loading, error, refetch };
}
