# ToskaMesh.Runtime.Orleans

Orleans provider for ToskaMesh stateful hosting. Supplies the silo hosting and clustering used by `ToskaMesh.Runtime.Stateful` (default provider). Not typically referenced directly by consumers; include the provider-agnostic `ToskaMesh.Runtime.Stateful` package and configure clustering/discovery/auth via `Mesh:Stateful` and `Mesh:Service*` settings.
