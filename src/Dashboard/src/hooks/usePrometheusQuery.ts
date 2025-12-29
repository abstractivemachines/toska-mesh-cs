import { useEffect, useState, useCallback } from 'react';
import { useConfig } from '../context';
import { queryRange, queryInstant } from '../api/prometheus';
import type { PrometheusResult } from '../types/api';

interface UsePrometheusQueryResult {
  data: PrometheusResult | null;
  loading: boolean;
  error: string | null;
  refetch: () => void;
}

export function usePrometheusInstantQuery(query: string): UsePrometheusQueryResult {
  const { gatewayBaseUrl } = useConfig();
  const [data, setData] = useState<PrometheusResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [refetchCount, setRefetchCount] = useState(0);

  const refetch = useCallback(() => {
    setRefetchCount((c) => c + 1);
  }, []);

  useEffect(() => {
    if (!query) {
      setData(null);
      setLoading(false);
      return;
    }

    const controller = new AbortController();
    setLoading(true);
    setError(null);

    queryInstant(gatewayBaseUrl, query, undefined, controller.signal)
      .then((result) => {
        if (result.status === 'error') {
          setError(result.error || 'Query failed');
        } else {
          setData(result);
          setError(null);
        }
      })
      .catch((err) => {
        if (err.name === 'AbortError') return;
        setError(err.message || 'Failed to query Prometheus');
      })
      .finally(() => {
        setLoading(false);
      });

    return () => controller.abort();
  }, [gatewayBaseUrl, query, refetchCount]);

  return { data, loading, error, refetch };
}

interface UsePrometheusRangeQueryResult extends UsePrometheusQueryResult {}

export function usePrometheusRangeQuery(
  query: string,
  start: number,
  end: number,
  step: string
): UsePrometheusRangeQueryResult {
  const { gatewayBaseUrl } = useConfig();
  const [data, setData] = useState<PrometheusResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [refetchCount, setRefetchCount] = useState(0);

  const refetch = useCallback(() => {
    setRefetchCount((c) => c + 1);
  }, []);

  useEffect(() => {
    if (!query) {
      setData(null);
      setLoading(false);
      return;
    }

    const controller = new AbortController();
    setLoading(true);
    setError(null);

    queryRange(gatewayBaseUrl, query, start, end, step, controller.signal)
      .then((result) => {
        if (result.status === 'error') {
          setError(result.error || 'Query failed');
        } else {
          setData(result);
          setError(null);
        }
      })
      .catch((err) => {
        if (err.name === 'AbortError') return;
        setError(err.message || 'Failed to query Prometheus');
      })
      .finally(() => {
        setLoading(false);
      });

    return () => controller.abort();
  }, [gatewayBaseUrl, query, start, end, step, refetchCount]);

  return { data, loading, error, refetch };
}
