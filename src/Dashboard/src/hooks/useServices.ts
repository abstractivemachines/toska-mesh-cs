import { useEffect, useState, useCallback } from 'react';
import { useConfig } from '../context';
import { getServices } from '../api/services';
import type { DashboardServiceCatalogItem } from '../types/api';

interface UseServicesResult {
  services: DashboardServiceCatalogItem[];
  loading: boolean;
  error: string | null;
  refetch: () => void;
}

export function useServices(): UseServicesResult {
  const { gatewayBaseUrl } = useConfig();
  const [services, setServices] = useState<DashboardServiceCatalogItem[]>([]);
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

    getServices(gatewayBaseUrl, controller.signal)
      .then((data) => {
        setServices(data);
        setError(null);
      })
      .catch((err) => {
        if (err.name === 'AbortError') return;
        setError(err.message || 'Failed to load services');
      })
      .finally(() => {
        setLoading(false);
      });

    return () => controller.abort();
  }, [gatewayBaseUrl, refetchCount]);

  return { services, loading, error, refetch };
}
