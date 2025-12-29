/**
 * Service catalog API functions
 */

import type { DashboardServiceCatalogItem } from '../types/api';
import { apiGet } from './client';

const SERVICES_PATH = '/api/dashboard/services';

export async function getServices(
  baseUrl: string,
  signal?: AbortSignal
): Promise<DashboardServiceCatalogItem[]> {
  return apiGet<DashboardServiceCatalogItem[]>(baseUrl, SERVICES_PATH, undefined, signal);
}

export async function getService(
  baseUrl: string,
  serviceName: string,
  signal?: AbortSignal
): Promise<DashboardServiceCatalogItem | null> {
  const services = await getServices(baseUrl, signal);
  return services.find((s) => s.serviceName === serviceName) ?? null;
}
