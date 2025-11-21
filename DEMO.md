# ToskaMesh Demo Guide

## 📊 What We've Built

**Statistics:**
- **Lines of Code:** ~3,000 C# LOC
- **Projects:** 13 (4 services, 4 shared libraries, 5 placeholder services)
- **Implemented Services:** 2 (Gateway, Discovery)
- **Infrastructure Components:** 4 (Consul, RabbitMQ, PostgreSQL, Redis)

## 🏗️ Architecture

```
┌──────────┐
│ Clients  │
└────┬─────┘
     │
     ▼
┌────────────────────────┐
│  Gateway (Port 5000)   │  ← JWT Auth, Rate Limiting, Dynamic Routing
└────────┬───────────────┘
         │
         ▼
┌────────────────────────┐
│ Discovery (Port 5100)  │  ← Service Registry, Health Checks
└────────┬───────────────┘
         │
         ▼
    ┌────────┐
    │ Consul │  ← Service Discovery
    └────────┘
```

## 🚀 Quick Start

### 1. Start Infrastructure

```bash
# Start Consul, RabbitMQ, PostgreSQL, Redis
docker-compose up -d

# Verify all services are healthy
docker-compose ps
```

**Infrastructure URLs:**
- Consul UI: http://localhost:8500
- RabbitMQ Management: http://localhost:15672 (guest/guest)

### 2. Build the Solution

```bash
# Restore dependencies and build
dotnet restore
dotnet build
```

### 3. Run Discovery Service

```bash
# Terminal 1: Start Discovery Service
cd src/Core/ToskaMesh.Discovery
dotnet run

# Service will start on: http://localhost:5100
# Swagger UI: http://localhost:5100/swagger
```

### 4. Run Gateway Service

```bash
# Terminal 2: Start Gateway Service
cd src/Core/ToskaMesh.Gateway
dotnet run

# Service will start on: http://localhost:5000
# Swagger UI: http://localhost:5000/swagger
```

## 🧪 Test the Services

### Test 1: Discovery Service Health Check

```bash
curl http://localhost:5100/health
```

**Expected Response:**
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.001234"
}
```

### Test 2: Register a Service with Discovery

```bash
curl -X POST http://localhost:5100/api/ServiceRegistration/register \
  -H "Content-Type: application/json" \
  -d '{
    "serviceName": "test-service",
    "serviceId": "test-service-1",
    "address": "localhost",
    "port": 8080,
    "healthCheck": {
      "endpoint": "/health",
      "interval": "00:00:10",
      "timeout": "00:00:05"
    },
    "metadata": {
      "version": "1.0.0",
      "environment": "dev"
    }
  }'
```

**Expected Response:**
```json
{
  "success": true,
  "data": {
    "serviceName": "test-service",
    "serviceId": "test-service-1",
    "address": "localhost",
    "port": 8080,
    "status": "Healthy"
  },
  "message": "Service registered successfully"
}
```

### Test 3: Discover Services

```bash
curl http://localhost:5100/api/ServiceDiscovery/services
```

**Expected Response:**
```json
{
  "success": true,
  "data": ["consul", "test-service"]
}
```

### Test 4: Get Service Instances

```bash
curl http://localhost:5100/api/ServiceDiscovery/services/test-service/instances
```

### Test 5: Gateway Health Check

```bash
curl http://localhost:5000/health
```

### Test 6: Gateway Routing (after registering a service)

The Gateway will automatically create routes for registered services:

```bash
# Gateway routes to: http://localhost:5100
curl http://localhost:5000/api/discovery-service/health
```

### Test 7: Rate Limiting

```bash
# Exceed rate limit (100 requests per minute)
for i in {1..150}; do
  curl -s http://localhost:5000/health > /dev/null
  echo "Request $i"
