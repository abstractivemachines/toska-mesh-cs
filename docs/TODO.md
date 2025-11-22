# Toska Mesh TODOs

## Foundation & Tooling
- ~~Configure CI/CD (GitHub Actions) and integrate code-quality scanners (SonarQube, CodeQL).~~ (Done via GitHub Actions CI + CodeQL workflows)
- ~~Finish shared libraries: exception middleware, validation helpers, ICircuitBreaker, message contracts, password hasher wrapper, authorization policies, API-key authentication, tracing helpers, logging enrichers.~~
- ~~Complete Week-3 infrastructure: Orleans silo hosting, MassTransit + RabbitMQ setup, Consul client wiring, PostgreSQL management, Redis caching, standardized health checks.~~

## Core Mesh Services
- ~~Implement ToskaMesh.Core Orleans grains (coordination, membership, leader election, event broadcasting, persistence).~~
- ~~Extend Discovery with full Consul registration, instance tracking, health execution, metadata management, REST endpoints (align with plan).~~
- ~~Finish Gateway features: dynamic Consul routes, JWT enforcement, rate limiting, request logging, API versioning, strict CORS.~~
- ~~Build Router algorithms: round-robin, least connections, weighted RR, IP hash, health-aware routing, request metrics.~~
- ~~Deliver Health Monitor: HTTP/TCP/custom probes, Polly circuit breakers, bulkheads, dashboard, alerting, historical data.~~

- ## Business Services
- ~~Auth Service: registration/login, refresh tokens, password reset, email verification, RBAC, OAuth2 integrations, profile management, audit logging, DB schema/migrations, unit & integration tests.~~
- ~~Config Service: YAML parsing, versioning, env-specific configs, change notifications, validation schemas, CRUD API, rollback, persistence, tests.~~
- ~~Metrics Service: Prometheus aggregation, custom metrics, query API, alert rules, Grafana provisioning, historical storage, tests.~~
- ~~Tracing Service: OpenTelemetry ingestion, Jaeger/Zipkin exporters, query & visualization endpoints, sampling, correlation, performance analysis, tests.~~

## Integration & Deployment
- Inter-service communication: gRPC contracts, service-to-service auth, message queue flows, saga patterns, integration tests, E2E tracing validation, load tests (k6/JMeter).
- Containerization: create Dockerfiles for all services, finalize docker-compose coverage, ensure images build/run.
- Kubernetes/Helm: manifests (deployments, services, ConfigMaps, Secrets, ingress, HPA), Helm charts, optional operators, service-mesh integration.

## Testing & Quality
- Implement testing strategy (unit/integration/performance/E2E) with xUnit, Moq, Coverlet, Testcontainers, BenchmarkDotNet, k6/JMeter, Playwright/Selenium.
- Add Prometheus/Grafana dashboards, alert rules, and monitoring for RED metrics, resource usage, queue depth, cache hit ratios.
- Documentation deliverables: API/Swagger, architecture diagrams, deployment guides (local/Compose/K8s/cloud), troubleshooting, perf tuning, security best practices, migration guide, optional videos/blog posts.
