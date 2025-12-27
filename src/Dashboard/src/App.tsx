import { useEffect, useMemo, useState } from 'react';

type DashboardConfig = {
  gatewayBaseUrl?: string;
};

type DashboardServiceCatalogItem = {
  serviceName: string;
  instances: Array<{ serviceId: string; address: string; port: number; status: string | number }>;
  health: Array<{ serviceId: string; status: string | number; lastProbe: string }>;
};

declare global {
  interface Window {
    __DASHBOARD_CONFIG__?: DashboardConfig;
  }
}

function buildGatewayUrl(baseUrl: string, path: string) {
  const trimmed = baseUrl.replace(/\/+$/, '');
  if (!trimmed) {
    return path;
  }

  return `${trimmed}${path}`;
}

export default function App() {
  const gatewayBaseUrl = useMemo(() => {
    const configValue = window.__DASHBOARD_CONFIG__?.gatewayBaseUrl ?? '';
    const envValue = import.meta.env.VITE_GATEWAY_BASE_URL ?? '';
    return configValue || envValue;
  }, []);

  const [services, setServices] = useState<DashboardServiceCatalogItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const controller = new AbortController();
    const url = buildGatewayUrl(gatewayBaseUrl, '/api/dashboard/services');

    async function load() {
      try {
        const response = await fetch(url, { signal: controller.signal });
        if (!response.ok) {
          throw new Error(`Gateway returned ${response.status}`);
        }
        const payload = (await response.json()) as DashboardServiceCatalogItem[];
        setServices(payload);
      } catch (err) {
        if ((err as Error).name === 'AbortError') {
          return;
        }
        setError((err as Error).message);
      } finally {
        setLoading(false);
      }
    }

    load();

    return () => controller.abort();
  }, [gatewayBaseUrl]);

  return (
    <div className="app">
      <header>
        <div>
          <p className="eyebrow">ToskaMesh</p>
          <h1>Dashboard</h1>
        </div>
        <div className="meta">
          <span>Gateway</span>
          <strong>{gatewayBaseUrl || 'relative'}</strong>
        </div>
      </header>

      <section className="panel">
        <h2>Service Catalog</h2>
        {loading && <p>Loading services from the gateway...</p>}
        {!loading && error && (
          <p className="error">Unable to load services: {error}</p>
        )}
        {!loading && !error && services.length === 0 && (
          <p>No services discovered yet.</p>
        )}
        {!loading && !error && services.length > 0 && (
          <ul>
            {services.map((service) => (
              <li key={service.serviceName}>
                <div className="service-row">
                  <div>
                    <h3>{service.serviceName}</h3>
                    <p>
                      {service.instances.length} instance(s) •{' '}
                      {service.health.length} health snapshot(s)
                    </p>
                  </div>
                  <span className="pill">active</span>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
