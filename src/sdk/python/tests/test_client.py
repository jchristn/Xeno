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

    def test_default_options_send_seeded_bearer_without_tenant_header(self):
        opener = FakeOpener()
        client = XenoClient("https://xeno.local", opener=opener)

        client.list_openai_models()

        request = opener.calls[0]["request"]
        self.assertEqual(request.headers["Authorization"], "Bearer default")
        self.assertNotIn("X-tenant-guid", request.headers)

    def test_ping_sends_heartbeat_query_parameters(self):
        opener = FakeOpener(lambda request: FakeResponse('{"status":"healthy","sequence":3,"serverUtc":"2026-07-14T12:00:01Z"}'))
        client = XenoClient("https://xeno.local", bearer_token=None, opener=opener)

        response = client.ping(3, "2026-07-14T12:00:00Z")

        self.assertEqual(response["status"], "healthy")
        self.assertEqual(response["sequence"], 3)
        request = opener.calls[0]["request"]
        self.assertEqual(request.full_url, "https://xeno.local/v1.0/api/ping?sequence=3&clientSentUtc=2026-07-14T12%3A00%3A00Z")
        self.assertEqual(request.get_method(), "GET")
        self.assertNotIn("Authorization", request.headers)

    def test_onboard_uses_public_signup_route_and_surfaces_conflict(self):
        call_count = 0

        def response_factory(request):
            nonlocal call_count
            call_count += 1
            if call_count == 1:
                return FakeResponse(
                    '{"status":"success","tenant":{"id":"ten_1","name":"Tenant","active":true},"user":{"id":"usr_1","tenantId":"ten_1","username":"owner@xeno","email":"owner@xeno","isAdmin":false,"isTenantAdmin":true,"active":true},"credential":{"id":"cred_1","tenantId":"ten_1","userId":"usr_1","accessKey":"xeno_access","secretKey":"xeno_secret","active":true},"token":{"token":"tok_1","tokenType":"Bearer","tenantId":"ten_1","userId":"usr_1","sessionId":"sess_1"}}'
                )

            return HTTPError(
                "https://xeno.local/v1.0/api/onboarding/signup",
                409,
                "Conflict",
                {},
                FakeResponse('{"error":"EmailAlreadyExists","message":"Email already exists in the tenant."}', 409),
            )

        opener = FakeOpener(response_factory)
        client = XenoClient("https://xeno.local", bearer_token=None, opener=opener)

        response = client.onboard({"tenantName": "Tenant", "email": "owner@xeno", "username": "owner@xeno", "password": "password"})
        self.assertEqual(response["tenant"]["id"], "ten_1")
        self.assertEqual(response["credential"]["secretKey"], "xeno_secret")
        request = opener.calls[0]["request"]
        self.assertEqual(request.full_url, "https://xeno.local/v1.0/api/onboarding/signup")
        self.assertEqual(request.get_method(), "POST")
        self.assertNotIn("Authorization", request.headers)
        self.assertIn('"tenantName": "Tenant"', opener.calls[0]["body"])

        with self.assertRaises(XenoApiError) as context:
            client.onboard({"tenantId": "ten_1", "email": "owner@xeno", "password": "password"})
        self.assertEqual(context.exception.status, 409)
        self.assertEqual(context.exception.response["error"], "EmailAlreadyExists")

    def test_management_crud_methods_use_expected_routes_and_bodies(self):
        opener = FakeOpener(lambda request: FakeResponse("{}", 204 if request.get_method() == "DELETE" else 200))
        client = XenoClient("https://xeno.local", opener=opener)

        client.create_tenant({"name": "Tenant"})
        client.update_tenant("ten_1", {"name": "Tenant 2"})
        client.delete_tenant("ten_1")
        client.nuke_tenant({"tenantId": "ten_1", "confirmTenantId": "ten_1", "dryRun": True, "reason": "Testing tenant nuke"})
        client.create_user({"username": "user@xeno", "password": "password"})
        client.update_user("usr_1", {"active": False})
        client.delete_user("usr_1")
        client.nuke_user({"tenantId": "ten_1", "userId": "usr_1", "confirmUserId": "usr_1", "dryRun": True, "reason": "Testing user nuke"})
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
                ("POST", "https://xeno.local/v1.0/api/admin/nuke/tenant"),
                ("POST", "https://xeno.local/v1.0/api/users"),
                ("PUT", "https://xeno.local/v1.0/api/users/usr_1"),
                ("DELETE", "https://xeno.local/v1.0/api/users/usr_1"),
                ("POST", "https://xeno.local/v1.0/api/admin/nuke/user"),
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
        self.assertIn('"confirmTenantId": "ten_1"', opener.calls[3]["body"])
        self.assertIn('"confirmUserId": "usr_1"', opener.calls[7]["body"])
        self.assertIn('"permissionIds": ["perm_1"]', opener.calls[12]["body"])
        self.assertIn('"principalId": "usr_1"', opener.calls[19]["body"])
        self.assertIn('"modelName": "*"', opener.calls[21]["body"])

    def test_openai_and_request_history_methods_use_expected_routes(self):
        opener = FakeOpener()
        client = XenoClient("https://xeno.local", opener=opener)

        client.list_openai_models()
        client.create_chat_completion({"model": "llama3.2:latest"})
        client.create_embedding({"model": "text-embedding-3-small", "input": "hello"})
        client.list_ollama_models()
        client.create_ollama_chat({"model": "llama3.2:latest"})
        client.create_ollama_generate({"model": "llama3.2:latest", "prompt": "hello"})
        client.create_ollama_embedding({"model": "llama3.2:latest", "input": "hello"})
        client.list_request_history({"model": "llama3.2:latest", "empty": ""})
        client.get_model_token_leaderboard({"limit": 10, "offset": 0, "empty": ""})
        client.query_model_token_leaderboard({"limit": 10, "offset": 0})
        client.get_model_token_leaderboard()
        client.query_model_token_leaderboard()
        client.query_model_token_leaderboard(None)
        client.get_request_history_summary({"bucketMinutes": 60})

        self.assertEqual(
            [(call["request"].get_method(), call["request"].full_url) for call in opener.calls],
            [
                ("GET", "https://xeno.local/v1/models"),
                ("POST", "https://xeno.local/v1/chat/completions"),
                ("POST", "https://xeno.local/v1/embeddings"),
                ("GET", "https://xeno.local/api/tags"),
                ("POST", "https://xeno.local/api/chat"),
                ("POST", "https://xeno.local/api/generate"),
                ("POST", "https://xeno.local/api/embed"),
                ("GET", "https://xeno.local/v1.0/api/request-history?model=llama3.2%3Alatest"),
                ("GET", "https://xeno.local/v1.0/api/request-history/model-leaderboard?limit=10&offset=0"),
                ("POST", "https://xeno.local/v1.0/api/request-history/model-leaderboard"),
                ("GET", "https://xeno.local/v1.0/api/request-history/model-leaderboard"),
                ("POST", "https://xeno.local/v1.0/api/request-history/model-leaderboard"),
                ("POST", "https://xeno.local/v1.0/api/request-history/model-leaderboard"),
                ("GET", "https://xeno.local/v1.0/api/request-history/summary?bucketMinutes=60"),
            ],
        )
        self.assertIn('"offset": 0', opener.calls[9]["body"])
        self.assertEqual(opener.calls[11]["body"], "{}")
        self.assertEqual(opener.calls[12]["body"], "{}")

    def test_ledger_methods_use_expected_routes_and_bodies(self):
        def response_factory(request):
            if request.full_url.endswith("/v1.0/api/ledger/status"):
                return FakeResponse('{"queue":{"enabled":true,"queueDepth":1},"netLedgerHealthy":true}')
            if request.full_url.endswith("/v1.0/api/ledger/balance/me"):
                return FakeResponse('{"tenantId":"ten_default","userId":"usr_1","balance":5}')
            if "/v1.0/api/ledger/balances?" in request.full_url:
                return FakeResponse('{"items":[],"total":0,"page":1,"pageSize":100}')
            if request.full_url.endswith("/v1.0/api/ledger/balances/users/usr_1"):
                return FakeResponse('{"tenantId":"ten_default","userId":"usr_1","balance":5}')
            if request.full_url.endswith("/v1.0/api/ledger/entries/search"):
                return FakeResponse('{"items":[{"direction":"Credit","amount":7}],"total":1}')
            return FakeResponse("{}")

        opener = FakeOpener(response_factory)
        client = XenoClient("https://xeno.local", opener=opener)

        status = client.get_ledger_status()
        self.assertEqual(status["queue"]["queueDepth"], 1)
        balance = client.get_my_ledger_balance()
        self.assertEqual(balance["balance"], 5)
        client.search_ledger_balances({"userId": "usr_1", "page": 1, "empty": ""})
        client.get_ledger_user_balance("usr_1")
        client.get_my_ledger_entries()
        entries = client.search_ledger_entries({"userId": "usr_1", "direction": "Credit", "pageSize": 25})
        self.assertEqual(entries["items"][0]["amount"], 7)
        client.drain_ledger_queue()
        client.reconcile_ledger()

        self.assertEqual(
            [(call["request"].get_method(), call["request"].full_url) for call in opener.calls],
            [
                ("GET", "https://xeno.local/v1.0/api/ledger/status"),
                ("GET", "https://xeno.local/v1.0/api/ledger/balance/me"),
                ("GET", "https://xeno.local/v1.0/api/ledger/balances?userId=usr_1&page=1"),
                ("GET", "https://xeno.local/v1.0/api/ledger/balances/users/usr_1"),
                ("GET", "https://xeno.local/v1.0/api/ledger/entries/me"),
                ("POST", "https://xeno.local/v1.0/api/ledger/entries/search"),
                ("POST", "https://xeno.local/v1.0/api/ledger/drain"),
                ("POST", "https://xeno.local/v1.0/api/ledger/reconcile"),
            ],
        )
        self.assertIn('"direction": "Credit"', opener.calls[5]["body"])
        self.assertEqual(opener.calls[6]["body"], "{}")

    def test_credential_options_send_tenant_and_access_key_headers(self):
        opener = FakeOpener()
        client = XenoClient("https://xeno.local", bearer_token=None, access_key="xeno_access", tenant_id="ten_123", opener=opener)

        client.list_openai_models()

        request = opener.calls[0]["request"]
        self.assertEqual(request.headers["X-access-key"], "xeno_access")
        self.assertNotIn("X-secret-key", request.headers)
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
        anonymous_client = XenoClient("https://xeno.local", bearer_token=None, opener=anonymous_opener)
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

    def test_update_settings_passes_through_email_settings(self):
        opener = FakeOpener(lambda request: FakeResponse('{"Email":{"Provider":"File"}}'))
        client = XenoClient("https://xeno.local", opener=opener)

        response = client.update_settings({"Email": {"Enabled": True, "Provider": "File", "File": {"Directory": "data/email-outbox"}}})

        request = opener.calls[0]["request"]
        self.assertEqual(request.full_url, "https://xeno.local/v1.0/api/settings")
        self.assertEqual(request.get_method(), "POST")
        self.assertIn('"Email"', opener.calls[0]["body"])
        self.assertIn('"Provider": "File"', opener.calls[0]["body"])
        self.assertEqual(response["Email"]["Provider"], "File")
        self.assertNotIn("settings", response)

    def test_send_test_email_uses_administrative_email_route(self):
        opener = FakeOpener(lambda request: FakeResponse('{"accepted":true,"skipped":false,"provider":"Mailgun","providerMessageId":"msg_1","statusMessage":"Email accepted by Mailgun.","templateId":"administrative-test","toEmail":"joel.christner@gmail.com","subject":"Xeno test email","sentUtc":"2026-07-10T00:00:00Z"}'))
        client = XenoClient("https://xeno.local", opener=opener)

        response = client.send_test_email({"toEmail": "joel.christner@gmail.com", "body": "hello world"})

        self.assertTrue(response["accepted"])
        self.assertEqual(response["templateId"], "administrative-test")
        request = opener.calls[0]["request"]
        self.assertEqual(request.full_url, "https://xeno.local/v1.0/api/email/test")
        self.assertEqual(request.get_method(), "POST")
        self.assertIn('"toEmail": "joel.christner@gmail.com"', opener.calls[0]["body"])
        self.assertIn('"body": "hello world"', opener.calls[0]["body"])


if __name__ == "__main__":
    unittest.main()
