# Redis grain storage demo

Minimal stateful service that persists Orleans grain state to ToskaRedis (Redis) and exposes a tiny HTTP front-end.

## What it shows
- `[PersistentState]` using the mesh Redis grain storage provider (`RedisStorageConnectionString`).
- Local clustering (no Consul) for easy testing.
- Front-end (`MeshServiceHost`) calls grains; grain state persists in Redis without TTL/eviction.

## Run locally

1. Start Redis (durable + no eviction):
   ```bash
   docker run --rm -p 6379:6379 \
     redis:7-alpine \
     redis-server --appendonly yes --appendfsync everysec --save "" --maxmemory-policy noeviction
   ```
2. Start the silo (uses Redis storage):
   ```bash
   dotnet run --project examples/redis-grain-storage-demo/silo/RedisGrainSilo.csproj
   ```
3. Start the API front-end:
   ```bash
   dotnet run --project examples/redis-grain-storage-demo/api/RedisGrainApi.csproj
   ```
4. Exercise the counter (persists to Redis):
   ```bash
   curl -s http://localhost:8080/counter          # {"value":0}
   curl -s -XPOST http://localhost:8080/counter/increment -H "Content-Type: application/json" -d '{"delta":5}'
   curl -s http://localhost:8080/counter          # {"value":5}
   curl -s -XPOST http://localhost:8080/counter/reset -i # 204
   ```

## Configuration
- Redis storage: `Redis:ConnectionString` (default `localhost:6379`), `Redis:Database` (optional), `Redis:KeyPrefix` (defaults to `redis-grain-demo:grain:`).
- Orleans cluster: `Orleans:ClusterId` (default `redis-grain-demo`), `Orleans:ServiceId` (default `redis-grain-silo`), `Orleans:GatewayPort` (default `30000`), `Orleans:SiloPort` (default `11111`).
- Both API and silo set `RegisterAutomatically = false` and `AllowNoopServiceRegistry = true` to avoid external service registry dependencies.

## Code
- Grain interface: `examples/redis-grain-storage-demo/contracts/CounterGrainInterfaces.cs`
- Grain implementation + state: `examples/redis-grain-storage-demo/silo/Grains/CounterGrain.cs`
- Silo host (Redis-backed storage): `examples/redis-grain-storage-demo/silo/Program.cs`
- API front-end (calls grains): `examples/redis-grain-storage-demo/api/Program.cs`
