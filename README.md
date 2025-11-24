# Toska Mesh - C# Implementation

A distributed service mesh implementation in C#/.NET 8, ported from the original Elixir version. This implementation provides a production-ready platform for managing distributed microservices with advanced features like service discovery, load balancing, health monitoring, and distributed tracing.

## Overview

This C# implementation maintains the hybrid architecture of the original Elixir version while leveraging the .NET ecosystem and best practices.

## Architecture

### Project Structure

```
ToskaMesh/
├── src/
│   ├── Core/                          # Core mesh components
│   │   ├── ToskaMesh.Core/           # Central coordinator with Orleans/MassTransit
│   │   ├── ToskaMesh.Discovery/      # Service discovery with Consul
│   │   ├── ToskaMesh.Gateway/        # API Gateway with YARP
│   │   ├── ToskaMesh.Router/         # Load balancing with Polly
│   │   └── ToskaMesh.HealthMonitor/  # Health checks and circuit breakers
│   │
│   ├── Services/                      # Business domain services
│   │   ├── ToskaMesh.AuthService/    # Authentication & JWT tokens
│   │   ├── ToskaMesh.ConfigService/  # Configuration management
│   │   ├── ToskaMesh.MetricsService/ # Metrics collection (Prometheus)
│   │   └── ToskaMesh.TracingService/ # Distributed tracing (OpenTelemetry)
│   │
│   └── Shared/                        # Shared libraries
│       ├── ToskaMesh.Common/         # Common utilities & response models
│       ├── ToskaMesh.Protocols/      # Interfaces & contracts
│       ├── ToskaMesh.Security/       # JWT & security utilities
│       └── ToskaMesh.Telemetry/      # OpenTelemetry configuration
│
├── tests/                            # Unit & integration tests
├── deployments/                      # Docker & Kubernetes configs
├── docs/                            # Documentation
└── ToskaMesh.sln                    # Solution file
```

## Technology Stack

### Framework & Runtime
- **.NET 8** - Latest LTS version
- **ASP.NET Core** - Web framework for HTTP APIs
- **C# 12** - Latest language features

### Core Dependencies

#### Clustering & Messaging
- **Microsoft.Orleans** (8.0.0) - Virtual actor model for distributed systems
- **MassTransit** (8.1.0) - Message-based coordination
- **MassTransit.RabbitMQ** - RabbitMQ transport

#### Service Discovery & Configuration
- **Consul** (1.6.10.9) - Service registry client
- **Steeltoe.Discovery.Consul** (3.2.0) - Cloud-native service discovery
- **YamlDotNet** (13.7.1) - YAML configuration parsing

#### API Gateway & Routing
- **Yarp.ReverseProxy** (2.1.0) - High-performance reverse proxy
- **AspNetCoreRateLimit** (5.0.0) - Rate limiting middleware

#### Resilience & Fault Tolerance
- **Polly** (8.2.0) - Circuit breakers, retry, timeout policies
- **Polly.Extensions.Http** (3.0.0) - HTTP resilience

#### Authentication & Security
- **Microsoft.AspNetCore.Identity.EntityFrameworkCore** (8.0.0) - Identity management
- **Microsoft.AspNetCore.Authentication.JwtBearer** (8.0.0) - JWT authentication
- **BCrypt.Net-Next** (4.0.3) - Password hashing

#### Data Access
- **Npgsql.EntityFrameworkCore.PostgreSQL** (8.0.0) - PostgreSQL provider
- **Microsoft.EntityFrameworkCore** (8.0.0) - ORM

#### Observability
- **OpenTelemetry** (1.7.0) - Distributed tracing & metrics
- **OpenTelemetry.Exporter.Prometheus.AspNetCore** (1.7.0-rc.1) - Prometheus exporter
- **prometheus-net.AspNetCore** (8.2.0) - Prometheus metrics
- **Serilog** (3.1.1) - Structured logging
- **AspNetCore.HealthChecks.UI** (7.0.2) - Health check dashboard

#### API Documentation
- **Swashbuckle.AspNetCore** (6.5.0) - OpenAPI/Swagger

