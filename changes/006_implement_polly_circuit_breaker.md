# Change: Implement ICircuitBreaker with Polly

**Date:** 2025-11-27
**Type:** Feature
**Files:**
- `src/Shared/ToskaMesh.Common/ToskaMesh.Common.csproj`
- `src/Shared/ToskaMesh.Common/Resilience/PollyCircuitBreaker.cs` (new)
- `src/Shared/ToskaMesh.Common/Resilience/CircuitBreakerExtensions.cs` (new)

## Summary

Implemented the `ICircuitBreaker` interface from `ToskaMesh.Protocols` using Polly 8.x resilience pipelines.

## Problem

The `ICircuitBreaker` interface was defined in `ToskaMesh.Protocols` but had no implementation, making it unusable.

## Solution

Created a Polly-based implementation with:

### 1. `PollyCircuitBreaker` class
- Implements `ICircuitBreaker` using Polly 8.x `ResiliencePipeline`
- Configurable failure ratio, sampling duration, minimum throughput, and break duration
- Raises `StateChanged` events on circuit state transitions
- Logs state changes at appropriate levels (Warning for open, Info for closed/half-open)

### 2. `CircuitBreakerOptions` class
- Configuration options following the established pattern with `SectionName` constant
- Sensible defaults:
  - `FailureRatio`: 0.5 (50%)
  - `SamplingDuration`: 30 seconds
  - `MinimumThroughput`: 10 requests
  - `BreakDuration`: 30 seconds

### 3. DI Extension Methods
- `AddCircuitBreaker(name, configure)` - Add a single named circuit breaker
- `AddCircuitBreaker(name, configuration)` - Add from IConfiguration
- `AddCircuitBreakerFactory(configureDefaults)` - Add factory for creating breakers on demand

### 4. `ICircuitBreakerFactory` interface and implementation
- Creates and caches named circuit breakers
- Useful when multiple circuit breakers are needed (e.g., one per downstream service)

## Usage Examples

### Single circuit breaker:
```csharp
services.AddCircuitBreaker("downstream-api", options =>
{
    options.FailureRatio = 0.5;
    options.BreakDuration = TimeSpan.FromSeconds(30);
});
```

### Factory for multiple circuit breakers:
```csharp
services.AddCircuitBreakerFactory();

// Later, in a service:
var breaker = _factory.GetOrCreate("service-name");
await breaker.ExecuteAsync(async () => await CallServiceAsync());
```

### Configuration-based:
```json
{
  "CircuitBreaker": {
    "FailureRatio": 0.5,
    "SamplingDuration": "00:00:30",
    "MinimumThroughput": 10,
    "BreakDuration": "00:00:30"
  }
}
```

## Files Changed

- `src/Shared/ToskaMesh.Common/ToskaMesh.Common.csproj` - Added Polly package reference
- `src/Shared/ToskaMesh.Common/Resilience/PollyCircuitBreaker.cs` - New implementation
- `src/Shared/ToskaMesh.Common/Resilience/CircuitBreakerExtensions.cs` - DI extensions and factory
