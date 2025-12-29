/**
 * Tracing API functions
 */

import type {
  TraceDetailDto,
  TracePerformanceResponse,
  TraceQueryParameters,
  TraceQueryResponse,
} from '../types/api';
import { apiGet } from './client';

const TRACES_BASE = '/api/dashboard/traces';

export async function getTraces(
  baseUrl: string,
  params?: TraceQueryParameters,
  signal?: AbortSignal
): Promise<TraceQueryResponse> {
  const queryParams: Record<string, string> = {};

  if (params) {
    if (params.serviceName) queryParams.serviceName = params.serviceName;
    if (params.operationName) queryParams.operationName = params.operationName;
    if (params.status) queryParams.status = params.status;
    if (params.correlationId) queryParams.correlationId = params.correlationId;
    if (params.from) queryParams.from = params.from;
    if (params.to) queryParams.to = params.to;
    if (params.minDurationMs !== undefined) queryParams.minDurationMs = params.minDurationMs.toString();
    if (params.maxDurationMs !== undefined) queryParams.maxDurationMs = params.maxDurationMs.toString();
    if (params.page !== undefined) queryParams.page = params.page.toString();
    if (params.pageSize !== undefined) queryParams.pageSize = params.pageSize.toString();
  }

  return apiGet<TraceQueryResponse>(baseUrl, TRACES_BASE, queryParams, signal);
}

export async function getTrace(
  baseUrl: string,
  traceId: string,
  signal?: AbortSignal
): Promise<TraceDetailDto> {
  return apiGet<TraceDetailDto>(baseUrl, `${TRACES_BASE}/${encodeURIComponent(traceId)}`, undefined, signal);
}

export async function getTracesByCorrelation(
  baseUrl: string,
  correlationId: string,
  signal?: AbortSignal
): Promise<TraceDetailDto[]> {
  return apiGet<TraceDetailDto[]>(
    baseUrl,
    `${TRACES_BASE}/correlation/${encodeURIComponent(correlationId)}`,
    undefined,
    signal
  );
}

export async function getTracePerformance(
  baseUrl: string,
  params?: {
    serviceName?: string;
    from?: string;
    to?: string;
    operationName?: string;
  },
  signal?: AbortSignal
): Promise<TracePerformanceResponse> {
  const queryParams: Record<string, string> = {};

  if (params) {
    if (params.serviceName) queryParams.serviceName = params.serviceName;
    if (params.from) queryParams.from = params.from;
    if (params.to) queryParams.to = params.to;
    if (params.operationName) queryParams.operationName = params.operationName;
  }

  return apiGet<TracePerformanceResponse>(baseUrl, `${TRACES_BASE}/performance`, queryParams, signal);
}

export async function getTraceServiceNames(
  baseUrl: string,
  signal?: AbortSignal
): Promise<string[]> {
  return apiGet<string[]>(baseUrl, `${TRACES_BASE}/services`, undefined, signal);
}

export async function getTraceOperations(
  baseUrl: string,
  serviceName: string,
  signal?: AbortSignal
): Promise<string[]> {
  return apiGet<string[]>(
    baseUrl,
    `${TRACES_BASE}/services/${encodeURIComponent(serviceName)}/operations`,
    undefined,
    signal
  );
}
