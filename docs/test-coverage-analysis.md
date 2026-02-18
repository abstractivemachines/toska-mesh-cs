# ToskaMesh Test Coverage Analysis

## Executive Summary

The ToskaMesh codebase contains **18 projects** with **164 C# source files** but only **8 test projects** with **17 test files** and **89 test methods**. Ten projects (56%) have **zero test coverage**, including critical shared libraries and core infrastructure components. The projects that do have tests often cover only 1-2 classes out of many, leaving significant logic untested.

---

## Current Coverage Matrix

| Project | Source Files | Test Files | Test Methods | Coverage Level |
|---------|-------------|-----------|--------------|----------------|
| ToskaMesh.Common | 20 | 0 | 0 | **None** |
| ToskaMesh.Protocols | 4 | 0 | 0 | **None** (interfaces only) |
| ToskaMesh.Security | 6 | 1 | 1 | **Minimal** |
| ToskaMesh.Telemetry | 9 | 0 | 0 | **None** |
| ToskaMesh.Runtime | 14 | 5 | 14 | Partial |
| ToskaMesh.Runtime.Stateful | 3 | (shared) | (shared) | Partial |
| ToskaMesh.Runtime.Orleans | 2 | 0 | 0 | **None** |
| ToskaMesh.Core | 10 | 0 | 0 | **None** |
| ToskaMesh.Discovery | 9 | 0 | 0 | **None** |
| ToskaMesh.Gateway | 16 | 0 | 0 | **None** |
| ToskaMesh.Router | 2 | 1 | 12 | Good |
| ToskaMesh.HealthMonitor | 5 | 0 | 0 | **None** |
| ToskaMesh.AuthService | 10 | 1 | 10 | Minimal |
| ToskaMesh.ConfigService | 8 | 2 | 19 | Moderate |
| ToskaMesh.MetricsService | 15 | 2 | 15 | Minimal |
| ToskaMesh.TracingService | 16 | 2 | 22 | Minimal |
| ToskaMesh.ObservabilityService | 8 | 2 | 3 | **Minimal** |

---

## Priority 1: Critical Gaps (Security & Resilience)

These are the highest-impact areas where missing tests pose the most risk.

### 1.1 ToskaMesh.Security — JwtTokenService, PasswordHasher, ApiKeyAuthenticationHandler, SecretValidation

**Current state:** Only `MeshServiceTokenProvider` has a single test. The four classes below handle authentication, password storage, and secret validation — all security-critical — yet have zero tests.

**JwtTokenService** (`src/Shared/ToskaMesh.Security/JwtTokenService.cs`)
- `GenerateToken(userId, userName, roles?)` — produces a signed JWT with claims
- `ValidateToken(token)` — validates signature, expiry, issuer, audience
- Branches: role claim insertion, four distinct exception-handling paths (expired, bad signature, generic token error, unknown exception)
- Recommended tests:
  - Token round-trip: generate then validate returns correct claims
  - Expired token returns null
  - Tampered signature returns null
  - Roles are included as claims when provided
  - Null/empty roles omit role claims
  - Constructor rejects placeholder or short secrets via `SecretValidation`

**PasswordHasher** (`src/Shared/ToskaMesh.Security/PasswordHasher.cs`)
- `HashPassword(password)` / `VerifyPassword(password, hash)`
- Branches: work factor range validation (4–31), null/empty input guards, `SaltParseException` handling
- Recommended tests:
  - Hash + verify round-trip succeeds
  - Wrong password fails verification
  - Same password produces different hashes (salt variance)
  - Work factor out of range throws
  - Null/empty inputs throw

**ApiKeyAuthenticationHandler** (`src/Shared/ToskaMesh.Security/ApiKeyAuthenticationHandler.cs`)
- `HandleAuthenticateAsync()` — reads `X-API-Key` header, looks up in configured key map
- Branches: missing header, empty value, unknown key, valid key with roles
- Recommended tests:
  - Missing header returns `AuthenticateResult.NoResult()`
  - Invalid key returns `AuthenticateResult.Fail()`
  - Valid key returns success with correct claims (ServiceName, ServiceId, roles)
  - Challenge sets 401 + `WWW-Authenticate` header
  - Forbidden sets 403

