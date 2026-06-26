from __future__ import annotations

import json
import sys
from getpass import getpass

from .client import XenoApiError, XenoClient


def main() -> int:
    base_url = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:9000"
    tenant_id = "ten_default"
    bearer_token: str | None = None

    print_help(base_url, tenant_id)
    while True:
        try:
            try:
                command = input("xeno-sdk [? for help]> ").strip()
            except EOFError:
                return 0
            if not command:
                continue
            if command in ("?", "help"):
                print_help(base_url, tenant_id)
            elif command in ("q", "quit"):
                return 0
            elif command == "cls":
                print("\033c", end="")
            elif command.startswith("url "):
                base_url = command[4:].strip()
            elif command.startswith("tenant "):
                tenant_id = command[7:].strip()
            elif command.startswith("token "):
                bearer_token = command[6:].strip()
            else:
                run_command(command, XenoClient(base_url=base_url, tenant_id=tenant_id, bearer_token=bearer_token), tenant_id)
        except XenoApiError as error:
            print(f"API {error.status} at {error.endpoint}: {error.message}")
        except (ValueError, OSError) as error:
            print(f"Error: {error}")


def run_command(command: str, client: XenoClient, tenant_id: str) -> None:
    if command == "health":
        print_json(client.get_health())
    elif command == "models":
        print_json(client.list_openai_models())
    elif command == "clients":
        print_json(client.list_clients())
    elif command == "tenants":
        print_json(client.list_tenants())
    elif command == "users":
        print_json(client.list_users())
    elif command == "credentials":
        print_json(client.list_credentials())
    elif command == "roles":
        print_json(client.list_roles())
    elif command == "permissions":
        print_json(client.list_permissions())
    elif command == "routing-policies":
        print_json(client.list_routing_policies())
    elif command == "history":
        print_json(client.list_request_history())
    elif command == "settings":
        print_json(client.get_settings())
    elif command == "metrics":
        print(client.get_metrics())
    elif command == "login":
        username = prompt("Username", "admin@xeno")
        password = getpass("Password [password]: ") or "password"
        print_json(client.login(username, password, tenant_id))
    elif command == "chat":
        model = prompt("Model", "llama3.2:latest")
        content = prompt("Prompt", "hello")
        print_json(client.create_chat_completion({"model": model, "messages": [{"role": "user", "content": content}]}))
    else:
        print("Unknown command. Type ? for help.")


def prompt(label: str, fallback: str) -> str:
    value = input(f"{label} [{fallback}]: ").strip()
    return value or fallback


def print_json(value: object) -> None:
    print(json.dumps(value, indent=2))


def print_help(base_url: str, tenant_id: str) -> None:
    print("")
    print("Connection")
    print(f"  url <url>       set server URL, current {base_url}")
    print(f"  tenant <id>     set tenant, current {tenant_id}")
    print("  token <token>   set bearer token for authenticated servers")
    print("")
    print("Read commands")
    print("  health          server health")
    print("  models          OpenAI-compatible model list")
    print("  clients         connected/registered clients")
    print("  tenants         tenants")
    print("  users           users")
    print("  credentials     credentials")
    print("  roles           roles")
    print("  permissions     permissions")
    print("  routing-policies routing policies")
    print("  history         request history")
    print("  settings        server settings")
    print("  metrics         Prometheus metrics")
    print("")
    print("Actions")
    print("  login           exchange username/password for a client credential")
    print("  chat            send a chat completion request")
    print("")
    print("Other")
    print("  cls             clear screen")
    print("  q               quit")
    print("")


if __name__ == "__main__":
    raise SystemExit(main())
