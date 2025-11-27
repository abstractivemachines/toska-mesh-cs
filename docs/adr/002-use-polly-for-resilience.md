# ADR-002: Use Polly for Resilience

## Status
Accepted

## Context

Distributed systems need resilience patterns to handle transient failures, network issues, and downstream service degradation. The original Elixir implementation used OTP supervision trees for fault tolerance.

We needed a resilience library that provides:
- Circuit breakers
- Retry policies with backoff
- Timeout handling
- Bulkhead isolation
- Fallback strategies

Options considered:
1. **Polly** - Mature .NET resilience library
2. **Microsoft.Extensions.Http.Resilience** - Built on Polly, HTTP-focused
3. **Custom implementation** - Roll our own patterns
4. **Steeltoe** - Cloud-native resilience

## Decision

We chose **Polly** (v8.x) for the following reasons:

1. **Mature and Battle-Tested**: Widely adopted across the .NET ecosystem
2. **Comprehensive**: Supports all major resilience patterns
3. **Composable**: Policies can be combined into pipelines
4. **BSD-3 License**: Permissive open source, no commercial restrictions
5. **.NET Foundation Member**: Long-term stability and governance
6. **Polly v8**: New `ResiliencePipeline` API is more performant and composable

## Consequences

### Positive
- Declarative resilience policies
- Consistent approach across all services
- Easy to test with deterministic outcomes
- Telemetry integration for observability
- No licensing concerns

### Negative
- Another dependency to manage
- Polly v8 API differs from v7 (migration effort if using older patterns)
- Need to understand when to apply which policy

### Implementation Notes
- Created `ICircuitBreaker` abstraction in `ToskaMesh.Protocols`
- Implemented `PollyCircuitBreaker` in `ToskaMesh.Common.Resilience`
- Factory pattern (`ICircuitBreakerFactory`) for creating named breakers
- Default configuration via `CircuitBreakerOptions` class
