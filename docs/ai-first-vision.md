# Using ToskaMesh to Enable Rapid AI-First Products

## The Core Thesis

ToskaMesh already solves the hard distributed systems problems — discovery, routing, health, resilience, messaging, observability, stateful coordination. AI-first products are fundamentally **multi-service orchestration problems**: they compose LLM calls, retrieval, tool use, memory, and domain logic into coherent workflows. The mesh is the missing infrastructure layer that lets you build these systems as composable, observable, production-grade services instead of monolithic scripts.

---

## 1. AI Model Gateway — The Obvious First Move

The Gateway already does dynamic YARP routing with health-driven instance filtering. Extending it to be an **AI model gateway** is natural:

**What this looks like:**
- Services register as model providers with metadata: `model_name`, `provider` (openai, anthropic, local-vllm), `capabilities` (chat, embeddings, vision, function-calling), `cost_per_1k_tokens`, `max_context_window`, `supports_streaming`
- The Gateway routes `/api/ai/chat` to the right backend based on request properties — model preference, context length, budget constraints
- Load balancing strategies extend beyond RoundRobin: add **CostOptimized** (cheapest healthy provider), **LatencyOptimized** (fastest p50), **QualityTiered** (try Claude, fallback to Haiku if rate-limited)
- Circuit breakers already handle provider outages; a rate-limited OpenAI endpoint trips the breaker, traffic shifts to Anthropic automatically

**Why this matters for rapid products:** Every AI product needs provider abstraction, failover, and cost management. Building it once in the mesh means every service gets it for free. New team members scaffold a service with `toska init my-ai-service` and immediately have resilient multi-provider AI access.

---

## 2. Agent Orchestration via MassTransit

This is where ToskaMesh's messaging layer becomes powerful. AI agents are inherently **event-driven, multi-step workflows** — exactly what MassTransit's pub/sub and RPC patterns handle.

**The pattern:**
```
User Request → OrchestratorService (saga/state machine)
  → publishes ReasoningRequestEvent
  → LLM Service processes, publishes ReasoningCompleteEvent with tool_calls
  → Orchestrator evaluates tool_calls
  → publishes ToolExecutionCommand to appropriate service
  → ToolService executes, publishes ToolResultEvent
  → Orchestrator feeds results back to LLM
  → Loop until done, publish FinalResponseEvent
```

MassTransit **sagas** map perfectly to agentic loops. Each agent run is a saga instance with state (conversation history, tool results, retry count, token budget). The saga coordinates between:
- **LLM services** (stateless, wrapping provider APIs)
- **Tool services** (stateless, executing search/code/API calls)
- **Memory services** (stateful Orleans grains, holding conversation and retrieval context)
- **Guardrail services** (stateless, checking content policy, PII, cost limits)

**Why this matters:** You get distributed agent execution with built-in retry, timeout, compensation (saga rollback), and observability — for free. No custom orchestration framework needed. Each concern is a separate mesh service that scales independently.

---

## 3. Stateful Conversation & Memory via Orleans Grains

The `MeshStatefulLambdaService` + Orleans integration is underappreciated for AI use cases. Grains are **virtual actors** — perfect for:

- **ConversationGrain** — Holds chat history, manages context window (truncation/summarization), persists to Redis. Grain ID = session ID. Survives restarts. Multiple services can interact with the same conversation state.
- **UserProfileGrain** — Accumulates user preferences, interaction patterns, personalization context across sessions. Feeds into prompt construction.
- **AgentMemoryGrain** — Long-term agent memory. Stores facts, tool results, learned preferences. Implements reflection/consolidation on a timer.
- **RateLimitGrain** — Per-user, per-model token budget tracking. Grain activation is lazy, so millions of users cost nothing until active.

**Why this matters:** AI products live or die on state management. "Remember what I said earlier" is table stakes. Orleans grains give you distributed, persistent, actor-model state with zero boilerplate — and they're already wired into the mesh's discovery and health systems.

---

## 4. Retrieval-Augmented Generation as Composable Services

RAG is not one thing — it's a pipeline: **query → embed → retrieve → rerank → augment → generate → cite**. Each stage is a mesh service:

| Service | Type | Role |
|---------|------|------|
| `embedding-service` | Stateless Lambda | Wraps embedding model (local or API) |
| `vector-store-service` | Stateful (Redis/Pgvector) | Manages document indexes |
| `retrieval-service` | Stateless Lambda | Orchestrates search + reranking |
| `ingestion-service` | Stateless Lambda | Chunks, embeds, stores documents |
| `generation-service` | Stateless Lambda | Augments prompt with context, calls LLM |

These communicate via MassTransit events:
- `DocumentIngestedEvent` triggers embedding + indexing
- `RetrievalRequestCommand` / `RetrievalResultEvent` for synchronous RAG
- `IndexRebuiltEvent` for cache invalidation

**Why this matters:** Monolithic RAG is brittle. When you need to swap Pinecone for pgvector, or add a reranker, or change chunking strategy — you change one service. The mesh handles routing, health, and discovery. You can A/B test retrieval strategies by deploying two versions and using weighted routing.

---

