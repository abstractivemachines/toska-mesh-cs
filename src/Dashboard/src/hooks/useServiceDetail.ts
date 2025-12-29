import { useEffect, useState, useCallback } from 'react';
import { useConfig } from '../context';
import { getService } from '../api/services';
import type { DashboardServiceCatalogItem } from '../types/api';

interface UseServiceDetailResult {
  service: DashboardServiceCatalogItem | null;
  loading: boolean;
  error: string | null;
  refetch: () => void;
}

export function useServiceDetail(serviceName: string): UseServiceDetailResult {
  const { gatewayBaseUrl } = useConfig();
  const [service, setService] = useState<DashboardServiceCatalogItem | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [refetchCount, setRefetchCount] = useState(0);

  const refetch = useCallback(() => {
    setRefetchCount((c) => c + 1);
  }, []);

  useEffect(() => {
    if (!serviceName) {
      setService(null);
      setLoading(false);
      return;
    }

    const controller = new AbortController();
    setLoading(true);
    setError(null);

    getService(gatewayBaseUrl, serviceName, controller.signal)
      .then((data) => {
        if (data) {
          setService(data);
          setError(null);
        } else {
          setError(`Service "${serviceName}" not found`);
        }
      })
      .catch((err) => {
        if (err.name === 'AbortError') return;
        setError(err.message || 'Failed to load service');
      })
      .finally(() => {
        setLoading(false);
      });

    return () => controller.abort();
  }, [gatewayBaseUrl, serviceName, refetchCount]);

  return { service, loading, error, refetch };
}
