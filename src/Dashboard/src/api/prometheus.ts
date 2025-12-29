/**
 * Prometheus metrics API functions
 */

import type { PrometheusResult } from '../types/api';
import { apiGet } from './client';

const PROMETHEUS_BASE = '/api/dashboard/prometheus';

export async function queryInstant(
  baseUrl: string,
  query: string,
  time?: number,
  signal?: AbortSignal
): Promise<PrometheusResult> {
  const params: Record<string, string> = { query };
  if (time !== undefined) {
    params.time = time.toString();
  }
  return apiGet<PrometheusResult>(baseUrl, `${PROMETHEUS_BASE}/query`, params, signal);
}

export async function queryRange(
  baseUrl: string,
  query: string,
  start: number,
  end: number,
  step: string,
  signal?: AbortSignal
): Promise<PrometheusResult> {
  const params: Record<string, string> = {
    query,
    start: start.toString(),
    end: end.toString(),
    step,
  };
  return apiGet<PrometheusResult>(baseUrl, `${PROMETHEUS_BASE}/query-range`, params, signal);
}

export async function getLabels(
  baseUrl: string,
  signal?: AbortSignal
): Promise<PrometheusResult> {
  return apiGet<PrometheusResult>(baseUrl, `${PROMETHEUS_BASE}/labels`, undefined, signal);
}

export async function getSeries(
  baseUrl: string,
  match: string[],
  start?: number,
  end?: number,
  signal?: AbortSignal
): Promise<PrometheusResult> {
  const params: Record<string, string> = {};
  if (match.length > 0) {
    params['match[]'] = match.join(',');
  }
  if (start !== undefined) {
    params.start = start.toString();
  }
  if (end !== undefined) {
    params.end = end.toString();
  }
  return apiGet<PrometheusResult>(baseUrl, `${PROMETHEUS_BASE}/series`, params, signal);
}
