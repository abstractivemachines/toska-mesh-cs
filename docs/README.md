# Documentation Map

- **Start here**
  - [Getting started](GETTING_STARTED.md) – single-page local demo/setup.
  - [MeshServiceHost quickstart](meshservicehost-quickstart.md) – runtime surface with stateless/stateful samples.
  - [Examples index](../examples/README.md) – links to runnable samples; see per-example guides for `hello`, `adder`, `todo`, and `redis-grain-storage`.
  - Cluster bootstraps: [Talos quickstart](../deployments/QUICKSTART-TALOS.md), [EKS quickstart](../deployments/QUICKSTART-EKS.md).
  - Demo script: [DEMO.md](../DEMO.md) for the abbreviated local walkthrough.

- **Architecture & Runtime**
  - [MeshServiceHost diagram](meshservicehost-diagram.md) and [runtime SDK design](runtime-sdk-design.md).
  - [Orleans stateful grain example](orleans-stateful-grain-example.md) and [queue-to-grain dispatch](queue-to-grain-dispatch.md).
  - [Evented communication](evented-communication.md) patterns; [runtime packaging](runtime-packaging.md) layout.
  - [Toska manifest reference](toska-manifest.md) for `toska.yaml`.
  - [ToskaStore integration](toskastore.md) for mesh key/value usage (see the
    [ToskaStore README](https://github.com/abstractivemachines/toska_store/blob/main/README.md)).

- **Operations & Security**
  - [Monitoring setup](monitoring-setup.md) for metrics, tracing, and alerting.
  - [mTLS enablement](mtls-enable.md) plus rollout follow-ups/backlog in [mtls-rollout-todo.md](mtls-rollout-todo.md).
  - [Cost optimization](cost-optimization.md) with EKS-specific follow-up in [deployments/terraform/eks/COST-OPTIMIZATION-SUMMARY.md](../deployments/terraform/eks/COST-OPTIMIZATION-SUMMARY.md).
  - Gateway/discovery routing notes: [adder-mesh-gateway](adder-mesh-gateway.md).

- **Deployments & Infra**
  - [Kubernetes deployment](kubernetes-deployment.md) overlays and [EKS deployment guide](eks-deployment-guide.md).
  - Terraform: [deployments/terraform/eks/README.md](../deployments/terraform/eks/README.md).

- **Examples & SDK packaging**
  - Per-sample guides: [hello-mesh-service](../examples/hello-mesh-service/README.md), [adder-mesh-service](../examples/adder-mesh-service/README.md), [todo-mesh-service](../examples/todo-mesh-service/README.md), [redis-grain-storage-demo](../examples/redis-grain-storage-demo/README.md).
  - NuGet readmes: [ToskaMesh.Runtime](../src/Shared/ToskaMesh.Runtime/PackageReadme.md), [Stateful](../src/Shared/ToskaMesh.Runtime.Stateful/PackageReadme.md), [Orleans](../src/Shared/ToskaMesh.Runtime.Orleans/PackageReadme.md).

- **Plans, reviews, and process**
  - [Implementation plan](IMPLEMENTATION_PLAN.md); planning stubs in [plans/stateful-runtime-namespace-plan.md](plans/stateful-runtime-namespace-plan.md) and [plans/example-consumer-on-mesh.md](plans/example-consumer-on-mesh.md).
  - [Code review guide](CODE_REVIEW.md); contributor notes in [AGENTS.md](../AGENTS.md).

- **Decisions & change history**
  - ADRs: [adr/README.md](adr/README.md) (001 Orleans clustering, 002 Polly resilience, 003 Consul discovery, 004 YARP gateway).
  - Changelog: [CHANGELOG.md](CHANGELOG.md) links to detailed change notes in `changes/`.

- **Tooling**
  - CLI helper: [tools/cli/README.md](../tools/cli/README.md).
