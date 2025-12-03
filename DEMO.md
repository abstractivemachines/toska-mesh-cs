# ToskaMesh Demo Guide

Prefer the single-page walkthrough in [docs/GETTING_STARTED.md](docs/GETTING_STARTED.md); the steps below mirror it in abbreviated form.

## Fast local run
```bash
export MESH_SERVICE_AUTH_SECRET="local-dev-mesh-service-secret-32chars"
export MESH_SERVICE_AUTH_ISSUER="ToskaMesh.Services"
export MESH_SERVICE_AUTH_AUDIENCE="ToskaMesh.Services"
cd deployments
docker-compose up -d postgres redis consul prometheus grafana gateway discovery
```
- Health: `curl http://localhost:5000/health` (gateway), `curl http://localhost:5010/health` (discovery).
- Consoles: Consul `http://localhost:8500`, Grafana `http://localhost:3000`, Prometheus `http://localhost:9090`.

## Minimal service checks
- Register a dummy service with discovery to validate registration → discovery API → Consul UI; then hit it via gateway (`/api/{service}/**`) to confirm routing.
- Metrics endpoints: `curl http://localhost:5000/metrics` and `curl http://localhost:5010/metrics`.

## Explore next
- Runtime hosting surfaces and samples: [meshservicehost-quickstart](docs/meshservicehost-quickstart.md) and `examples/`.
- Deployment guides: [kubernetes-deployment](docs/kubernetes-deployment.md) and [eks-deployment-guide](docs/eks-deployment-guide.md).

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
