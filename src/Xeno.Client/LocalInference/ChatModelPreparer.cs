namespace Xeno.Client.LocalInference
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Chat;
    using Xeno.Client.Routing;
    using Xeno.Core.Chat;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Gets a model ready for chat: finds where it would run and, for a model on a local Ollama, reads its capabilities
    /// (tools, thinking) and loads it into memory so the first message does not wait for the load. Other local
    /// providers keep their models loaded; network models load on the machine that serves them when first asked.
    /// Thread safe.
    /// </summary>
    public sealed class ChatModelPreparer : IDisposable
    {
        #region Private-Members

        private readonly Func<ClientSettings> _Settings;
        private readonly Func<IReadOnlyCollection<LocalBackendStatus>?> _Backends;
        private readonly LocalProviderProxy _Proxy;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the preparer.
        /// </summary>
        /// <param name="settings">Returns the current client settings.</param>
        /// <param name="providerHandler">HTTP handler for provider calls, for tests; null for the default.</param>
        /// <param name="backends">Returns provider health, or null to use the running listener's monitor.</param>
        /// <exception cref="ArgumentNullException">settings is null.</exception>
        public ChatModelPreparer(Func<ClientSettings> settings, HttpMessageHandler? providerHandler = null, Func<IReadOnlyCollection<LocalBackendStatus>?>? backends = null)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Proxy = new LocalProviderProxy(providerHandler);
            _Backends = backends ?? (() => LocalInferenceServerRuntime.Backends?.Statuses);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Prepare a model.
        /// </summary>
        /// <param name="model">Model name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Readiness.</returns>
        /// <exception cref="ArgumentException">model is empty.</exception>
        public async Task<ChatModelReadiness> PrepareAsync(string model, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(model)) throw new ArgumentException("Choose a model.");
            ClientSettings settings = _Settings();
            LocalInferenceRouteDecision decision = LocalInferenceRouter.Resolve(settings, _Backends(), model, LocalInferenceApiShapeEnum.OpenAi, false);
            ChatModelReadiness readiness = new ChatModelReadiness { Model = model, Route = decision.Target == LocalInferenceRouteTargetKindEnum.Network ? "network" : decision.RouteHeader };
            if (decision.Target == LocalInferenceRouteTargetKindEnum.Network)
            {
                readiness.Message = "Xeno network models load on the machine that serves them when first asked.";
                return readiness;
            }

            if (decision.Provider == null)
            {
                readiness.Route = "unavailable";
                readiness.Message = "Nothing on this machine serves this model, and routing does not allow the Xeno network.";
                return readiness;
            }

            ClientRunnerSettings provider = decision.Provider;
            if (!String.Equals(provider.ApiType, "Ollama", StringComparison.OrdinalIgnoreCase))
            {
                readiness.Loaded = true;
                readiness.SupportsTools = true;
                readiness.Message = "Served by " + provider.Name + ", which keeps its models loaded.";
                return readiness;
            }

            int timeoutMs = Math.Max(1000, settings.LocalInferenceServer.RequestTimeoutMs);
            ConcurrencyLease? lease = await ProviderConcurrencyGate.Shared.AcquireLocalAsync(settings, provider, model, settings.LocalInferenceServer.LocalQueueTimeoutMs, token).ConfigureAwait(false);
            if (lease == null)
            {
                readiness.Message = "Provider " + provider.Name + " is busy; the model loads with the first message.";
                return readiness;
            }

            using (lease)
            {
                OllamaShowCapabilitiesDto? show = await PostAsync<OllamaShowCapabilitiesDto>(provider, "/api/show", JsonUtil.Serialize(new OllamaShowRequestDto { Model = model }), 15000, token).ConfigureAwait(false);
                List<string> capabilities = show?.Capabilities ?? new List<string>();
                readiness.SupportsTools = show != null && show.UsesTools;
                readiness.SupportsThinking = capabilities.Any(item => String.Equals(item, "thinking", StringComparison.OrdinalIgnoreCase));
                Stopwatch clock = Stopwatch.StartNew();
                LocalProviderProxyResult loaded = await _Proxy.SendAsync(provider, "POST", "/api/generate", JsonUtil.Serialize(new OllamaLoadRequestDto { Model = model }), timeoutMs, (bytes, count) => Task.CompletedTask, token).ConfigureAwait(false);
                readiness.LoadMs = clock.Elapsed.TotalMilliseconds;
                readiness.Loaded = loaded.Completed && loaded.StatusCode.HasValue && loaded.StatusCode.Value < 400;
                readiness.Message = readiness.Loaded
                    ? "Loaded on " + provider.Name + " in " + Math.Round(readiness.LoadMs / 1000.0, 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + " s."
                    : "Could not load the model on " + provider.Name + (loaded.StatusCode == 404 ? ": the model is not installed there." : !String.IsNullOrWhiteSpace(loaded.Error) ? ": " + loaded.Error : (loaded.StatusCode.HasValue ? " (HTTP " + loaded.StatusCode.Value + ")." : "."));
                return readiness;
            }
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            _Proxy.Dispose();
        }

        #endregion

        #region Private-Methods

        private async Task<T?> PostAsync<T>(ClientRunnerSettings provider, string path, string body, int timeoutMs, CancellationToken token) where T : class
        {
            using (MemoryStream buffer = new MemoryStream())
            {
                LocalProviderProxyResult result = await _Proxy.SendAsync(provider, "POST", path, body, timeoutMs, (bytes, count) =>
                {
                    buffer.Write(bytes, 0, count);
                    return Task.CompletedTask;
                }, token).ConfigureAwait(false);
                if (!result.Completed || !result.StatusCode.HasValue || result.StatusCode.Value >= 400) return null;
                JsonUtil.TryDeserialize<T>(Encoding.UTF8.GetString(buffer.ToArray()), out T? value);
                return value;
            }
        }

        #endregion
    }
}
