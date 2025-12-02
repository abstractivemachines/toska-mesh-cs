# mTLS rollout follow-ups

1) Get discovery healthy
- Investigate why discovery pods refuse connections on :80/:50051 (readiness 503). Check longer logs, describe pods, and confirm Kestrel binding/entrypoint matches env (`ASPNETCORE_URLS`, `Kestrel__Endpoints__Http__Url`, `Kestrel__Endpoints__Grpc__Url`).
- Verify service selectors vs pod labels (Helm uses `app.kubernetes.io/*`). Ensure endpoints exist and kube-probes are correct.
- From a curl pod, retest `http://toskamesh-discovery.toskamesh.svc.cluster.local:80/health/ready` and `http://toskamesh-discovery.toskamesh.svc.cluster.local:50051` once fixed.
- Discovery fixed: rabbit host now points at `rabbitmq.toskamesh-infra.svc.cluster.local`, gRPC served over TLS on 50051 with SAN wildcard service host (`toskamesh-internal.toskamesh.svc.cluster.local`); added companion `toskamesh-internal` service for that hostname. Clients use HTTPS and AllowInsecureTransport=false (name now matches cert), client cert optional.

2) Stabilize gateway rollout
- New gateway pod CrashLooped; old pod still serving. After discovery is healthy, restart gateway and confirm liveness/readiness on :80 and HTTPS/mTLS on :8443 with mounted TLS secret.
- Confirm outbound client cert loads from `toskamesh-gateway-tls` and `SSL_CERT_FILE` is honored.
- Gateway fixed: health endpoints stay on HTTP (bypass HTTPS redirect for /health) and image `192.168.50.73:5000/toskamesh-gateway:local-20251202` rolled out. Probes now 200.

3) Fix example services blocked on discovery
- `adder-mesh-service` CrashLooping with gRPC connection refused. Retest once discovery is reachable on the expected port (likely :50051). Update env if discovery serves gRPC on 80 instead.
- Adder pod running after discovery+gateway fixes (was failing on gRPC to :50051 while discovery served HTTP/1.1 on 80).

4) Align manifests with Helm selectors
- Manifests in repo use simple `app` selectors; live deployments use `app.kubernetes.io/*` (immutable). Update manifests to match Helm selectors to avoid apply errors, or switch to `kubectl patch`/Helm for future changes.
- Updated k8s deployments/services to `app.kubernetes.io` selectors (Helm release/name `toskamesh`; example apps use their own name/instance) so `kubectl apply` no longer conflicts with immutable selectors.

5) Secrets and certs
- Current secrets: `toskamesh-gateway-tls`, `toskamesh-mtls`, `toskamesh-discovery-secrets` (rotated service-auth). Cert password: `meshPfxPass!2024`. Rotate/store securely before production.
