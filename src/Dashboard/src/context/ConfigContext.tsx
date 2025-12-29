import { createContext, useContext, useMemo, type ReactNode } from 'react';
import { buildUrl } from '../api/client';

interface ConfigContextValue {
  gatewayBaseUrl: string;
  buildGatewayUrl: (path: string, params?: Record<string, string>) => string;
}

const ConfigContext = createContext<ConfigContextValue | null>(null);

export function ConfigProvider({ children }: { children: ReactNode }) {
  const value = useMemo(() => {
    const configValue = window.__DASHBOARD_CONFIG__?.gatewayBaseUrl ?? '';
    const envValue = import.meta.env.VITE_GATEWAY_BASE_URL ?? '';
    const gatewayBaseUrl = configValue || envValue;

    return {
      gatewayBaseUrl,
      buildGatewayUrl: (path: string, params?: Record<string, string>) =>
        buildUrl(gatewayBaseUrl, path, params),
    };
  }, []);

  return <ConfigContext.Provider value={value}>{children}</ConfigContext.Provider>;
}

export function useConfig(): ConfigContextValue {
  const context = useContext(ConfigContext);
  if (!context) {
    throw new Error('useConfig must be used within a ConfigProvider');
  }
  return context;
}
