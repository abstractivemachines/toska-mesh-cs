FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/Shared ./src/Shared
COPY examples/mesh-rpc-demo ./examples/mesh-rpc-demo

WORKDIR /src/examples/mesh-rpc-demo/service-c
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8083
ENTRYPOINT ["dotnet", "MeshRpcServiceC.dll"]
