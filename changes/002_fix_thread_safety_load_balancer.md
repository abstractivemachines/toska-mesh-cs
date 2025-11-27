# Change: Fix Thread Safety in LoadBalancerService

**Date:** 2025-11-27
**Type:** Bug Fix
**File:** `src/Core/ToskaMesh.Router/Services/LoadBalancerService.cs`

## Summary

Fixed a thread safety issue in the `ServiceLoadStats.Report` method where `_totalResponseTicks` was being incremented without atomic operations.

## Problem

Line 223 was using a non-atomic compound assignment operator:
```csharp
_totalResponseTicks += result.ResponseTime.Ticks;
```

This is not thread-safe because the operation involves:
1. Reading the current value
2. Adding to it
3. Writing it back

In a concurrent environment, multiple threads could read the same value before any writes complete, causing lost updates.

## Solution

Replaced with `Interlocked.Add` which performs an atomic addition:
```csharp
Interlocked.Add(ref _totalResponseTicks, result.ResponseTime.Ticks);
```

This is consistent with how `_totalRequests`, `_successfulRequests`, and `_failedRequests` are already handled in the same class using `Interlocked.Increment`.

## Files Changed

- `src/Core/ToskaMesh.Router/Services/LoadBalancerService.cs:223` - Changed `+=` to `Interlocked.Add`
