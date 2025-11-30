## Todo Mesh stateful rollout follow-ups (Ready)

### Next steps
- ✅ Verified todo-mesh API ↔ silo end-to-end via port-forward (`POST /todos/{id}` returns 201, `GET` returns 200) after restart.
- ✅ Cleaned up stray `default/todo-mesh-api` deployment (default namespace now empty).
- ✅ Fixed `kubernetes-dashboard-kong` CrashLoop by setting `KONG_PORT_MAPS=8000:8000, 443:8443`; deployment now 1/1 Running.
- Redis connection string now points at `redis-master.toskamesh-infra.svc.cluster.local:6379`; keep Consul overrides: API uses `consul-server.toskamesh-infra.svc.cluster.local:8500`; silo uses advertised podIP + gateway port 30000.
- Images deployed in `toskamesh`: `todo-mesh-silo` digest `sha256:ca760705b8839fb8fdb8634a34fa77eb181918e135d5ca83c480170722aa3a33`; `todo-mesh-api` digest `sha256:58a6d149aba777dfbbcf45c49fb5780b395e1093d700b0fc9d1b439bac5f657b`.

### Comment
- Silo and API rebuilt against source with Orleans codegen enabled for contracts (`Microsoft.Orleans.Sdk` + `[GenerateSerializer]` on `TodoState`); Consul membership shows `todo-mesh-silo` at pod IP/gateway 30000, API ready.
- K8s overlays updated: silo ConfigMap uses `consul-server.toskamesh-infra:8500`, advertised IP from status.podIP, Redis at `redis-master.toskamesh-infra:6379`; API already pointed to that Consul address. Probes remain relaxed.
- Deployments applied; current state: todo-mesh-silo 1/1 Running (10.244.0.172), todo-mesh-api 1/1 Running (10.244.0.173), core mesh services healthy.
- Remaining issues: none observed after clearing default namespace stray pod and fixing kubernetes-dashboard-kong CrashLoop.