**SecretValidation** (`src/Shared/ToskaMesh.Security/SecretValidation.cs`)
- `EnsureSecureSecret(secret, minLength, settingName)` — rejects null, short, or placeholder values
- `LooksLikePlaceholder(value)` — detects strings like `change-me`, `your-secret-key`
- Recommended tests:
  - Null/whitespace secret throws with setting name in message
  - Short secret throws
  - Each placeholder pattern detected (case-insensitive)
  - Valid secret passes

---

### 1.2 ToskaMesh.Common — RetryPolicy and PollyCircuitBreaker

**Current state:** Zero tests for any of the 20 files in ToskaMesh.Common.

**RetryPolicy** (`src/Shared/ToskaMesh.Common/Utilities/RetryPolicy.cs`)
- `ExecuteAsync<T>(operation, options?, shouldRetry?, ct)` — retries with exponential backoff + optional jitter
- Branches: default options, default predicate, jitter toggle, max-delay cap, backoff multiplier, cancellation
- Recommended tests:
  - Success on first attempt — no retry
  - Success after N failures — correct number of retries
  - Exceeding max retries throws the original exception
  - `shouldRetry` predicate filters specific exception types
  - Jitter enabled produces variable delays
  - Delay capped at `MaxDelay`
  - Cancellation token aborts retry loop

**PollyCircuitBreaker** (`src/Shared/ToskaMesh.Common/Resilience/PollyCircuitBreaker.cs`)
- `ExecuteAsync<T>(action, ct)` / `RecordSuccess()` / `RecordFailure(ex)`
- Branches: state transitions (Closed→Open→HalfOpen→Closed), `StateChanged` event firing, thread-safe state access
- Recommended tests:
  - Successful execution when closed
  - Exception propagation when circuit is open
  - `StateChanged` event fires with correct previous/new states
  - `RecordSuccess` / `RecordFailure` log correctly

---

## Priority 2: Core Infrastructure (Zero Coverage)

These projects form the backbone of the mesh but have no tests at all.

### 2.1 ToskaMesh.Discovery — ServiceManager

**ServiceManager** (`src/Core/ToskaMesh.Discovery/Services/ServiceManager.cs`) is the central service lifecycle coordinator. It manages registration, deregistration, health checks, metadata, and event publishing.

Key untested logic:
- `RegisterAsync` — publishes `ServiceRegisteredEvent` via MassTransit, tracks registration time
- `DeregisterAsync` — publishes `ServiceDeregisteredEvent`, removes tracking
- `UpdateHealthAsync` — detects status changes, publishes `ServiceHealthChangedEvent` only on actual change
- `PerformHealthChecksAsync` — iterates all services, probes HTTP endpoints, falls back to TCP
- `GetMetadataSummaryAsync` — aggregates metadata across instances

Recommended tests (mock `IServiceRegistry`, `IPublishEndpoint`, `IHttpClientFactory`):
- Register publishes event and creates tracking entry
- Deregister publishes event and removes tracking
- Health update publishes event only when status actually changes
- Health check HTTP probe maps status codes correctly (200=Healthy, 5xx=Unhealthy)
- Health check falls back to TCP when HTTP endpoint is absent
- Metadata summary groups and counts correctly
- Concurrent registration/deregistration is safe

### 2.2 ToskaMesh.Gateway — ConsulProxyConfigProvider

**ConsulProxyConfigProvider** (`src/Core/ToskaMesh.Gateway/Services/ConsulProxyConfigProvider.cs`) translates Consul service catalog into YARP proxy routes dynamically.

Key untested logic:
- `UpdateConfigAsync` — creates YARP routes and clusters from service instances
- Route creation with path patterns, cluster creation with destinations
- Filtering out Consul system services
- Skipping services with no healthy instances
- `NormalizePrefix` — adds leading/trailing slashes
- Destination address formatting (`scheme://address:port`)

Recommended tests (mock `IServiceRegistry`):
- Routes created for each discovered service
- Consul system services filtered out
- Empty/unhealthy services produce no routes
- Prefix normalization edge cases (null, missing slashes, duplicates)
- Destination addresses formatted correctly with scheme from metadata

### 2.3 ToskaMesh.Core — MeshCoordinator

**MeshCoordinator** (`src/Core/ToskaMesh.Core/Services/MeshCoordinator.cs`) manages cluster membership and pub/sub via Orleans grains.

Key untested logic:
- `BroadcastAsync` — JSON serializes messages with type info
- `SubscribeAsync` — registers handlers per topic, creates observer
- Event dispatching — type matching by `AssemblyQualifiedName`, `FullName`, or `Name`
- `JoinClusterAsync` / `LeaveClusterAsync` — DTO conversion and grain calls

