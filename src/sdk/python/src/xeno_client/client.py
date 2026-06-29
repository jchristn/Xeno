from __future__ import annotations

import json
from dataclasses import dataclass
from typing import Any
from urllib.error import HTTPError
from urllib.parse import urlencode
from urllib.request import Request, build_opener


@dataclass
class XenoApiError(Exception):
    message: str
    status: int
    endpoint: str
    response: Any

    def __str__(self) -> str:
        return self.message


class XenoModel:
    def __init__(self, **data: Any) -> None:
        self.raw = dict(data)
        for key, value in data.items():
            setattr(self, key, value)
            setattr(self, self._snake_case(key), value)

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> "XenoModel":
        return cls(**data)

    @staticmethod
    def _snake_case(value: str) -> str:
        chars: list[str] = []
        for index, char in enumerate(value):
            if char.isupper() and index > 0:
                chars.append("_")
            chars.append(char.lower())
        return "".join(chars)


class AuditRecord(XenoModel):
    pass


class Credential(XenoModel):
    pass


class CredentialLoginResponse(XenoModel):
    pass


class EffectivePermission(XenoModel):
    pass


class HealthResponse(XenoModel):
    pass


class ModelConfiguration(XenoModel):
    pass


class ModelDefinition(XenoModel):
    pass


class OpenAiChatCompletion(XenoModel):
    pass


class OpenAiEmbeddingList(XenoModel):
    pass


class OpenAiModelList(XenoModel):
    pass


class Permission(XenoModel):
    pass


class RequestHistoryEntry(XenoModel):
    pass


class RequestHistorySummary(XenoModel):
    pass


class Role(XenoModel):
    pass


class RoleAssignment(XenoModel):
    pass


class RoutingPolicy(XenoModel):
    pass


class RoutingPolicyMetricDefinition(XenoModel):
    pass


class RoutingPolicyValidationResult(XenoModel):
    pass


class ServerClientTelemetryCollectionAttemptRecord(XenoModel):
    pass


class ServerClientTelemetryCollectionStatusResponse(XenoModel):
    pass


class ServerClientTelemetryCollectResponse(XenoModel):
    pass


class ServerClientTelemetryLatestResponse(XenoModel):
    pass


class ServerClientTelemetryRollupBucket(XenoModel):
    pass


class ServerClientTelemetrySampleDetail(XenoModel):
    pass


class ServerClientTelemetrySearchResult(XenoModel):
    pass


class ServerProviderTelemetryRollupBucket(XenoModel):
    pass


class ServerProviderTelemetrySampleDetail(XenoModel):
    pass


class ServerProviderTelemetrySampleRecord(XenoModel):
    pass


class ServerProviderTelemetrySearchResult(XenoModel):
    pass


class SettingsResponse(XenoModel):
    pass


class Tenant(XenoModel):
    pass


class TokenResponse(XenoModel):
    pass


class User(XenoModel):
    pass


class XenoClientRecord(XenoModel):
    pass


class XenoObjectResponse(XenoModel):
    pass


