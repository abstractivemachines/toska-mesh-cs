# Runtime Packaging (NuGet)

## Projects
- `src/Shared/ToskaMesh.Runtime` → `ToskaMesh.Runtime` (stateless host)
- `src/Shared/ToskaMesh.Runtime.Orleans` → `ToskaMesh.Runtime.Orleans` (stateful host)

## Pack locally
```bash
# Stateless host
dotnet pack src/Shared/ToskaMesh.Runtime/ToskaMesh.Runtime.csproj -c Release -o ./artifacts/nuget

# Stateful (Orleans) host
dotnet pack src/Shared/ToskaMesh.Runtime.Orleans/ToskaMesh.Runtime.Orleans.csproj -c Release -o ./artifacts/nuget
```

## Push to a feed
```bash
# NuGet.org (requires NUGET_API_KEY)
dotnet nuget push ./artifacts/nuget/*.nupkg -k $NUGET_API_KEY -s https://api.nuget.org/v3/index.json --skip-duplicate

# GitHub Packages example (replace OWNER/REPO and token)
dotnet nuget push ./artifacts/nuget/*.nupkg \
  -k $GITHUB_TOKEN \
  -s https://nuget.pkg.github.com/abstractivemachines-com/index.json \
  --skip-duplicate
```

## Versioning
- Current placeholder: `0.1.0-preview` (set in the csproj files).
- Update the version before packing for a release; CI can inject `$(PackageVersion)` if desired.

## Notes
- `GeneratePackageOnBuild` is disabled; use `dotnet pack` explicitly.
- Packages include repository metadata/tags; license is inherited from the repo. Update metadata as needed before publishing.