Recommended tests (mock `IClusterClient`):
- Broadcast serializes with type information
- Subscribe registers handler and creates observer on first call
- Event dispatch matches types correctly (all three name formats)
- Multiple subscribers to same topic all receive events

### 2.4 ToskaMesh.HealthMonitor — HealthProbeWorker, HealthReportCache

**HealthProbeWorker** (`src/Core/ToskaMesh.HealthMonitor/Services/HealthProbeWorker.cs`) probes service health via HTTP and TCP with circuit breaker protection.

Key untested logic:
- HTTP probe with custom headers from metadata
- TCP probe with timeout handling
- Probe fallback chain (HTTP → TCP → Unknown)
- Circuit breaker integration (`BrokenCircuitException` handling)
- Cache updates with probe type and message

**HealthReportCache** (`src/Core/ToskaMesh.HealthMonitor/Services/HealthReportCache.cs`) is a thread-safe in-memory cache.

Key untested logic:
- `GetOrAdd` with concurrent dictionary
- `GetByService` case-insensitive filtering
- `Update` creates or updates `MonitoredInstance` with timestamp

Both are pure in-process components with clear interfaces — straightforward to test with mocks.

---

## Priority 3: Shared Libraries (Zero Coverage)

### 3.1 ToskaMesh.Common — GlobalExceptionMiddleware

**GlobalExceptionMiddleware** (`src/Shared/ToskaMesh.Common/Middleware/GlobalExceptionMiddleware.cs`) maps exceptions to HTTP status codes.

Untested branches:
- `ArgumentException` / `ArgumentNullException` → 400
- `UnauthorizedAccessException` → 401
- `KeyNotFoundException` → 404
- `InvalidOperationException` → 409
- `ValidationException` → 400 with error list
- Default → 500 with generic message

Every branch should be tested — this is the global error boundary for all services.

### 3.2 ToskaMesh.Common — ApiResponse

**ApiResponse** (`src/Shared/ToskaMesh.Common/ApiResponse.cs`) is the standard response envelope.

Static factory methods (`SuccessResponse`, `ErrorResponse`) with multiple overloads. Pure logic, no dependencies — trivial to test but currently untested. Should verify:
- `Success` flag set correctly
- Error messages joined with `;` separator
- Timestamp populated
- TraceId passed through

### 3.3 ToskaMesh.Common — ValidationExtensions

**ValidationExtensions** (`src/Shared/ToskaMesh.Common/Validation/ValidationExtensions.cs`) provides shared validation rules.

Key untested rules:
- `MustBeValidUrl` — HTTP/HTTPS only
- `MustBeValidPort` — 1–65535 range
- `MustBeValidIPv4` — rejects IPv6 and invalid formats
- `NotEmptyOrWhitespace` — rejects empty and whitespace-only strings
- `ValidateAndThrow` / `ValidateAndThrowAsync` — throw `ValidationException` with error list

### 3.4 ToskaMesh.Common — ConsulServiceRegistry, GrpcServiceRegistry

Both service registry implementations have complex mapping logic:
- Health status mapping (Consul health checks → domain `HealthStatus` enum)
- TTL interval calculation with buffer and minimum enforcement
- gRPC proto ↔ domain model bidirectional mapping
- Metadata dictionary population

### 3.5 ToskaMesh.Telemetry

Nine files with zero tests. Priority items:
- `MeshMetrics` — counter/histogram registration and recording
- `TracingIngestExporter` — span export to tracing service endpoint
- `CorrelationIdEnricher` / `ServiceNameEnricher` — Serilog log enrichment

---

## Priority 4: Shallow Coverage in Existing Tests

These projects have tests but cover only a fraction of their logic.

### 4.1 ToskaMesh.Security — MeshServiceTokenProvider

**Current:** 1 test method covering token generation and caching.
**Missing:** Token expiration, refresh, different lifetimes, signature validation, claims variations, key rotation.

### 4.2 ToskaMesh.AuthService — RefreshTokenService

**Current:** 10 tests covering CRUD and basic validation.
**Missing:** `AuthController` (login, register, refresh, logout endpoints), `AuditService` (audit log creation), `EmailSender`, database entity relationships.

### 4.3 ToskaMesh.ObservabilityService — SloCalculator, TopologyGraphBuilder

