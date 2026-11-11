namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using Xeno.Client;
    using Xeno.Client.Chat;
    using Xeno.Core.Chat;
    using Xeno.Client.LocalDashboard;
    using Xeno.Client.LocalDashboard.Database;
    using Xeno.Client.LocalInference;
    using Xeno.Client.Services;

    /// <summary>
    /// A running local chat API for the current client home: a fake Ollama answering every chat with fixed text, the
    /// local dashboard service with chat, and a published local-api.json, so front ends can chat as with a real client.
    /// </summary>
    internal sealed class ChatApiFixture : IDisposable
    {
        private readonly FakeProviderServer _Provider;
        private readonly RoutedChatTransport _Transport;
        private readonly LocalDashboardService _Service;
        private bool _Disposed;

        /// <summary>
        /// Start the fixture.
        /// </summary>
        /// <param name="answer">Text every chat answer streams.</param>
        internal ChatApiFixture(string answer)
        {
            string chunk = "data: {\"choices\":[{\"delta\":{\"content\":" + Xeno.Core.Serialization.JsonUtil.Serialize(answer) + "},\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":2,\"completion_tokens\":2}}\n\n";
            _Provider = new FakeProviderServer(request => new FakeProviderResponse { ContentType = "text/event-stream", Chunks = new List<string> { chunk, "data: [DONE]\n\n" } });
            Settings = new ClientSettings { ClientId = "xcl_fixture", Name = "fixture", Providers = new List<ClientRunnerSettings> { ListenerRoutingHarness.Provider("box", "Ollama", _Provider.BaseUrl, "llama3.1:8b") } };
            Settings.LocalDashboard.Host = "127.0.0.1";
            Settings.LocalDashboard.Port = FakeProviderServer.GetFreePort();
            Settings.LocalDashboard.Database.Filename = Path.Combine(ClientSettings.SettingsDirectory, "local-dashboard.db");
            Settings.Save();
            LocalDashboardDatabaseDriverBase driver = LocalDashboardDatabaseDriverFactory.Create(Settings.LocalDashboard.Database);
            driver.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
            Store = new SqliteChatStore(Settings.LocalDashboard.Database);
            Store.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
            _Transport = new RoutedChatTransport(() => Settings, null, () => null);
            _Service = new LocalDashboardService(Settings, driver.RequestHistory, driver.TelemetryHistory, null, driver.ProviderTelemetryHistory, null, Store, new ChatEngine(Store, _Transport, () => Settings.Chat));
            _Service.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            LocalApiEndpoint.Publish(new LocalApiEndpoint { Url = _Service.DashboardUrl, Token = _Service.ApiToken, ProcessId = Environment.ProcessId });
        }

        /// <summary>
        /// Settings saved to the client home.
        /// </summary>
        internal ClientSettings Settings { get; }

        /// <summary>
        /// Chat store.
        /// </summary>
        internal SqliteChatStore Store { get; }

        /// <summary>
        /// Stop everything.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            LocalApiEndpoint.Withdraw(Environment.ProcessId);
            _Service.Dispose();
            _Transport.Dispose();
            _Provider.Dispose();
        }
    }
}
