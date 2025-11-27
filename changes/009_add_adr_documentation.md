# Change: Add Architecture Decision Records (ADRs)

**Date:** 2025-11-27
**Type:** Documentation
**Files:**
- `docs/adr/README.md` (new)
- `docs/adr/001-use-orleans-for-clustering.md` (new)
- `docs/adr/002-use-polly-for-resilience.md` (new)
- `docs/adr/003-consul-service-discovery.md` (new)
- `docs/adr/004-yarp-api-gateway.md` (new)
- `docs/CODE_REVIEW.md` (updated)

## Summary

Added Architecture Decision Records (ADRs) to document key architectural choices and updated the CODE_REVIEW.md to reflect all completed improvements.

## ADRs Created

### ADR-001: Use Orleans for Clustering
Documents the decision to use Microsoft Orleans for distributed state management and virtual actor model, replacing BEAM/OTP from the Elixir version.

### ADR-002: Use Polly for Resilience
Documents the decision to use Polly (BSD-3 licensed) for circuit breakers, retry policies, and other resilience patterns.

### ADR-003: Use Consul for Service Discovery
Documents the decision to use HashiCorp Consul as the primary service discovery mechanism, with gRPC registry as an alternative.

### ADR-004: Use YARP for API Gateway
Documents the decision to use Microsoft's YARP (Yet Another Reverse Proxy) for the API gateway instead of alternatives like Ocelot.

## CODE_REVIEW.md Updates

Updated to reflect the status of all improvements made during the code review session:

- ✅ Thread safety fix in LoadBalancerService
- ✅ JWT exception logging
- ✅ Consul health check configuration extraction
- ✅ ConsulServiceRegistry timestamp tracking
- ✅ ICircuitBreaker Polly implementation
- ✅ JWT secret validation
- ✅ Rate limiter X-Forwarded-For support
- ✅ Stable hash for IPHash strategy
- ✅ ADR documentation

Added change log table at the bottom for future tracking.
