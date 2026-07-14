# Xeno Server on Linux

This guide deploys the Xeno server and web dashboard with Docker on Linux.

## Defaults

- Dashboard: `http://localhost:9100`
- Server REST API: `http://localhost:9000`
- Health check: `http://localhost:9000/health`
- Ping heartbeat: `http://localhost:9000/v1.0/api/ping`
- Swagger UI: `http://localhost:9000/swagger`
- Client WebSocket: `ws://localhost:9000/ws/v1/clients`
- Dashboard user: `admin@xeno`
- Dashboard password: `password`
- Default client access key: `default`
- Default REST bearer token: `default`

## Prerequisites

1. Install Docker Engine and the Docker Compose plugin.
2. Install Git.
3. Confirm Docker works.

```bash
docker version
docker compose version
```

4. Clone the repository.

```bash
git clone https://github.com/jchristn/Xeno.git
cd Xeno
```

## Deploy with Docker

1. Change to the Docker directory.

```bash
cd docker
```

2. Review the server configuration.

```bash
nano xeno.json
```

3. Start PostgreSQL, the server, and the dashboard.

```bash
docker compose pull
docker compose up -d
```

4. Confirm all containers are running or healthy.

```bash
docker compose ps
```

5. Check the server health endpoint.

```bash
curl http://localhost:9000/health
```

6. Follow logs if startup fails.

```bash
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

3. Use the dashboard to manage the server:

- `CONTROL > Clients`: connected client state.
- `CONTROL > Client Telemetry`: server-collected host and provider telemetry.
- `CONTROL > Advertised Models`: model inventory.
- `CONTROL > Routing Policies`: model placement.
- `OPERATE > Requests`: request history.
- `OPERATE > Explorer`: live API testing.
- `ADMINISTRATION`: tenants, users, credentials, roles, permissions, and security.

## Connect Clients

For a client on the same Linux host:

```bash
xeno server set-url ws://localhost:9000/ws/v1/clients
xeno server set-rest-url http://localhost:9000
xeno login -u admin@xeno --server http://localhost:9000
```

For remote clients, use the reachable server host:

```bash
xeno server set-url ws://<server-host>:9000/ws/v1/clients
xeno server set-rest-url http://<server-host>:9000
```

## Manage the Deployment

View logs:

```bash
docker compose logs -f
```

Restart services:

```bash
docker compose restart xeno-server xeno-dashboard
```

Stop the stack:

```bash
docker compose down
```

Stop and remove persisted PostgreSQL data:

```bash
docker compose down -v
```

Update images and restart:

```bash
docker compose pull
docker compose up -d
```

## Next Steps

1. Change seeded credentials before using a shared environment.
2. Create tenant-scoped users and access keys.
3. Install clients on machines that can reach model providers.
4. Add provider endpoints and advertised models.
5. Confirm clients and models in the dashboard.
6. Configure routing policies.
7. Test routes in `OPERATE > Explorer`.
8. Monitor `OPERATE > Requests` and `CONTROL > Client Telemetry`.
