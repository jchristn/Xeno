namespace Xeno.Client.LocalInference
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Chat;
    using Xeno.Core.Chat;
    using Xeno.Core.Chat.Wire;
    using Xeno.Client.Routing;
    using Xeno.Core.Models;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Sends chat completion requests the way the local inference listener routes them: through
    /// <see cref="LocalInferenceRouter"/>, to a provider on this machine under a shared concurrency lease, or over the
    /// websocket tunnel to the Xeno network, with network fallback only when the model's route rule allows it and only
    /// before the first response byte. Works whether or not the listener itself is enabled. Thread safe.
    /// </summary>
    public sealed class RoutedChatTransport : IChatCompletionTransport, IDisposable
    {
        #region Private-Members

        private const string ChatPath = "/v1/chat/completions";
        private readonly Func<ClientSettings> _Settings;
        private readonly LocalProviderProxy _Proxy;
        private readonly Func<IReadOnlyCollection<LocalBackendStatus>?> _Backends;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the transport.
        /// </summary>
        /// <param name="settings">Returns the current client settings.</param>
        /// <param name="providerHandler">HTTP handler for provider calls, for tests; null for the default.</param>
        /// <param name="backends">Returns provider health, or null to use the running listener's monitor.</param>
        /// <exception cref="ArgumentNullException">settings is null.</exception>
        public RoutedChatTransport(Func<ClientSettings> settings, HttpMessageHandler? providerHandler = null, Func<IReadOnlyCollection<LocalBackendStatus>?>? backends = null)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Proxy = new LocalProviderProxy(providerHandler);
            _Backends = backends ?? (() => LocalInferenceServerRuntime.Backends?.Statuses);
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<ChatTransportResult> SendAsync(ChatCompletionRequestDto request, Func<byte[], int, Task> onBytes, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(onBytes);
            ClientSettings settings = _Settings();
            string body = JsonUtil.Serialize(request);
            LocalInferenceRouteDecision decision = LocalInferenceRouter.Resolve(settings, _Backends(), request.Model, LocalInferenceApiShapeEnum.OpenAi, false);
            if (decision.Target == LocalInferenceRouteTargetKindEnum.Network)
            {
                return await SendNetworkAsync(settings, request.Model, body, decision.IsFallback, onBytes, token).ConfigureAwait(false);
            }

            if (decision.Target == LocalInferenceRouteTargetKindEnum.Unavailable || decision.Provider == null)
            {
                return new ChatTransportResult
                {
                    Route = "unavailable",
                    StatusCode = decision.Reason == "localOnlyMode" ? 404 : 502,
                    FailureKind = ChatFailureKindEnum.Unavailable,
                    Error = decision.Reason == "localOnlyMode"
                        ? "Model '" + request.Model + "' is not served on this machine, and routing is set to this machine only."
                        : "The route rule for model '" + request.Model + "' names a provider that is not set up for local requests on this machine."
                };
            }

            ClientRunnerSettings provider = decision.Provider;
            ChatTransportResult result = new ChatTransportResult { Route = decision.RouteHeader, Provider = provider.Name };
            int timeoutMs = Math.Max(1000, settings.LocalInferenceServer.RequestTimeoutMs);
            ConcurrencyLease? lease = await ProviderConcurrencyGate.Shared.AcquireLocalAsync(settings, provider, request.Model, settings.LocalInferenceServer.LocalQueueTimeoutMs, token).ConfigureAwait(false);
            if (lease == null)
            {
                result.StatusCode = 429;
                result.FailureKind = ChatFailureKindEnum.Unavailable;
                result.Error = "Provider '" + provider.Name + "' is busy with as many requests as it allows. Try again shortly.";
                return result;
            }

            LocalProviderProxyResult sent;
            using (lease)
            {
                sent = await _Proxy.SendAsync(provider, "POST", ChatPath, body, timeoutMs, onBytes, token).ConfigureAwait(false);
            }

            LocalBackendHealthMonitor? monitor = LocalInferenceServerRuntime.Backends;
            if (sent.Started)
            {
                if (sent.StatusCode.HasValue && sent.StatusCode.Value < 500) monitor?.MarkSuccess(provider);
                if (!sent.Completed) monitor?.MarkFailure(provider, sent.Error ?? "Incomplete response.");
                result.StatusCode = sent.StatusCode;
                result.Completed = sent.Completed;
                result.Error = String.IsNullOrWhiteSpace(sent.Error) ? null : sent.Error;
                if (sent.TimedOut) result.FailureKind = ChatFailureKindEnum.Timeout;
                else if (!sent.Completed) result.FailureKind = ChatFailureKindEnum.Interrupted;
                return result;
            }

            monitor?.MarkFailure(provider, sent.Error ?? "Unavailable.");
            if (decision.FallbackToNetwork)
            {
                return await SendNetworkAsync(settings, request.Model, body, true, onBytes, token).ConfigureAwait(false);
            }

            result.StatusCode = sent.TimedOut ? 504 : 502;
            result.FailureKind = sent.TimedOut ? ChatFailureKindEnum.Timeout : ChatFailureKindEnum.Unavailable;
            result.Error = String.IsNullOrWhiteSpace(sent.Error) ? "Provider '" + provider.Name + "' is unavailable." : sent.Error;
            return result;
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

        private static async Task<ChatTransportResult> SendNetworkAsync(ClientSettings settings, string model, string body, bool fallback, Func<byte[], int, Task> onBytes, CancellationToken token)
        {
            ChatTransportResult result = new ChatTransportResult { Route = "network", Fallback = fallback };
            if (!LocalInferenceTunnelBridge.IsAvailable)
            {
                result.StatusCode = 502;
                result.FailureKind = ChatFailureKindEnum.Unavailable;
                result.Error = "This client is not connected to the Xeno network, so network models are unavailable. Start the client and sign in.";
                return result;
            }

            ClientLocalInferenceRequestMessage message = new ClientLocalInferenceRequestMessage
            {
                RequestId = Guid.NewGuid().ToString("N"),
                Method = "POST",
                RelativePath = ChatPath,
                ProviderFamily = "openai",
                Model = model,
                Embeddings = false,
                Stream = true,
                RequestorIp = "127.0.0.1",
                Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = "application/json" },
                Body = body,
                ExcludeRequestingClient = fallback
            };

            int timeoutMs = Math.Max(1000, settings.LocalInferenceServer.RequestTimeoutMs);
            LocalInferencePendingResponse pending;
            try
            {
                pending = await LocalInferenceTunnelBridge.SendRequestAsync(message, token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is WebSocketException)
            {
                result.StatusCode = 502;
                result.FailureKind = ChatFailureKindEnum.Unavailable;
                result.Error = "Could not reach the Xeno network: " + ex.Message;
                return result;
            }

            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
                using (timeout.Token.Register(delegate
                {
                    LocalInferenceTunnelBridge.CancelPending(message.RequestId, timeout.Token);
                    _ = TrySendCancelAsync(message.RequestId, token.IsCancellationRequested ? "CallerDisconnected" : "RequestTimeout");
                }))
                {
                    try
                    {
                        Task<ServerLocalInferenceResponseMessage> first = await Task.WhenAny(pending.Start.Task, pending.Final.Task).ConfigureAwait(false);
                        ServerLocalInferenceResponseMessage start = await first.ConfigureAwait(false);
                        result.StatusCode = start.StatusCode ?? (start.Success ? 200 : 502);
                        if (String.Equals(start.Type, "server.localInference.response.start", StringComparison.OrdinalIgnoreCase))
                        {
                            while (await pending.Chunks.Reader.WaitToReadAsync(timeout.Token).ConfigureAwait(false))
                            {
                                while (pending.Chunks.Reader.TryRead(out ServerLocalInferenceChunkMessage? chunk))
                                {
                                    if (chunk == null) continue;
                                    byte[] bytes = chunk.Decode();
                                    if (bytes.Length > 0) await onBytes(bytes, bytes.Length).ConfigureAwait(false);
                                }
                            }

                            ServerLocalInferenceResponseMessage final = await pending.Final.Task.ConfigureAwait(false);
                            result.Completed = final.Success;
                            if (!final.Success) result.Error = String.IsNullOrWhiteSpace(final.Error) ? "The Xeno network ended the response early." : final.Error;
                            return result;
                        }

                        byte[] whole = Encoding.UTF8.GetBytes(start.Body ?? String.Empty);
                        if (whole.Length > 0) await onBytes(whole, whole.Length).ConfigureAwait(false);
                        result.Completed = start.Success;
                        if (!start.Success && String.IsNullOrWhiteSpace(start.Body)) result.Error = String.IsNullOrWhiteSpace(start.Error) ? "The Xeno network could not answer." : start.Error;
                        return result;
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested)
                    {
                        result.StatusCode ??= 504;
                        result.FailureKind = ChatFailureKindEnum.Timeout;
                        result.Error = "The Xeno network did not answer within " + Seconds(timeoutMs) + ".";
                        return result;
                    }
                }
            }
        }

        private static string Seconds(int milliseconds)
        {
            double seconds = Math.Round(milliseconds / 1000.0, milliseconds % 1000 == 0 ? 0 : 1);
            return seconds.ToString(System.Globalization.CultureInfo.InvariantCulture) + (seconds == 1 ? " second" : " seconds");
        }

        private static async Task TrySendCancelAsync(string requestId, string reason)
        {
            try
            {
                await LocalInferenceTunnelBridge.SendCancelAsync(requestId, reason, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is WebSocketException || ex is OperationCanceledException)
            {
            }
        }

        #endregion
    }
}
