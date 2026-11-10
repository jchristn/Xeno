namespace Test.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client;
    using Xeno.Client.LocalInference;
    using Xeno.Core.Models;

    /// <summary>
    /// Runs a real local inference listener in an isolated client home with a fake websocket tunnel, for routing tests.
    /// </summary>
    internal sealed class ListenerRoutingHarness : IDisposable
    {
        private readonly ClientHomeScope _Home = new ClientHomeScope();
        private bool _Disposed;

        internal ListenerRoutingHarness(Action<ClientSettings> configure, bool tunnel = true)
        {
            Settings = new ClientSettings
            {
                ClientId = "xcl_routing_test",
                TenantId = "ten_default",
                Providers = new List<ClientRunnerSettings>(),
                LocalInferenceServer = new ClientLocalInferenceServerSettings
                {
                    Enabled = true,
                    Host = "127.0.0.1",
                    Port = FakeProviderServer.GetFreePort(),
                    HealthCheckIntervalMs = 300000
                }
            };
            configure?.Invoke(Settings);
            if (tunnel)
            {
                LocalInferenceTunnelBridge.Register(Settings, async delegate (object payload, CancellationToken token)
                {
                    ClientLocalInferenceRequestMessage? request = payload as ClientLocalInferenceRequestMessage;
                    if (request == null) return;
                    TunnelRequests.Enqueue(request);
                    ServerLocalInferenceResponseMessage response = TunnelResponder(request);
                    response.RequestId = request.RequestId;
                    if (String.IsNullOrWhiteSpace(response.Type)) response.Type = "server.localInference.response";
                    LocalInferenceTunnelBridge.AcceptResponse(response);
                    await Task.CompletedTask.ConfigureAwait(false);
                });
            }
            else
            {
                LocalInferenceTunnelBridge.Clear();
            }

            Service = new LocalInferenceServerService(Settings);
            Service.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            Http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        internal ClientSettings Settings { get; }

        internal LocalInferenceServerService Service { get; }

        internal HttpClient Http { get; }

        internal ConcurrentQueue<ClientLocalInferenceRequestMessage> TunnelRequests { get; } = new ConcurrentQueue<ClientLocalInferenceRequestMessage>();

        internal Func<ClientLocalInferenceRequestMessage, ServerLocalInferenceResponseMessage> TunnelResponder { get; set; } = request => new ServerLocalInferenceResponseMessage
        {
            Success = true,
            StatusCode = 200,
            ContentType = "application/json",
            Body = "{\"from\":\"network\"}"
        };

        internal string Url(string path)
        {
            return Service.Url + path;
        }

        internal static ClientRunnerSettings Provider(string name, string apiType, string endpoint, params string[] models)
        {
            ClientRunnerSettings provider = new ClientRunnerSettings { Name = name, ApiType = apiType, Endpoint = endpoint, Models = new List<ClientModelSettings>(), TimeoutMs = 5000 };
            foreach (string model in models) provider.Models.Add(new ClientModelSettings { Name = model, SupportsCompletions = true, SupportsEmbeddings = true });
            return provider;
        }

        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            Http.Dispose();
            Service.Dispose();
            LocalInferenceTunnelBridge.Clear();
            _Home.Dispose();
        }
    }
}
