# Stateful Todo Mesh Debug Notes

Context (Talos deployment):
- Silo (`todo-mesh-silo`) runs via `StatefulMeshHost` with Consul clustering and Redis KV.
- API (`todo-mesh-api`) uses `MeshServiceHost` + Orleans client (Consul clustering) to call grains.
- MeshServiceHost registry stub ordering is fixed (noop stub added after `AddMeshService`).

Images pushed:
- `192.168.50.73:5000/todo-mesh-silo:local`
- `192.168.50.73:5000/todo-mesh-api:local`

Current state:
- Silo: `1/1 Running` after switching probes to TCP on port 11111 and setting `ServiceRegistryProvider = Consul`. Registers with Consul as `todo-mesh-silo` at `localhost:8080` (likely not useful). Consul `agent/services` shows only `hello-mesh-service`; silo entry not visible.
- API: crash-looping. Logs only show Orleans client setup then “Application is shutting down…”; no stack trace. Health probes fail because Kestrel never starts. Env aligns with silo: `Orleans__ClusterId=mesh-stateful`, `Orleans__ServiceId=todo-mesh-silo`, discovery gRPC `http://toskamesh-discovery.toskamesh.svc.cluster.local:50051`, mesh auth secret from `toskamesh-discovery-secrets`, HTTP/2 plaintext enabled.
- Discovery service ports: http 80, grpc 50051. Kestrel endpoints set for both HTTP (Http1) and gRPC (Http2). REST discovery works; gRPC not validated with a proper client yet.

Open issues:
1) Orleans client in API likely cannot connect to silo (previously no exception surfaced).
2) Silo registration to Consul is not useful (localhost:8080) and may be unnecessary; consider disabling mesh auto-registration on the silo or setting mesh address/port to pod IP + 11111 if we want it registered.

Suggested next steps:
- Silo: disable mesh auto-registration or set `Mesh__Service__Address=$(status.podIP)` and `Mesh__Service__Port=11111` before registering; keep probes on port 11111.
- API: Orleans client startup now logs connection failures and retries (5s backoff) without killing Kestrel; health exposes `/health/ready` as “unhealthy” until connected. Rebuild/push/redeploy the API image, update readiness probes to `/health/ready` (liveness can stay `/health`), and watch pod logs for Orleans connection errors.
- API k8s overlay added: `k8s/todo-mesh-api` (ConfigMap, Deployment, Service) targets image `192.168.50.73:5000/todo-mesh-api:local` with readiness `/health/ready`. Apply with `kubectl apply -f k8s/todo-mesh-api -n <ns>`.
- Silo k8s overlay added: `k8s/todo-mesh-silo` (ConfigMap, Deployment, Service) targets image `192.168.50.73:5000/todo-mesh-silo:local`, disables mesh auto-registration, and sets probes to TCP 11111. Apply with `kubectl apply -f k8s/todo-mesh-silo -n <ns>`.
- Validate Orleans connectivity: confirm Consul membership for `mesh-stateful`/`todo-mesh-silo` and gateway port 30000 reachability; keep probes lenient enough to surface the logs.
- Validate discovery gRPC with a proper gRPC client (e.g., grpcurl) at `toskamesh-discovery.toskamesh.svc.cluster.local:50051`.

Notes:
- MeshServiceHost ordering fix is in main (commit `Fix registry stub ordering in MeshServiceHost`).
- Probes were changed on the silo to TCP on 11111; API probes still HTTP on /health live/ready.
