# ADR-004: Use YARP for API Gateway

## Status
Accepted

## Context

The API Gateway is the entry point for all external requests. It needs to:
- Route requests to appropriate backend services
- Handle authentication and authorization
- Apply rate limiting
- Provide request/response transformation
- Support dynamic route configuration

Options considered:
1. **YARP (Yet Another Reverse Proxy)** - Microsoft's high-performance reverse proxy
2. **Ocelot** - Popular .NET API Gateway
3. **Kong/Traefik** - External gateway solutions
4. **Custom middleware** - Build routing from scratch

## Decision

We chose **YARP** (v2.1.0) for the following reasons:

1. **Performance**: Built for high throughput, minimal overhead
2. **Microsoft Support**: First-party library, actively maintained
3. **Flexibility**: Fully customizable with middleware pipeline
4. **Dynamic Configuration**: Can update routes without restart
5. **Native .NET**: Integrates seamlessly with ASP.NET Core
6. **Extensibility**: Easy to add custom transforms and load balancing

## Consequences

### Positive
- Excellent performance benchmarks
- Full control over routing logic
- Easy integration with ASP.NET Core authentication
- Can load routes dynamically from Consul
- Supports WebSockets and gRPC proxying

### Negative
- More configuration code than declarative gateways like Ocelot
- Less out-of-box features (need to implement some features manually)
- Newer than alternatives (less community examples)

### Implementation Notes
- `ConsulProxyConfigProvider` dynamically loads routes from Consul
- Routes configured via `GatewayRoutingOptions`
- Rate limiting uses ASP.NET Core's built-in `RateLimiter`
- JWT authentication integrated via standard middleware
- X-Forwarded-For header respected for accurate client IP detection