## Getting Started

### Prerequisites

1. **.NET 8 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
2. **Docker** - For running infrastructure services
3. **PostgreSQL 15** - Database (or use Docker)
4. **Visual Studio 2022** or **VS Code** (optional)

### Installation

1. **Clone the repository**
```bash
cd toksa-mesh/csharp-implementation
```

2. **Restore dependencies**
```bash
dotnet restore
```

3. **Build the solution**
```bash
dotnet build
```

### Running with Docker

The easiest way to run the entire mesh is using Docker Compose:

```bash
cd deployments
docker-compose up -d
```

This will start:
- PostgreSQL (port 5432)
- Redis (port 6379)
- Consul (port 8500)
- Prometheus (port 9090)
- Grafana (port 3000)
- Gateway (port 5000)
- Discovery Service (port 5010)
- Auth Service (port 5001)
- Config Service (port 5002)
- Metrics Service (port 5003)

### Running Locally

1. **Start infrastructure services**
```bash
cd deployments
docker-compose up -d postgres redis consul prometheus grafana
```

2. **Run individual services**
```bash
# Terminal 1 - Gateway
cd src/Core/ToskaMesh.Gateway
dotnet run

# Terminal 2 - Auth Service
cd src/Services/ToskaMesh.AuthService
dotnet run

# Terminal 3 - Config Service
cd src/Services/ToskaMesh.ConfigService
dotnet run
```

## Key Features

### 1. Service Discovery
- Automatic service registration with Consul
- Health check integration
- Dynamic service lookup
- Load balancing across instances

### 2. API Gateway (YARP)
- Reverse proxy routing
- Request/response transformation
- Rate limiting
- Authentication middleware
- Distributed tracing

### 3. Load Balancing
- Multiple strategies: Round-robin, least connections, random, weighted
- Health-aware routing
- Circuit breakers with Polly
- Request tracking and metrics

### 4. Health Monitoring
- Configurable health checks
- Circuit breaker patterns
- Health check dashboard
- Automatic unhealthy instance removal

### 5. Authentication & Security
- JWT-based authentication
- BCrypt password hashing
- Role-based authorization
- Token refresh mechanism

### 6. Observability
- **Metrics**: Prometheus + Grafana dashboards
- **Tracing**: OpenTelemetry distributed tracing
- **Logging**: Structured logging with Serilog
- **Health Checks**: ASP.NET Core health endpoints

## Development

### Project Commands

```bash
# Build solution
dotnet build

# Run tests
dotnet test

# Format code
dotnet format

# Create migration (example for AuthService)
cd src/Services/ToskaMesh.AuthService
dotnet ef migrations add InitialCreate
dotnet ef database update

# Publish service
dotnet publish -c Release -o ./publish
```

### Adding a New Service

1. Create new project:
```bash
dotnet new webapi -n ToskaMesh.YourService -o src/Services/ToskaMesh.YourService
```

2. Add project to solution:
```bash
dotnet sln add src/Services/ToskaMesh.YourService/ToskaMesh.YourService.csproj
```

3. Add shared library references:
```bash
dotnet add src/Services/ToskaMesh.YourService reference src/Shared/ToskaMesh.Common
dotnet add src/Services/ToskaMesh.YourService reference src/Shared/ToskaMesh.Protocols
dotnet add src/Services/ToskaMesh.YourService reference src/Shared/ToskaMesh.Telemetry
```

4. Configure telemetry in Program.cs:
```csharp
builder.Services.AddMeshTelemetry("YourService");
```

## Configuration

### appsettings.json Example

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=toksa_mesh;Username=toksa_mesh;Password=your_password"
  },
  "Consul": {
    "Address": "http://localhost:8500",
    "ServiceName": "your-service",
    "ServiceId": "your-service-1"
  },
  "JwtToken": {
    "Secret": "your-super-secret-key-min-32-characters",
    "Issuer": "ToskaMesh",
    "Audience": "ToskaMesh",
    "ExpirationHours": 24
  }
}
```

## Testing

### Unit Tests
```bash
dotnet test tests/ToskaMesh.UnitTests
```

### Integration Tests
```bash
# Ensure Docker services are running
cd deployments
docker-compose up -d