**Current:** 3 tests total — one for SLO burn rate, two for topology graph.
**Missing:**
- `SloCalculator`: multi-window evaluation, compliance percentage, error budget tracking, severity levels
- `TopologyGraphBuilder`: multiple traces, cyclic dependencies, latency distribution
- `ObservabilityStore`: the main data store has zero tests
- `ObservabilitySeedData`: seed data generation untested
- `ObservabilityMetrics`: metrics registration untested

### 4.4 ToskaMesh.MetricsService

**Current:** 15 tests across AlertRuleService and MetricHistoryService.
**Missing:** `CustomMetricService`, `GrafanaProvisioningService`, `MetricsRegistry`, `MetricDefinitionWarmupService`, all three controllers (`AlertsController`, `GrafanaController`, `MetricsController`).

### 4.5 ToskaMesh.TracingService

**Current:** 22 tests across TraceStorageService and TraceAnalyticsService — the best-covered service.
**Missing:** `TraceRetentionService` (TTL/cleanup logic), `TraceSummaryRefreshService` (materialized view refresh), `TracesController` (API endpoints), `TraceSummaryBootstrapper` (DB initialization).

### 4.6 ToskaMesh.Runtime

**Current:** 14 tests across heartbeat, lambda, and stateful hosting.
**Missing:** `MeshAutoRegistrar`, `MeshServiceBootstrap`, `MeshRequestContext`, key-value store implementations (`RedisKeyValueStore`, `ToskaStoreKeyValueStore`, `MeshKeyValueProvider`).

---

## Recommended Action Plan

### Phase 1 — Security & Resilience (Highest Impact)

| New Test File | Target Class | Est. Tests |
|--------------|-------------|-----------|
| `PasswordHasherTests.cs` | PasswordHasher | 10 |
| `JwtTokenServiceTests.cs` | JwtTokenService | 12 |
| `SecretValidationTests.cs` | SecretValidation | 10 |
| `ApiKeyAuthenticationHandlerTests.cs` | ApiKeyAuthenticationHandler | 10 |
| `RetryPolicyTests.cs` | RetryPolicy | 10 |
| `PollyCircuitBreakerTests.cs` | PollyCircuitBreaker | 8 |

### Phase 2 — Core Infrastructure

| New Test File | Target Class | Est. Tests |
|--------------|-------------|-----------|
| `ServiceManagerTests.cs` | ServiceManager | 15 |
| `ConsulProxyConfigProviderTests.cs` | ConsulProxyConfigProvider | 12 |
| `HealthProbeWorkerTests.cs` | HealthProbeWorker | 12 |
| `HealthReportCacheTests.cs` | HealthReportCache | 8 |
| `MeshCoordinatorTests.cs` | MeshCoordinator | 12 |

### Phase 3 — Shared Libraries

| New Test File | Target Class | Est. Tests |
|--------------|-------------|-----------|
| `GlobalExceptionMiddlewareTests.cs` | GlobalExceptionMiddleware | 10 |
| `ApiResponseTests.cs` | ApiResponse | 10 |
| `ValidationExtensionsTests.cs` | ValidationExtensions | 14 |
| `ConsulServiceRegistryTests.cs` | ConsulServiceRegistry | 15 |
| `GrpcServiceRegistryTests.cs` | GrpcServiceRegistry | 12 |

### Phase 4 — Deepen Existing Coverage

| Existing Test Project | New Coverage Targets | Est. Tests |
|----------------------|---------------------|-----------|
| ToskaMesh.AuthService.Tests | AuthController, AuditService | 15 |
| ToskaMesh.ObservabilityService.Tests | ObservabilityStore, deeper SLO/Topology | 12 |
| ToskaMesh.MetricsService.Tests | CustomMetricService, MetricsRegistry | 10 |
| ToskaMesh.TracingService.Tests | TraceRetentionService, TraceSummaryRefreshService | 10 |
| ToskaMesh.Runtime.Tests | KeyValue stores, MeshAutoRegistrar | 10 |
| ToskaMesh.Security.Tests | Deeper MeshServiceTokenProvider | 8 |

---

## CI/CD Note

The current CI workflow (`ci.yml`) builds and tests only a subset of projects: ToskaMesh.Common, ToskaMesh.Security, ToskaMesh.Telemetry, ToskaMesh.Discovery, ToskaMesh.Gateway, and ToskaMesh.Router. As new test projects are added, the CI workflow should be updated to run `dotnet test` across the full solution to ensure all tests are executed on every PR.
