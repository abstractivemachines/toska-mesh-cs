# Monitoring & Alerting

This project ships with a ready-to-use Prometheus + Grafana stack that tracks RED metrics, resource usage, queue depth, and cache effectiveness.

## Quickstart (Docker Compose)

```bash
cd deployments
docker-compose up -d prometheus grafana rabbitmq-exporter redis-exporter
```

- Prometheus: http://localhost:9090
- Grafana: http://localhost:3000 (admin/admin)
- RabbitMQ Exporter: http://localhost:9419/metrics
- Redis Exporter: http://localhost:9121/metrics

## Prometheus Configuration

- Base config: `deployments/prometheus.yml`
- Recording/alerting rules: `deployments/prometheus.rules.yml`
  - RED metrics: request rate, error rate, p95 latency, active requests
  - Resources: CPU %, working-set memory
  - Messaging/Cache: RabbitMQ queue depth + publish rate, Redis cache hit ratio
  - Alerts: high error rate (>5%), high p95 latency (>250ms), CPU saturation, memory pressure, queue depth > 1k, cache hit ratio < 80%
- Scrape jobs cover gateway, auth/config/metrics/tracing/health-monitor services plus RabbitMQ and Redis exporters.

## Grafana Provisioning

- Datasource: `deployments/grafana/provisioning/datasources/datasource.yml` (Prometheus default datasource).
- Dashboards: provisioned from `deployments/grafana/dashboards/`:
  - `toska-red`: RED metrics per service
  - `toska-resources`: CPU and memory usage per service
  - `toska-messaging`: queue depth/publish rate and Redis cache hit ratio

## Verifications

1. Prometheus rules: open http://localhost:9090/rules and confirm `toskamesh-*` groups load.
2. Grafana dashboards: open Grafana → Dashboards → ToskaMesh folder and confirm panels populate.
3. Alert logic: in Prometheus → Alerts, check rule status (alerts will stay pending if thresholds not met).

## Kubernetes Notes

If you use kube-prometheus-stack, mount/copy `deployments/prometheus.rules.yml` into an `PrometheusRule` and reuse the dashboard JSON files by creating `ConfigMap` dashboard providers.