# Run integration tests
dotnet test tests/ToskaMesh.IntegrationTests
```

## Deployment

### Docker
See `deployments/docker-compose.yml` for the complete Docker setup.

### Kubernetes
Kubernetes manifests will be added in future iterations.

### Environment Variables

Key environment variables for configuration:

- `ASPNETCORE_ENVIRONMENT` - Development, Staging, Production
- `ASPNETCORE_URLS` - Binding URLs (e.g., http://+:80)
- `ConnectionStrings__DefaultConnection` - PostgreSQL connection
- `Consul__Address` - Consul server address
- `JwtToken__Secret` - JWT signing secret

## Monitoring & Observability

### Metrics Endpoints
- Gateway: http://localhost:5000/metrics
- Auth Service: http://localhost:5001/metrics
- Config Service: http://localhost:5002/metrics
- Tracing Service: http://localhost:5004/metrics
- Metrics Service: http://localhost:5003/metrics
- Health Monitor: http://localhost:5005/metrics

### Dashboards
- Prometheus: http://localhost:9090
- Grafana: http://localhost:3000 (admin/admin). Dashboards auto-provisioned from `deployments/grafana/dashboards`:
  - RED metrics (`toska-red`)
  - Service resources (`toska-resources`)
  - Messaging & cache (`toska-messaging`)
- Consul UI: http://localhost:8500

### Alert Rules
- Prometheus alerting/recording rules live in `deployments/prometheus.rules.yml` (RED, resource usage, queue depth, cache hit ratio).
- RabbitMQ and Redis exporters are included in `deployments/docker-compose.yml` and scraped by Prometheus by default.
- For setup details see `docs/monitoring-setup.md`.

### Health Checks
- Gateway: http://localhost:5000/health
- Individual services: http://localhost:{port}/health

## Comparison with Elixir Version

| Feature | Elixir | C# |
|---------|--------|-----|
| **Runtime** | BEAM/OTP | .NET CLR |
| **Concurrency** | Lightweight processes | Tasks/Threads/Orleans Grains |
| **Clustering** | libcluster | Orleans/MassTransit |
| **Web Framework** | Phoenix | ASP.NET Core |
| **Reverse Proxy** | Plug | YARP |
| **Service Discovery** | Custom/Consul | Steeltoe/Consul |
| **Resilience** | GenServer supervision | Polly policies |
| **Metrics** | Telemetry | OpenTelemetry/Prometheus |
| **Database** | Ecto | Entity Framework Core |
| **Auth** | Guardian | ASP.NET Identity + JWT |

## Next Steps

### Phase 1 Complete ✓
- [x] Solution structure
- [x] Shared libraries
- [x] Core project scaffolding
- [x] Docker configuration

### Phase 2: Core Implementation
- [ ] Implement MeshCore coordinator
- [ ] Service discovery with Consul
- [ ] Gateway routing configuration
- [ ] Load balancer with Polly
- [ ] Health monitor implementation

### Phase 3: Services
- [ ] AuthService implementation
- [ ] ConfigService implementation
- [ ] MetricsService implementation
- [ ] TracingService implementation

### Phase 4: Testing & Deployment
- [ ] Unit tests
- [ ] Integration tests
- [ ] Kubernetes manifests
- [ ] CI/CD pipelines
- [ ] Production documentation

## Contributing

1. Follow C# coding conventions
2. Add XML documentation comments
3. Write unit tests for new features
4. Update this README for significant changes
5. Use `dotnet format` before committing

## License

MIT License - See LICENSE file for details

## Resources

- [.NET Documentation](https://docs.microsoft.com/dotnet/)
- [ASP.NET Core](https://docs.microsoft.com/aspnet/core/)
- [Orleans Documentation](https://docs.microsoft.com/dotnet/orleans/)
- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/)
- [Polly Documentation](https://github.com/App-vNext/Polly)
