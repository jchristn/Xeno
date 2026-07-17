namespace Xeno.Client.LocalInference
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models;

    /// <summary>
    /// Coordinates localhost inference requests with the active Xeno websocket session.
    /// </summary>
    public static class LocalInferenceTunnelBridge
    {
        private static readonly ConcurrentDictionary<string, LocalInferencePendingResponse> _Pending = new ConcurrentDictionary<string, LocalInferencePendingResponse>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _SyncRoot = new object();
        private static Func<object, CancellationToken, Task>? _SendAsync;
        private static ClientSettings? _Settings;

        /// <summary>
        /// Whether a registered websocket session is available for local inference proxying.
        /// </summary>
        public static bool IsAvailable
        {
            get
            {
                lock (_SyncRoot)
                {
                    return _SendAsync != null;
                }
            }
        }

        /// <summary>
        /// Register the active websocket sender.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="sendAsync">Websocket send delegate.</param>
        public static void Register(ClientSettings settings, Func<object, CancellationToken, Task> sendAsync)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(sendAsync);
            lock (_SyncRoot)
            {
                _Settings = settings;
                _SendAsync = sendAsync;
            }
        }

        /// <summary>
        /// Clear the active websocket sender and cancel pending local inference requests.
        /// </summary>
        public static void Clear()
        {
            lock (_SyncRoot)
            {
                _Settings = null;
                _SendAsync = null;
            }

            foreach (KeyValuePair<string, LocalInferencePendingResponse> item in _Pending)
            {
                item.Value.Cancel(CancellationToken.None);
            }

            _Pending.Clear();
        }

        /// <summary>
        /// Send a local inference request through the active websocket session.
        /// </summary>
        /// <param name="message">Request message.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Pending response state.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the websocket session is not available.</exception>
        public static async Task<LocalInferencePendingResponse> SendRequestAsync(ClientLocalInferenceRequestMessage message, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(message);
            token.ThrowIfCancellationRequested();
            Func<object, CancellationToken, Task>? sender;
            ClientSettings? settings;
            lock (_SyncRoot)
            {
                sender = _SendAsync;
                settings = _Settings;
            }

            if (sender == null || settings == null) throw new InvalidOperationException("The Xeno websocket connection is not available.");
            if (String.IsNullOrWhiteSpace(message.RequestId)) message.RequestId = Guid.NewGuid().ToString("N");
            message.Type = "client.localInference.request";
            message.ClientId = settings.ClientId ?? String.Empty;
            message.TenantId = settings.TenantId;
            LocalInferencePendingResponse pending = new LocalInferencePendingResponse();
            if (!_Pending.TryAdd(message.RequestId, pending)) throw new InvalidOperationException("A local inference request with the same ID is already pending.");

            try
            {
                await sender(message, token).ConfigureAwait(false);
                return pending;
            }
            catch
            {
                _Pending.TryRemove(message.RequestId, out LocalInferencePendingResponse? _);
                pending.Cancel(token);
                throw;
            }
        }

        /// <summary>
        /// Send a local inference cancellation through the active websocket session.
        /// </summary>
        /// <param name="requestId">Request ID.</param>
        /// <param name="reason">Cancellation reason.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static async Task SendCancelAsync(string requestId, string reason, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(requestId)) return;
            Func<object, CancellationToken, Task>? sender;
            ClientSettings? settings;
            lock (_SyncRoot)
            {
                sender = _SendAsync;
                settings = _Settings;
            }

            if (sender == null || settings == null) return;
            await sender(new ClientLocalInferenceCancelMessage
            {
                Type = "client.localInference.cancel",
                RequestId = requestId,
                ClientId = settings.ClientId ?? String.Empty,
                TenantId = settings.TenantId,
                Reason = String.IsNullOrWhiteSpace(reason) ? "CallerDisconnected" : reason
            }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Accept a server response message.
        /// </summary>
        /// <param name="message">Response message.</param>
        public static void AcceptResponse(ServerLocalInferenceResponseMessage message)
        {
            if (message == null || String.IsNullOrWhiteSpace(message.RequestId)) return;
            if (!_Pending.TryGetValue(message.RequestId, out LocalInferencePendingResponse? pending)) return;
            if (String.Equals(message.Type, "server.localInference.response.start", StringComparison.OrdinalIgnoreCase))
            {
                pending.SetStart(message);
                return;
            }

            pending.SetFinal(message);
            _Pending.TryRemove(message.RequestId, out LocalInferencePendingResponse? _);
        }

        /// <summary>
        /// Accept a server chunk message.
        /// </summary>
        /// <param name="message">Chunk message.</param>
        public static void AcceptChunk(ServerLocalInferenceChunkMessage message)
        {
            if (message == null || String.IsNullOrWhiteSpace(message.RequestId)) return;
            if (!_Pending.TryGetValue(message.RequestId, out LocalInferencePendingResponse? pending)) return;
            pending.AddChunk(message);
        }
    }
}