class XenoClient:
    def __init__(
        self,
        base_url: str = "http://localhost:9000",
        bearer_token: str | None = None,
        access_key: str | None = None,
        tenant_id: str = "ten_default",
        opener: Any | None = None,
    ) -> None:
        if not base_url:
            raise ValueError("base_url is required")

        self.base_url = base_url.rstrip("/")
        self.bearer_token = bearer_token
        self.access_key = access_key
        self.tenant_id = tenant_id
        self.opener = opener or build_opener()

    def get_health(self) -> dict[str, Any]:
        return self._request("GET", "/health")

    def get_openapi(self) -> dict[str, Any]:
        return self._request("GET", "/openapi.json")

    def get_metrics(self) -> str:
        return self._request("GET", "/metrics", response_type="text")

    def login(self, username: str, password: str, tenant_id: str | None = None) -> dict[str, Any]:
        return self._request("POST", "/v1.0/api/auth/login", {"username": username, "password": password, "tenantId": tenant_id or self.tenant_id})

    def create_token(self, access_key: str) -> dict[str, Any]:
        return self._request("POST", "/v1.0/api/auth/token", {"accessKey": access_key})

    def get_token_details(self) -> dict[str, Any]:
        return self._request("GET", "/v1.0/api/auth/token/details")

    def refresh_token(self) -> dict[str, Any]:
        return self._request("POST", "/v1.0/api/auth/token/refresh", {})

    def logout(self) -> dict[str, Any]:
        return self._request("POST", "/v1.0/api/auth/logout", {})

    def list_tenants(self) -> list[dict[str, Any]]:
        return self._request("GET", "/v1.0/api/tenants")

    def create_tenant(self, tenant: dict[str, Any]) -> dict[str, Any]:
        return self._request("POST", "/v1.0/api/tenants", tenant)

    def update_tenant(self, tenant_id: str, tenant: dict[str, Any]) -> dict[str, Any]:
        return self._request("PUT", f"/v1.0/api/tenants/{tenant_id}", tenant)

    def delete_tenant(self, tenant_id: str) -> None:
        return self._request("DELETE", f"/v1.0/api/tenants/{tenant_id}")

    def list_users(self) -> list[dict[str, Any]]:
        return self._request("GET", "/v1.0/api/users")

    def create_user(self, user: dict[str, Any]) -> dict[str, Any]:
        return self._request("POST", "/v1.0/api/users", user)

    def update_user(self, user_id: str, user: dict[str, Any]) -> dict[str, Any]:
        return self._request("PUT", f"/v1.0/api/users/{user_id}", user)

    def delete_user(self, user_id: str) -> None:
        return self._request("DELETE", f"/v1.0/api/users/{user_id}")

    def list_credentials(self) -> list[dict[str, Any]]:
        return self._request("GET", "/v1.0/api/credentials")

    def create_credential(self, credential: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("POST", "/v1.0/api/credentials", credential or {})

    def update_credential(self, credential_id: str, credential: dict[str, Any]) -> dict[str, Any]:
        return self._request("PUT", f"/v1.0/api/credentials/{credential_id}", credential)

    def delete_credential(self, credential_id: str) -> None:
        return self._request("DELETE", f"/v1.0/api/credentials/{credential_id}")

    def list_roles(self) -> list[dict[str, Any]]:
        return self._request("GET", "/v1.0/api/roles")

    def create_role(self, role: dict[str, Any]) -> dict[str, Any]:
        return self._request("POST", "/v1.0/api/roles", role)

    def update_role(self, role_id: str, role: dict[str, Any]) -> dict[str, Any]:
        return self._request("PUT", f"/v1.0/api/roles/{role_id}", role)

    def delete_role(self, role_id: str) -> None:
        return self._request("DELETE", f"/v1.0/api/roles/{role_id}")

    def list_permissions(self) -> list[dict[str, Any]]:
        return self._request("GET", "/v1.0/api/permissions")

    def list_grantable_permissions(self) -> list[dict[str, Any]]:
        return self._request("GET", "/v1.0/api/permissions/grantable")

    def create_permission(self, permission: dict[str, Any]) -> dict[str, Any]:
        return self._request("POST", "/v1.0/api/permissions", permission)

    def update_permission(self, permission_id: str, permission: dict[str, Any]) -> dict[str, Any]:
        return self._request("PUT", f"/v1.0/api/permissions/{permission_id}", permission)

    def delete_permission(self, permission_id: str) -> None:
        return self._request("DELETE", f"/v1.0/api/permissions/{permission_id}")

    def list_effective_permissions(self) -> list[dict[str, Any]]:
        return self._request("GET", "/v1.0/api/effective-permissions")

    def list_role_assignments(self) -> list[dict[str, Any]]:
        return self._request("GET", "/v1.0/api/role-assignments")

    def create_role_assignment(self, assignment: dict[str, Any]) -> dict[str, Any]:
        return self._request("POST", "/v1.0/api/role-assignments", assignment)

    def delete_role_assignment(self, assignment_id: str) -> None:
        return self._request("DELETE", f"/v1.0/api/role-assignments/{assignment_id}")

    def list_audit_records(self) -> list[dict[str, Any]]:
        return self._request("GET", "/v1.0/api/audit")

    def delete_audit_record(self, audit_id: str) -> None:
        return self._request("DELETE", f"/v1.0/api/audit/{audit_id}")

    def list_clients(self) -> list[dict[str, Any]]:
        return self._request("GET", "/v1.0/api/clients")

    def delete_client(self, client_id: str) -> None:
        return self._request("DELETE", f"/v1.0/api/clients/{client_id}")

    def delete_client_model(self, model: dict[str, Any]) -> dict[str, Any]:
        return self._request("DELETE", "/v1.0/api/client-models", model)

    def get_client_telemetry_status(self) -> dict[str, Any]:
        return self._request("GET", "/v1.0/api/client-telemetry/status")

    def get_latest_client_telemetry(self, filters: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/api/client-telemetry/latest{self._query_string(filters)}")

    def collect_client_telemetry(self, request: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("POST", "/v1.0/api/client-telemetry/collect", request or {})

    def search_client_telemetry(self, request: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("POST", "/v1.0/api/client-telemetry/history/search", request or {})

    def get_client_telemetry_rollups(self, request: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("POST", "/v1.0/api/client-telemetry/history/rollups", request or {})

    def get_client_telemetry_detail(self, sample_id: str) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/api/client-telemetry/history/{sample_id}")

    def delete_client_telemetry(self, sample_id: str) -> None:
        return self._request("DELETE", f"/v1.0/api/client-telemetry/history/{sample_id}")

    def get_latest_client_provider_telemetry(self, filters: dict[str, Any] | None = None) -> list[dict[str, Any]]:
        return self._request("GET", f"/v1.0/api/client-telemetry/providers/latest{self._query_string(filters)}")

    def search_client_provider_telemetry(self, request: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("POST", "/v1.0/api/client-telemetry/providers/history/search", request or {})

    def get_client_provider_telemetry_rollups(self, request: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("POST", "/v1.0/api/client-telemetry/providers/history/rollups", request or {})

    def get_client_provider_telemetry_detail(self, sample_id: str) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/api/client-telemetry/providers/history/{sample_id}")

    def delete_client_provider_telemetry(self, sample_id: str) -> None:
        return self._request("DELETE", f"/v1.0/api/client-telemetry/providers/history/{sample_id}")

    def get_client_telemetry_metric_catalog(self) -> list[dict[str, Any]]:
        return self._request("GET", "/v1.0/api/client-telemetry/metrics")

    def list_model_definitions(self) -> list[dict[str, Any]]:
        return self._request("GET", "/v1.0/api/model-definitions")

    def list_model_configurations(self) -> list[dict[str, Any]]:
        return self._request("GET", "/v1.0/api/model-configurations")

    def list_routing_policies(self) -> list[dict[str, Any]]:
        return self._request("GET", "/v1.0/api/routing-policies")

    def create_routing_policy(self, policy: dict[str, Any]) -> dict[str, Any]:
        return self._request("POST", "/v1.0/api/routing-policies", policy)

    def update_routing_policy(self, policy_id: str, policy: dict[str, Any]) -> dict[str, Any]:
        return self._request("PUT", f"/v1.0/api/routing-policies/{policy_id}", policy)

    def delete_routing_policy(self, policy_id: str) -> None:
        return self._request("DELETE", f"/v1.0/api/routing-policies/{policy_id}")

    def list_openai_models(self) -> dict[str, Any]:
        return self._request("GET", "/v1/models")

    def create_chat_completion(self, request: dict[str, Any]) -> dict[str, Any]:
        return self._request("POST", "/v1/chat/completions", request)

    def create_completion(self, request: dict[str, Any]) -> dict[str, Any]:
        return self._request("POST", "/v1/completions", request)

    def create_embedding(self, request: dict[str, Any]) -> dict[str, Any]:
        return self._request("POST", "/v1/embeddings", request)

    def list_request_history(self, filters: dict[str, Any] | None = None) -> list[dict[str, Any]]:
        return self._request("GET", f"/v1.0/api/request-history{self._query_string(filters)}")

    def get_request_history(self, history_id: str) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/api/request-history/{history_id}")

    def delete_request_history(self, history_id: str) -> None:
        return self._request("DELETE", f"/v1.0/api/request-history/{history_id}")

    def get_request_history_summary(self, filters: dict[str, Any] | None = None) -> dict[str, Any]:
        return self._request("GET", f"/v1.0/api/request-history/summary{self._query_string(filters)}")

    def get_request_analytics_overview(self) -> dict[str, Any]:
        return self._request("GET", "/v1.0/api/request-history/analytics/overview")

    def bulk_delete_request_history(self, filter_payload: dict[str, Any]) -> dict[str, Any]:
        return self._request("POST", "/v1.0/api/request-history/bulk", filter_payload)

    def get_analytics_overview(self) -> dict[str, Any]:
        return self._request("GET", "/v1.0/api/analytics/overview")

    def get_settings(self) -> dict[str, Any]:
        return self._request("GET", "/v1.0/api/settings")

    def update_settings(self, settings: dict[str, Any]) -> dict[str, Any]:
        return self._request("POST", "/v1.0/api/settings", settings)

    def get_backup(self) -> dict[str, Any]:
        return self._request("GET", "/v1.0/api/backup")

    def restore_backup(self, backup: dict[str, Any]) -> dict[str, Any]:
        return self._request("POST", "/v1.0/api/restore", backup)

    def _request(self, method: str, endpoint: str, body: dict[str, Any] | None = None, response_type: str = "json") -> Any:
        payload = None if body is None else json.dumps(body).encode("utf-8")
        request = Request(f"{self.base_url}{endpoint}", data=payload, method=method)
        if response_type != "text":
            request.add_header("Content-Type", "application/json")
        if self.bearer_token:
            request.add_header("Authorization", f"Bearer {self.bearer_token}")
        if self.access_key:
            request.add_header("x-access-key", self.access_key)
        if self.tenant_id:
            request.add_header("x-tenant-guid", self.tenant_id)

        try:
            response = self.opener.open(request, timeout=60)
            status = getattr(response, "status", getattr(response, "code", 200))
            raw = response.read()
        except HTTPError as error:
            status = error.code
            raw = error.read()

        text = raw.decode("utf-8") if isinstance(raw, bytes) else str(raw or "")
        if status < 200 or status >= 300:
            error_data = self._decode_json(text)
            message = error_data.get("message") or error_data.get("error") or f"HTTP {status}"
            raise XenoApiError(message, status, endpoint, error_data)

        if status == 204:
            return None

        if response_type == "text":
            return text

        return self._decode_json(text or "{}")

    @staticmethod
    def _query_string(filters: dict[str, Any] | None) -> str:
        if not filters:
            return ""

        serialized = urlencode({key: value for key, value in filters.items() if value not in (None, "")})
        return f"?{serialized}" if serialized else ""

    @staticmethod
    def _decode_json(text: str) -> Any:
        try:
            return json.loads(text or "{}")
        except json.JSONDecodeError:
            return {"message": text}
