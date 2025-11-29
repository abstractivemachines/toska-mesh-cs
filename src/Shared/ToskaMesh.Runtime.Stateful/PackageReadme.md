# ToskaMesh.Runtime.Stateful

Provider-agnostic stateful host for Toska Mesh services. Exposes `StatefulMeshHost` + `StatefulHostOptions` while deferring to pluggable providers (Orleans by default via the `ToskaMesh.Runtime.Orleans` dependency). Configure cluster/discovery/auth via `Mesh:Stateful` and `Mesh:Service*` settings; consumers reference this package, not provider internals.
