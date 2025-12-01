# Gateway mTLS enablement

This outlines how to turn on mutual TLS for the gateway (clients must present certs to the gateway, and the gateway presents a client cert to mesh services).

## Generate certificates
1. Create a mesh CA:
   ```bash
   openssl genrsa -out ca.key 4096
   openssl req -x509 -new -nodes -key ca.key -sha256 -days 365 -out ca.crt -subj "/CN=toskamesh-ca"
   ```
2. Issue a gateway server cert (include service DNS + localhost for dev):
   ```bash
   cat > gateway.cnf <<'EOF'
   [ req ]
   distinguished_name = dn
   req_extensions = v3_req
   prompt = no
   [ dn ]
   CN = toskamesh-gateway
   [ v3_req ]
   keyUsage = critical, digitalSignature, keyEncipherment
   extendedKeyUsage = serverAuth
   subjectAltName = @alt_names
   [ alt_names ]
   DNS.1 = toskamesh-gateway
   DNS.2 = toskamesh-gateway.toskamesh.svc.cluster.local
   DNS.3 = localhost
   EOF

   openssl genrsa -out gateway-server.key 4096
   openssl req -new -key gateway-server.key -out gateway-server.csr -config gateway.cnf
   openssl x509 -req -in gateway-server.csr -CA ca.crt -CAkey ca.key -CAcreateserial \
     -out gateway-server.crt -days 365 -sha256 -extensions v3_req -extfile gateway.cnf
   openssl pkcs12 -export -out gateway-server.pfx -inkey gateway-server.key -in gateway-server.crt -certfile ca.crt -passout pass:changeit
   ```
3. Issue a client cert for the gateway to call services:
   ```bash
   openssl genrsa -out gateway-client.key 4096
   openssl req -new -key gateway-client.key -out gateway-client.csr -subj "/CN=toskamesh-gateway-client"
   openssl x509 -req -in gateway-client.csr -CA ca.crt -CAkey ca.key -CAcreateserial \
     -out gateway-client.crt -days 365 -sha256
   openssl pkcs12 -export -out gateway-client.pfx -inkey gateway-client.key -in gateway-client.crt -certfile ca.crt -passout pass:changeit
   ```
4. Distribute `ca.crt` to every service trust store (Kubernetes ConfigMap or base image trust bundle) so they trust the gateway client cert and gateway server cert.

## Gateway configuration knobs
- Inbound mTLS (Kestrel): set `ASPNETCORE_Kestrel__Endpoints__Https__Url` (e.g., `https://+:8443`), `ASPNETCORE_Kestrel__Endpoints__Https__ClientCertificateMode=RequireCertificate`, and point `ASPNETCORE_Kestrel__Certificates__Default__Path`/`Password` at the server PFX. Leave these unset to stay on HTTP.
- Outbound mTLS: populate `Mesh:Gateway:Tls` (also available via env) with the client PFX path/password. If either is empty or the file is missing, the gateway skips client cert loading.
- Resilience: `Mesh:Gateway:Resilience` controls retry/jitter and the circuit breaker. Defaults are 3 retries with jittered backoff and a 50% failure ratio over 30s to trip the breaker for 20s.

## Kubernetes wiring
- `k8s/gateway/deployment.yaml` now mounts `/etc/tls` (from `toskamesh-gateway-tls`) and reads optional env vars for Kestrel HTTPS + the outbound client cert password. Without the secret, the pod runs unchanged.
- Create the TLS secret with your artifacts:
  ```bash
  kubectl create secret generic toskamesh-gateway-tls \
    --from-file=gateway-server.pfx=./gateway-server.pfx \
    --from-literal=gateway_server_pfx_password=changeit \
    --from-file=gateway-client.pfx=./gateway-client.pfx \
    --from-literal=gateway_client_pfx_password=changeit \
    --from-file=ca.crt=./ca.crt \
    --from-literal=gateway_server_pfx_path=/etc/tls/gateway-server.pfx \
    --from-literal=kestrel_https_url=https://+:8443
  ```
- Roll the CA bundle into your app images or a shared ConfigMap so downstream services trust the gateway client cert.

## Docker Compose wiring
- Drop `gateway-server.pfx`, `gateway-client.pfx`, and `ca.crt` under `deployments/certs/gateway` (mounted at `/etc/tls`).
- Set `GATEWAY_CLIENT_CERT_PASSWORD` for the outbound cert. Add the same ASP.NET Core env vars above when you want Compose to serve HTTPS with mTLS.

## Upstream services
- Enable client cert validation on each service Kestrel endpoint, point to the CA, and optionally enforce `ClientCertificateMode=RequireCertificate` when receiving gateway calls.
