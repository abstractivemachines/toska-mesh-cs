# ToskaMesh.Runtime

Stateless host for Toska Mesh services. Wraps ASP.NET Core with mesh defaults (registration, health, telemetry, auth) via `MeshServiceHost.RunAsync` and the `MeshServiceApp` DSL. Target: .NET 8, C# 12. Use alongside ToskaMesh discovery/gateway; configure service identity and registry in `Mesh:Service*` settings.
