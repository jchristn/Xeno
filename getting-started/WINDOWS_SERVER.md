# Xeno Server on Windows

This guide deploys the Xeno server and web dashboard with Docker Desktop on Windows.

## Defaults

- Dashboard: `http://localhost:9100`
- Server REST API: `http://localhost:9000`
- Health check: `http://localhost:9000/health`
- Swagger UI: `http://localhost:9000/swagger`
- Client WebSocket: `ws://localhost:9000/ws/v1/clients`
- Dashboard user: `admin@xeno`
- Dashboard password: `password`
- Default client access key: `default`
- Default REST bearer token: `default`

## Prerequisites

1. Install Docker Desktop for Windows.
2. Enable WSL 2 integration in Docker Desktop if prompted.
3. Install Git.
4. Open PowerShell.
5. Clone the repository.

```powershell
git clone https://github.com/jchristn/Xeno.git
cd Xeno
```

## Deploy with Docker

1. Change to the Docker directory.

```powershell
cd docker
```

2. Review the server configuration.

```powershell
notepad .\xeno.json
```

3. Start PostgreSQL, the server, and the dashboard.

```powershell
docker compose pull
docker compose up -d
```

4. Confirm all containers are running or healthy.

```powershell
docker compose ps
```

5. Check the server health endpoint.

```powershell
curl.exe http://localhost:9000/health
```

6. Follow logs if startup fails.

```powershell
docker compose logs -f xeno-server
docker compose logs -f xeno-dashboard
```

## Access the Dashboard

1. Open `http://localhost:9100`.
2. Log in with:

```text
Username: admin@xeno
Password: password
```

3. Start with these dashboard areas:

- `CONTROL > Clients`: confirm connected clients and inspect client ownership.
- `CONTROL > Client Telemetry`: inspect server-collected host and provider telemetry.
- `CONTROL > Advertised Models`: confirm which models clients expose.
- `CONTROL > Routing Policies`: configure model/client placement rules.
- `OPERATE > Requests`: inspect request history and routing evidence.
- `OPERATE > Explorer`: run live API calls against the server.
- `ADMINISTRATION > Users`, `Credentials`, and `Security`: manage users, access keys, roles, and permissions.

## Connect Clients

For a local Docker server, client machines should use:

```powershell
xeno server set-url ws://localhost:9000/ws/v1/clients
xeno server set-rest-url http://localhost:9000
xeno login -u admin@xeno --server http://localhost:9000
```

Use `ws://<server-host>:9000/ws/v1/clients` from other machines.

## Manage the Deployment

View logs:

```powershell
docker compose logs -f
```

Restart services:

```powershell
docker compose restart xeno-server xeno-dashboard
```

Stop the stack:

```powershell
docker compose down
```

Stop and remove persisted PostgreSQL data:

```powershell
docker compose down -v
```

Update images and restart:

```powershell
docker compose pull
docker compose up -d
```

## Next Steps

1. Change seeded credentials before using a shared environment.
2. Create tenant-scoped users and access keys in the dashboard.
3. Install one or more Xeno clients.
4. Add provider endpoints and advertised models on each client.
5. Confirm clients appear under `CONTROL > Clients`.
6. Confirm models appear under `CONTROL > Advertised Models`.
7. Configure routing policies.
8. Test inference through `OPERATE > Explorer`.
9. Watch `OPERATE > Requests` and `CONTROL > Client Telemetry` while traffic runs.
