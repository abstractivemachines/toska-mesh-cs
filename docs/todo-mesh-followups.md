## Todo Mesh stateful rollout follow-ups (Ready)

### Next steps
- Verify todo-mesh API ↔ silo connectivity after restart (Consul membership shows `10.244.0.165:30000`, API readiness is green).
- Clean up the stray `default/todo-mesh-api-67495fcb7d-m252v` pod (CreateContainerConfigError).
- Fix `kubernetes-dashboard-kong` CrashLoop (one replica still failing).
- Keep Consul address overrides: API uses `consul-server.toskamesh-infra.svc.cluster.local:8500`; silo uses advertised podIP + gateway port 30000.
- Images deployed in `toskamesh`: `todo-mesh-silo` digest `sha256:f591449f5168c5187f482a4672cd43a302f897fe12463dd225c7988c61effbe6`; `todo-mesh-api` digest `sha256:b1aa46f727a8d5a56cc22f5f2c0be0ae5f1577b7dfc06ff53cde68697131aadb`.

### Comment
- Silo and API rebuilt against source, added AdvertisedIPAddress piping from podIP; Consul KV now shows 10.244.0.165:30000 gateway, API ready.
- K8s overlays updated: silo ConfigMap uses consul-server.toskamesh-infra:8500, advertised IP from status.podIP; API already pointed to that Consul address. Probes relaxed.
- Deployments applied; current state: todo-mesh-silo 1/1 Running (10.244.0.165), todo-mesh-api 1/1 Running (10.244.0.166), core mesh services healthy.
- Remaining issues: one old todo-mesh-api pod in default namespace with CreateContainerConfigError (delete), kubernetes-dashboard-kong pod CrashLoopBackOff (1 replica).
