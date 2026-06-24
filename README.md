# Xeno v0.1.0 ALPHA

![Xeno logo](assets/logo.png)

Xeno v0.1.0 is an alpha release. APIs, configuration files, dashboard workflows, Docker deployment details, and client commands are subject to change before a stable release.

Xeno is an AI control plane for routing OpenAI-compatible completions and embeddings to distributed computers running the `xeno` client.

The first implementation includes:

- multitenant server state with seeded tenant, user, credential, connected clients, and advertised models;
- OpenAI-compatible routes at `/v1/chat/completions`, `/v1/completions`, `/v1/embeddings`, and `/v1/models`;
- management routes for health, OpenAPI, clients, advertised models, tenants, users, credentials, roles, permissions, settings, request history, and token bootstrap;
- a WebSocket registration endpoint at `/ws/v1/clients` gated by tenant-bound client access-key/secret credentials;
- a cross-platform `xeno` console tool with `~/.xeno/client.json` configuration;
- a React/Vite dashboard under `dashboard/` with create/edit workflows for tenants, users, credentials, roles, and permissions plus settings, clients, advertised models, analytics, request history, and integrated API explorer views;
- Touchstone shared tests with automated, xUnit, and NUnit runners.

## Quick Start

```powershell
dotnet build src/Xeno.sln
dotnet run --project src/Test.Automated -- --results results.json
dotnet run --project src/Xeno.Server -- xeno.json
```

Then call:

```bash
curl http://localhost:9000/health
curl http://localhost:9000/v1/models
```

The OpenAPI contract is available at `/openapi.json` and Swagger UI is available at `/swagger`. No generated SDK packages are checked into v0.1.0; use `/openapi.json` as the SDK generation source of truth.

## Client

See [CLIENT_GETTING_STARTED.md](CLIENT_GETTING_STARTED.md) for the full client setup flow.

On Windows, `install-tool.bat` rebuilds the client and installs `xeno` as a .NET global tool. `remove-tool.bat` uninstalls it.

```powershell
dotnet run --project src/Xeno.Client -- init
dotnet run --project src/Xeno.Client -- login -u admin@xeno
dotnet run --project src/Xeno.Client -- status
dotnet run --project src/Xeno.Client -- inventory
```

The seeded development user is `admin@xeno` with password `password`. The seeded development credential uses access key `default` and secret key `default`. `xeno login -u ...` prompts for the password, exchanges it for a client access key and secret, and stores only that client credential in `~/.xeno/client.json`. The WebSocket connection does not accept user passwords or bearer session tokens.

## Docker

```powershell
docker login
build-server.bat v0.1.0
build-dashboard.bat v0.1.0
build-all.bat v0.1.0
docker compose -f docker/compose.yaml up
```

The build scripts build and push both the requested tag and `latest` to Docker Hub. `docker compose pull` requires those tags to exist in Docker Hub, so run `docker login` with access to `jchristn77` before invoking the build scripts.

The default compose stack uses PostgreSQL. `xeno-postgres-init` runs before `xeno-server` and creates the `xeno` role/database expected by `docker/xeno.json`. MySQL and SQL Server remain optional compose profiles.

Images:

- `jchristn77/xeno-server:v0.1.0`
- `jchristn77/xeno-dashboard:v0.1.0`

## Requirements

Implementation follows the repository, backend, frontend, authentication, i18n, and testing guidance in `C:\code\agents\requirements`. The implementation plan remains in `XENO_PLAN.md`.
