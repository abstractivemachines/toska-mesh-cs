# ToskaMesh.Runtime

Stateless host for ToskaMesh services. Wraps ASP.NET Core with mesh defaults (registration, health, telemetry, auth) via `MeshLambdaService.RunAsync` and the `MeshServiceApp` DSL. Target: .NET 10, C# 14. Use alongside ToskaMesh discovery/gateway; configure service identity and registry in `Mesh:Service*` settings.
