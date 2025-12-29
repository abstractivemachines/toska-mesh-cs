import { useEffect, useState, useCallback } from 'react';
import { useConfig } from '../context';
import { getTraces } from '../api/traces';
import type { TraceQueryParameters, TraceQueryResponse } from '../types/api';

interface UseTracesResult {
  data: TraceQueryResponse | null;
  loading: boolean;
  error: string | null;
  refetch: () => void;
}

export function useTraces(params?: TraceQueryParameters): UseTracesResult {
  const { gatewayBaseUrl } = useConfig();
  const [data, setData] = useState<TraceQueryResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [refetchCount, setRefetchCount] = useState(0);

  const refetch = useCallback(() => {
    setRefetchCount((c) => c + 1);
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError(null);

    getTraces(gatewayBaseUrl, params, controller.signal)
      .then((response) => {
        setData(response);
        setError(null);
      })
      .catch((err) => {
        if (err.name === 'AbortError') return;
        setError(err.message || 'Failed to load traces');
      })
      .finally(() => {
        setLoading(false);
      });

    return () => controller.abort();
  }, [gatewayBaseUrl, params, refetchCount]);

  return { data, loading, error, refetch };
}
