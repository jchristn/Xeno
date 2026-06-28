from pathlib import Path
import sys
import unittest
from urllib.error import HTTPError

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "src"))

from xeno_client import XenoApiError, XenoClient


class FakeResponse:
    def __init__(self, body="{}", status=200):
        self.body = body.encode("utf-8")
        self.status = status
        self.code = status

    def read(self):
        return self.body

    def close(self):
        return None


class FakeOpener:
    def __init__(self, response_factory=None):
        self.calls = []
        self.response_factory = response_factory or (lambda request: FakeResponse("{}"))

    def open(self, request, timeout=60):
        body = request.data.decode("utf-8") if request.data else ""
        self.calls.append({"request": request, "body": body, "timeout": timeout})
        response = self.response_factory(request)
        if isinstance(response, HTTPError):
            raise response
        return response


class XenoClientTests(unittest.TestCase):
    def test_list_tenants_sends_tenant_and_bearer_headers(self):
        opener = FakeOpener(lambda request: FakeResponse("[]"))
        client = XenoClient("https://xeno.local", bearer_token="token-123", tenant_id="ten_123", opener=opener)

        client.list_tenants()

        request = opener.calls[0]["request"]
        self.assertEqual(request.full_url, "https://xeno.local/v1.0/api/tenants")
        self.assertEqual(request.get_method(), "GET")
        self.assertEqual(request.headers["Authorization"], "Bearer token-123")
        self.assertEqual(request.headers["X-tenant-guid"], "ten_123")

    def test_management_crud_methods_use_expected_routes_and_bodies(self):
        opener = FakeOpener(lambda request: FakeResponse("{}", 204 if request.get_method() == "DELETE" else 200))
        client = XenoClient("https://xeno.local", opener=opener)

        client.create_tenant({"name": "Tenant"})
        client.update_tenant("ten_1", {"name": "Tenant 2"})
        client.delete_tenant("ten_1")
        client.create_user({"username": "user@xeno", "password": "password"})
        client.update_user("usr_1", {"active": False})
        client.delete_user("usr_1")
        client.create_credential({"userId": "usr_1"})
        client.update_credential("cred_1", {"active": True})
        client.delete_credential("cred_1")
        client.create_role({"name": "Operator"})
        client.update_role("role_1", {"permissionIds": ["perm_1"]})
        client.delete_role("role_1")
        client.list_grantable_permissions()
        client.create_permission({"resourceType": "Clients", "operationType": "Read"})
        client.update_permission("perm_1", {"permit": False})
        client.delete_permission("perm_1")
        client.list_role_assignments()
        client.create_role_assignment({"roleId": "role_1", "principalType": "User", "principalId": "usr_1"})
        client.delete_role_assignment("rassn_1")
        client.create_routing_policy({"name": "Policy", "modelName": "*", "clientName": "*"})
        client.update_routing_policy("rpol_1", {"loadBalancingMode": "Random"})
        client.delete_routing_policy("rpol_1")

        self.assertEqual(
            [(call["request"].get_method(), call["request"].full_url) for call in opener.calls],
            [
                ("POST", "https://xeno.local/v1.0/api/tenants"),
                ("PUT", "https://xeno.local/v1.0/api/tenants/ten_1"),
                ("DELETE", "https://xeno.local/v1.0/api/tenants/ten_1"),
                ("POST", "https://xeno.local/v1.0/api/users"),
                ("PUT", "https://xeno.local/v1.0/api/users/usr_1"),
                ("DELETE", "https://xeno.local/v1.0/api/users/usr_1"),
                ("POST", "https://xeno.local/v1.0/api/credentials"),
                ("PUT", "https://xeno.local/v1.0/api/credentials/cred_1"),
                ("DELETE", "https://xeno.local/v1.0/api/credentials/cred_1"),
                ("POST", "https://xeno.local/v1.0/api/roles"),
                ("PUT", "https://xeno.local/v1.0/api/roles/role_1"),
                ("DELETE", "https://xeno.local/v1.0/api/roles/role_1"),
                ("GET", "https://xeno.local/v1.0/api/permissions/grantable"),
                ("POST", "https://xeno.local/v1.0/api/permissions"),
                ("PUT", "https://xeno.local/v1.0/api/permissions/perm_1"),
                ("DELETE", "https://xeno.local/v1.0/api/permissions/perm_1"),
                ("GET", "https://xeno.local/v1.0/api/role-assignments"),
                ("POST", "https://xeno.local/v1.0/api/role-assignments"),
                ("DELETE", "https://xeno.local/v1.0/api/role-assignments/rassn_1"),
                ("POST", "https://xeno.local/v1.0/api/routing-policies"),
                ("PUT", "https://xeno.local/v1.0/api/routing-policies/rpol_1"),
                ("DELETE", "https://xeno.local/v1.0/api/routing-policies/rpol_1"),
            ],
        )
        self.assertIn('"name": "Tenant"', opener.calls[0]["body"])
        self.assertIn('"permissionIds": ["perm_1"]', opener.calls[10]["body"])
        self.assertIn('"principalId": "usr_1"', opener.calls[17]["body"])
        self.assertIn('"modelName": "*"', opener.calls[19]["body"])

    def test_openai_and_request_history_methods_use_expected_routes(self):
        opener = FakeOpener()
        client = XenoClient("https://xeno.local", opener=opener)

        client.list_openai_models()
        client.create_chat_completion({"model": "llama3.2:latest"})
        client.create_embedding({"model": "text-embedding-3-small", "input": "hello"})
        client.list_request_history({"model": "llama3.2:latest", "empty": ""})
        client.get_request_history_summary({"bucketMinutes": 60})

        self.assertEqual(
            [(call["request"].get_method(), call["request"].full_url) for call in opener.calls],
            [
                ("GET", "https://xeno.local/v1/models"),
                ("POST", "https://xeno.local/v1/chat/completions"),
                ("POST", "https://xeno.local/v1/embeddings"),
                ("GET", "https://xeno.local/v1.0/api/request-history?model=llama3.2%3Alatest"),
                ("GET", "https://xeno.local/v1.0/api/request-history/summary?bucketMinutes=60"),
            ],
        )

    def test_credential_options_send_tenant_and_credential_headers(self):
        opener = FakeOpener()
        client = XenoClient("https://xeno.local", access_key="xeno_access", secret_key="secret-123", tenant_id="ten_123", opener=opener)

        client.list_openai_models()

        request = opener.calls[0]["request"]
        self.assertEqual(request.headers["X-access-key"], "xeno_access")
        self.assertEqual(request.headers["X-secret-key"], "secret-123")
        self.assertEqual(request.headers["X-tenant-guid"], "ten_123")

    def test_openai_errors_surface_authentication_and_authorization_status(self):
        unauthenticated_error = HTTPError(
            "https://xeno.local/v1/models",
            401,
            "Unauthorized",
            {},
            FakeResponse('{"error":"Unauthenticated","message":"Authentication is required."}', 401),
        )
        anonymous_opener = FakeOpener(lambda request: unauthenticated_error)
        anonymous_client = XenoClient("https://xeno.local", opener=anonymous_opener)
        with self.assertRaises(XenoApiError) as unauthenticated_context:
            anonymous_client.list_openai_models()
        self.assertEqual(unauthenticated_context.exception.status, 401)
        self.assertEqual(unauthenticated_context.exception.endpoint, "/v1/models")

        denied_error = HTTPError(
            "https://xeno.local/v1/chat/completions",
            403,
            "Forbidden",
            {},
            FakeResponse('{"error":"PermissionDenied","message":"Permission denied."}', 403),
        )
        denied_opener = FakeOpener(lambda request: denied_error)
        denied_client = XenoClient("https://xeno.local", bearer_token="token-123", tenant_id="ten_123", opener=denied_opener)
        with self.assertRaises(XenoApiError) as denied_context:
            denied_client.create_chat_completion({"model": "llama3.2:latest"})
        self.assertEqual(denied_context.exception.status, 403)
        self.assertEqual(denied_context.exception.endpoint, "/v1/chat/completions")

    def test_client_telemetry_methods_use_expected_routes_and_bodies(self):
        opener = FakeOpener(lambda request: FakeResponse("{}", 204 if request.get_method() == "DELETE" else 200))
        client = XenoClient("https://xeno.local", opener=opener)

        client.get_client_telemetry_status()
        client.get_latest_client_telemetry({"clientId": "xcl_1", "empty": ""})
        client.collect_client_telemetry({"clientId": "xcl_1", "collectHostTelemetry": True})
        client.search_client_telemetry({"clientId": "xcl_1", "metric": "telemetry.cpu.utilizationPercent"})
        client.get_client_telemetry_rollups({"metric": "telemetry.cpu.utilizationPercent"})
        client.get_client_telemetry_detail("ctel_1")
        client.delete_client_telemetry("ctel_1")
        client.get_latest_client_provider_telemetry({"clientId": "xcl_1"})
        client.search_client_provider_telemetry({"providerName": "ollama"})
        client.get_client_provider_telemetry_rollups({"metric": "provider.queuedRequests"})
        client.get_client_provider_telemetry_detail("cptel_1")
        client.delete_client_provider_telemetry("cptel_1")
        client.get_client_telemetry_metric_catalog()

        self.assertEqual(
            [(call["request"].get_method(), call["request"].full_url) for call in opener.calls],
            [
                ("GET", "https://xeno.local/v1.0/api/client-telemetry/status"),
                ("GET", "https://xeno.local/v1.0/api/client-telemetry/latest?clientId=xcl_1"),
                ("POST", "https://xeno.local/v1.0/api/client-telemetry/collect"),
                ("POST", "https://xeno.local/v1.0/api/client-telemetry/history/search"),
                ("POST", "https://xeno.local/v1.0/api/client-telemetry/history/rollups"),
                ("GET", "https://xeno.local/v1.0/api/client-telemetry/history/ctel_1"),
                ("DELETE", "https://xeno.local/v1.0/api/client-telemetry/history/ctel_1"),
                ("GET", "https://xeno.local/v1.0/api/client-telemetry/providers/latest?clientId=xcl_1"),
                ("POST", "https://xeno.local/v1.0/api/client-telemetry/providers/history/search"),
                ("POST", "https://xeno.local/v1.0/api/client-telemetry/providers/history/rollups"),
                ("GET", "https://xeno.local/v1.0/api/client-telemetry/providers/history/cptel_1"),
                ("DELETE", "https://xeno.local/v1.0/api/client-telemetry/providers/history/cptel_1"),
                ("GET", "https://xeno.local/v1.0/api/client-telemetry/metrics"),
            ],
        )
        self.assertIn('"clientId": "xcl_1"', opener.calls[2]["body"])
        self.assertIn('"metric": "telemetry.cpu.utilizationPercent"', opener.calls[3]["body"])
        self.assertIn('"providerName": "ollama"', opener.calls[8]["body"])

    def test_text_and_error_responses_are_handled(self):
        metrics_opener = FakeOpener(lambda request: FakeResponse("xeno_connected_clients 1"))
        metrics_client = XenoClient("https://xeno.local", opener=metrics_opener)
        self.assertEqual(metrics_client.get_metrics(), "xeno_connected_clients 1")

        error = HTTPError(
            "https://xeno.local/v1.0/api/clients",
            403,
            "Forbidden",
            {},
            FakeResponse('{"message":"denied"}', 403),
        )
        error_opener = FakeOpener(lambda request: error)
        error_client = XenoClient("https://xeno.local", opener=error_opener)
        with self.assertRaises(XenoApiError) as context:
            error_client.list_clients()
        self.assertEqual(context.exception.status, 403)
        self.assertEqual(context.exception.endpoint, "/v1.0/api/clients")
        self.assertEqual(str(context.exception), "denied")


if __name__ == "__main__":
    unittest.main()
