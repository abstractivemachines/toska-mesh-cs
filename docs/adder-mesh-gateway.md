# Adder Mesh Service through Toska Gateway

```mermaid
flowchart LR
    subgraph AdderPod["Adder Mesh Service Pod"]
        A["MeshServiceHost<br/>ASP.NET Core<br/>binds to 0.0.0.0:8083"] -->|uses| B["MeshServiceOptions<br/>Address=0.0.0.0<br/>AdvertisedAddress=PodIP"]
        B -->|registers| C["Discovery Registry<br/>entry Address=PodIP, Port=8083"]
    end

    subgraph Discovery["Discovery Service"]
        C --> D["Consul catalog<br/>adder-mesh-service"]
    end

    subgraph Gateway["Toska Gateway"]
        D --> E["YARP route<br/>/api/adder-mesh-service/**<br/>-> PodIP:8083"]
        E --> F["NodePort 30080<br/>(LoadBalancer if present)"]
    end

    Laptop["Laptop curl<br/>http://&lt;nodeIP&gt;:30080/api/adder-mesh-service/add"] --> F
```

## Notes on advertised vs. bind address

- Services bind to `0.0.0.0` inside the pod so port-forwarding, probes, and in-pod traffic work.
- The discovery service now rewrites non-routable registrations (`0.0.0.0`, loopback, or empty) to the caller's IP, so apps don't need to know their advertised address.
- You no longer need to set `Mesh__Service__AdvertisedAddress` (or `Mesh__Service__Address`) in the adder deployment; discovery normalizes it during registration and uses the caller IP as the advertised address. The gateway picks up that value when building routes.
