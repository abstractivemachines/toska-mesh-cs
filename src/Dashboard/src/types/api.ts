/**
 * API type definitions for ToskaMesh Dashboard
 * Mirrors backend DTOs from:
 * - src/Core/ToskaMesh.Gateway/Models/DashboardDtos.cs
 * - src/Services/ToskaMesh.TracingService/Models/TraceDtos.cs
 * - src/Shared/ToskaMesh.Protocols/IServiceRegistry.cs
 */

// ============================================================================
// Common Types
// ============================================================================

export type HealthStatus = 'Unknown' | 'Healthy' | 'Unhealthy' | 'Degraded';

// ============================================================================
// Service Discovery Types
// ============================================================================

export interface ServiceInstance {
  serviceName: string;
  serviceId: string;
  address: string;
  port: number;
  status: HealthStatus;
  metadata: Record<string, string>;
  registeredAt: string;
  lastHealthCheck: string;
}

export interface DashboardHealthSnapshot {
  serviceId: string;
  serviceName: string;
  address: string;
  port: number;
  status: HealthStatus;
  lastProbe: string;
  lastProbeType: string;
  message: string | null;
  metadata: Record<string, string>;
}

export interface DashboardMetadataKeySummary {
  key: string;
  instanceCount: number;
  values: string[];
}

export interface DashboardServiceMetadataSummary {
  serviceName: string;
  instanceCount: number;
  generatedAt: string;
  keys: DashboardMetadataKeySummary[];
}

export interface DashboardServiceCatalogItem {
  serviceName: string;
  instances: ServiceInstance[];
  health: DashboardHealthSnapshot[];
  metadata: DashboardServiceMetadataSummary | null;
}

// ============================================================================
// Tracing Types
// ============================================================================

export interface TraceSpanDto {
  traceId: string;
  spanId: string;
  parentSpanId: string | null;
  serviceName: string;
  operationName: string;
  startTime: string;
  endTime: string;
  status: string;
  kind: string | null;
  correlationId: string | null;
  attributes: Record<string, string | null> | null;
  events: Record<string, string | null> | null;
  resourceAttributes: Record<string, string | null> | null;
  cpuUsage: number | null;
  memoryUsageMb: number | null;
}

export interface TraceSummaryDto {
  traceId: string;
  serviceName: string;
  operation: string;
  startTimeUtc: string;
  endTimeUtc: string;
  durationMs: number;
  status: string;
  spanCount: number;
  correlationId: string | null;
}

export interface TraceDetailDto {
  summary: TraceSummaryDto;
  spans: TraceSpanDto[];
}

export interface TraceQueryResponse {
  total: number;
  page: number;
  pageSize: number;
  items: TraceSummaryDto[];
}

export interface TraceQueryParameters {
  serviceName?: string;
  operationName?: string;
  status?: string;
  correlationId?: string;
  from?: string;
  to?: string;
  minDurationMs?: number;
  maxDurationMs?: number;
  page?: number;
  pageSize?: number;
}

export interface TracePerformanceResponse {
  averageDurationMs: number;
  p95DurationMs: number;
  errorRate: number;
  throughputPerMinute: number;
  services: ServiceLatencyBreakdown[];
  hotspots: OperationHotspot[];
}

export interface ServiceLatencyBreakdown {
  serviceName: string;
  averageDurationMs: number;
  p95DurationMs: number;
  errorRate: number;
}

export interface OperationHotspot {
  operationName: string;
  averageDurationMs: number;
  errorRate: number;
}

// ============================================================================
// Prometheus Types
// ============================================================================

export interface PrometheusResult {
  status: 'success' | 'error';
  data: PrometheusData;
  error?: string;
  errorType?: string;
}

export interface PrometheusData {
  resultType: 'matrix' | 'vector' | 'scalar' | 'string';
  result: PrometheusMetricResult[];
}

export interface PrometheusMetricResult {
  metric: Record<string, string>;
  values?: [number, string][]; // For range queries [timestamp, value]
  value?: [number, string]; // For instant queries [timestamp, value]
}

// ============================================================================
// Dashboard Config
// ============================================================================

export interface DashboardConfig {
  gatewayBaseUrl?: string;
}

declare global {
  interface Window {
    __DASHBOARD_CONFIG__?: DashboardConfig;
  }
}
