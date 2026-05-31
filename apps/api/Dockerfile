# Multi-stage build for the SahaHR API (ASP.NET Core 9). Build context = repo root
# (so the .sln and all module projects resolve): `docker build -f apps/api/Dockerfile .`
# Render: set Root Directory = repo root, Dockerfile Path = apps/api/Dockerfile.

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore against the solution (copy only what the build needs).
COPY apps/api/ apps/api/
RUN dotnet restore apps/api/SahaHR.sln
RUN dotnet publish apps/api/src/SahaHR.Api/SahaHR.Api.csproj -c Release -o /app/publish --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Run as the non-root user shipped in the base image.
USER $APP_UID

# Render/Cloud platforms inject $PORT; Program.cs binds 0.0.0.0:$PORT when it is set.
# Default to 8080 for local `docker run` without -e PORT.
ENV PORT=8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "SahaHR.Api.dll"]