done
```

**Expected:** After 100 requests, you'll get HTTP 429 (Too Many Requests)

### Test 8: Prometheus Metrics

```bash
curl http://localhost:5000/metrics
curl http://localhost:5100/metrics
```

### Test 9: View Services in Consul UI

1. Open http://localhost:8500
2. Navigate to "Services"
3. You should see registered services

## 🔍 Key Features Demonstrated

### 1. Dynamic Service Discovery ✅

**How it works:**
- Services register with Discovery Service
- Discovery Service registers them in Consul
- Gateway polls Consul every 30 seconds
- Routes are created automatically

**Gateway Route Pattern:**
```
/api/{serviceName}/** → http://{service-address}:{port}/**
```

### 2. JWT Authentication ✅

**Configuration:** `src/Core/ToskaMesh.Gateway/appsettings.json`

```json
{
  "Jwt": {
    "SecretKey": "your-secret-key-min-32-chars-long",
    "Issuer": "ToskaMesh.Gateway",
    "Audience": "ToskaMesh.Services"
  }
}
```

**Test with JWT:**
```bash
# Generate a token (you'll need to implement Auth Service first)
# For now, JWT validation is configured but not enforced on all routes

# You can test by adding to Program.cs:
# .RequireAuthorization() on specific routes
```

### 3. Rate Limiting ✅

**Configuration:** `src/Core/ToskaMesh.Gateway/appsettings.json`

```json
{
  "RateLimit": {
    "EnableRateLimiting": true,
    "PermitLimit": 100,
    "WindowSeconds": 60,
    "QueueLimit": 10
  }
}
```

**Algorithm:** Fixed Window (IP-based)
- Each IP gets 100 requests per 60-second window
- Additional 10 requests can queue
- Returns HTTP 429 when exceeded

### 4. Request Logging ✅

Every request is logged with:
- Correlation ID (TraceIdentifier)
- Client IP address
- Request method and path
- Response status code
- Request duration in milliseconds

**Example Log Output:**
```
[INF] Incoming request: GET /health | Correlation-ID: 0HN4... | Client-IP: ::1
[INF] Outgoing response: GET /health | Status: 200 | Duration: 5ms | Correlation-ID: 0HN4...
```

### 5. Health Checks ✅

Three endpoints available:
- `/health` - Overall health
- `/health/ready` - Readiness probe (for Kubernetes)
- `/health/live` - Liveness probe (for Kubernetes)

### 6. Observability ✅

**OpenTelemetry Metrics:**
- Exposed at `/metrics` (Prometheus format)
- Includes HTTP request metrics
- Custom business metrics available

**Structured Logging:**
- Serilog with console and file sinks
- JSON-formatted logs
- Request correlation

## 📁 Code Walkthrough

### Gateway Service Key Files

```
src/Core/ToskaMesh.Gateway/
├── Services/ConsulProxyConfigProvider.cs     # Dynamic routing from Consul
├── Middleware/RequestLoggingMiddleware.cs    # Request/response logging
├── Configuration/JwtConfiguration.cs         # JWT settings model
├── Configuration/RateLimitConfiguration.cs   # Rate limit settings
└── Program.cs                                # Application setup
```

**ConsulProxyConfigProvider.cs (Lines: 145)**
- Polls Consul every 30 seconds
- Creates YARP routes dynamically
- Filters healthy instances only
- Round-robin load balancing

**RequestLoggingMiddleware.cs (Lines: 60)**
- Logs incoming requests
- Tracks request duration
- Adds correlation IDs
- Logs outgoing responses

### Discovery Service Key Files

```
src/Core/ToskaMesh.Discovery/
├── Controllers/
│   ├── ServiceRegistrationController.cs      # Register/deregister endpoints
│   └── ServiceDiscoveryController.cs         # Query service instances
├── Services/
│   ├── ServiceManager.cs                     # Business logic
│   ├── IServiceManager.cs                    # Interface
│   └── ServiceDiscoveryBackgroundService.cs  # Background health checks
└── Program.cs                                # Application setup
```

**ServiceManager.cs (Lines: 160)**
- Manages service registration
- Publishes events to RabbitMQ
- Performs health checks
- Integrates with Consul

### Shared Libraries

**ToskaMesh.Common (Lines: 850)**
- Service discovery integration
- Message bus integration
- Database context
- Health check extensions
- Validation helpers

**ToskaMesh.Security (Lines: 280)**
- JWT token generation/validation
- Password hashing (BCrypt)
- API key authentication
- Authorization policies

**ToskaMesh.Protocols (Lines: 120)**
- Service registry interface
- Service instance models
- Health status enums

**ToskaMesh.Telemetry (Lines: 150)**
- OpenTelemetry setup
- Prometheus exporter
- Serilog configuration
- Custom metrics

## 🐛 Troubleshooting

### Infrastructure Not Starting

```bash
# Check if ports are already in use
lsof -i :8500  # Consul
lsof -i :5672  # RabbitMQ
lsof -i :5432  # PostgreSQL
lsof -i :6379  # Redis

# View logs
docker-compose logs consul
docker-compose logs rabbitmq
```

### Services Can't Connect to Infrastructure

**Update appsettings.json to use localhost:**

```json
{
  "Consul": {
    "Address": "http://localhost:8500"
  },
  "MassTransit": {
    "RabbitMq": {
      "Host": "localhost"
    }
  }
}
```

### Build Errors

```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

## 🎯 Next Steps

To complete the demo, you could implement:

1. **Auth Service** - User registration, login, JWT token generation
2. **Sample Microservice** - A simple service that registers with Discovery
3. **End-to-End Flow** - Client → Gateway → Auth → Service
4. **Docker Images** - Containerize the services
5. **Kubernetes Manifests** - Deploy to K8s

## 📚 API Documentation

Once services are running:
- Gateway Swagger: http://localhost:5000/swagger
- Discovery Swagger: http://localhost:5100/swagger

## 🔗 Quick Links

- **Consul UI:** http://localhost:8500
- **RabbitMQ Management:** http://localhost:15672
- **Gateway:** http://localhost:5000
- **Discovery:** http://localhost:5100
- **Gateway Metrics:** http://localhost:5000/metrics
- **Discovery Metrics:** http://localhost:5100/metrics

---

**Built with:** .NET 8, YARP, Consul, MassTransit, OpenTelemetry, Serilog
**Lines of Code:** ~3,000 LOC
**Time to Build:** From scratch in one session ⚡
