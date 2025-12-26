FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Local packages produced from this repo (run dotnet pack before building the image).
COPY ./artifacts/nuget ./artifacts/nuget
COPY Directory.Build.props Directory.Packages.props ./
COPY ./examples/profile-kv-store-demo ./examples/profile-kv-store-demo

WORKDIR /src/examples/profile-kv-store-demo
RUN dotnet restore --configfile NuGet.config
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8084
ENTRYPOINT ["dotnet", "ProfileKvStoreDemo.dll"]