## 5. AI-Specific Observability

The ObservabilityService already tracks topology, SLOs, and burn rates. Extending it for AI:

- **Token usage metrics** per service, per model, per user — feed into Prometheus, alert on budget burn rate
- **Cost tracking** — aggregate provider costs across all services, SLO on cost-per-request
- **Latency decomposition** — break down an agent workflow: 200ms retrieval + 1.2s LLM + 50ms tool execution = 1.45s total (the topology graph already shows service-to-service dependencies)
- **Quality metrics** — log prompt/response pairs, track user feedback, compute quality signals
- **Prompt versioning** — ConfigService already manages centralized YAML config; extend it to store prompt templates with versioning, A/B assignment

The observability topology graph becomes an **agent workflow visualizer** — you can see exactly which services an agent invoked, in what order, with what latency and cost.

---

## 6. The "10-Minute AI Service" Developer Experience

This is the strategic play. Today:

```bash
toska init my-rag-service --type ai-rag
```

Scaffolds a service with:
- Vector store connection (pgvector or Redis)
- Embedding endpoint
- Retrieval + generation pipeline
- Pre-configured prompts in `config/prompts.yaml`
- Health checks that verify model endpoint availability
- Token counting middleware
- Cost tracking telemetry

```csharp
await MeshLambdaService.RunAsync(
    app =>
    {
        app.MapPost("/ask", async (AskRequest req, IAiPipeline pipeline) =>
            await pipeline.RetrieveAndGenerateAsync(req.Question));

        app.MapPost("/ingest", async (IngestRequest req, IIngestionService ingestion) =>
            await ingestion.IngestDocumentAsync(req.Document));
    },
    options =>
    {
        options.ServiceName = "my-rag-service";
        options.Metadata["capabilities"] = "rag,embeddings";
        options.Metadata["models"] = "claude-sonnet-4-5,text-embedding-3-small";
    },
    services =>
    {
        services.AddAiPipeline(config);  // new extension
        services.AddGrpcServiceRegistry(config);
    }
);
```

**That's a production-grade, observable, resilient RAG service in ~20 lines.** It registers with Discovery, gets health-monitored, appears in the topology graph, has circuit breakers on the LLM provider, tracks token costs, and is accessible through the Gateway.

---

## 7. Concrete Product Patterns This Enables

| Product | Mesh Services | Key Pattern |
|---------|--------------|-------------|
| **Customer support agent** | LLM, RAG (knowledge base), Tool executor (CRM/ticketing API), ConversationGrain | MassTransit saga orchestrates multi-turn agent loop |
| **Document Q&A platform** | Ingestion, Embedding, Vector store, Generation | Event-driven pipeline, weighted routing for model A/B testing |
| **Code review bot** | LLM, Git integration tool, Static analysis tool | Gateway authenticates GitHub webhooks, fan-out to analysis services |
| **Multi-tenant AI platform** | All of the above + AuthService per-tenant isolation | Orleans grains for tenant state, Gateway rate limiting per tenant |
| **Real-time data analyst** | LLM, SQL tool executor, Visualization service | Streaming responses via SSE through Gateway |

---

## 8. What Would Need to Be Built

Roughly in priority order:

1. **`ToskaMesh.Runtime.Ai`** — Shared library with `IAiPipeline`, `IModelRouter`, token counting middleware, streaming response helpers, prompt template engine. Follows the same pattern as `ToskaMesh.Runtime` but AI-specific.

2. **Model routing in the Gateway** — Extend `ILoadBalancer` with `ModelAware` strategy that reads request body (model preference, context length) and routes to the right backend. Add fallback chain logic.

3. **AI-specific messaging contracts** — `InferenceRequestCommand`, `InferenceCompleteEvent`, `TokenUsageEvent`, `ToolCallRequestCommand`. These become the lingua franca for agent orchestration.

4. **Streaming support** — SSE/WebSocket forwarding through the Gateway for streaming LLM responses. YARP supports this but it needs to be wired up with the health and observability layers.

5. **Prompt management in ConfigService** — Versioned prompt templates, A/B assignment, rollback. Natural extension of the existing YAML config system.

6. **`toska init --type ai-*` templates** — CLI scaffolding for common AI service patterns (rag, agent, tool, embedding).

7. **Observability extensions** — Token usage dashboards, cost burn-rate alerts, prompt/response logging with sampling.

---

## The Strategic Insight

Most AI frameworks (LangChain, Semantic Kernel, etc.) solve the **single-process orchestration** problem. They're good at chaining LLM calls within one application. But production AI systems are **distributed systems** — they need service isolation, independent scaling, resilience, observability, and multi-team development.

ToskaMesh is positioned at a different layer: it's not competing with LangChain, it's the **infrastructure that LangChain services run on**. A team could use Semantic Kernel inside one mesh service and LangChain inside another — the mesh doesn't care. It provides the connective tissue: discovery, routing, messaging, health, and observability.

The rapid product development story is: **define your AI capability as a mesh service, compose services via messaging, observe the whole system through a single pane of glass, and ship with production resilience from day one.** The mesh takes care of the distributed systems problems so teams can focus on the AI logic.
