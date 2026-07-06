namespace Xeno.Server
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Models.Telemetry;

    /// <summary>
    /// Pending response state for server-requested client telemetry.
    /// </summary>
    internal sealed class PendingTelemetryResponse
    {
        /// <summary>
        /// Host telemetry response task.
        /// </summary>
        public TaskCompletionSource<ServerClientTelemetrySampleRecord?> Host { get; } = new TaskCompletionSource<ServerClientTelemetrySampleRecord?>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Provider telemetry response task.
        /// </summary>
        public TaskCompletionSource<IReadOnlyList<ServerProviderTelemetrySampleRecord>> Provider { get; } = new TaskCompletionSource<IReadOnlyList<ServerProviderTelemetrySampleRecord>>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Host telemetry error task.
        /// </summary>
        public TaskCompletionSource<ClientTelemetryErrorMessage> HostError { get; } = new TaskCompletionSource<ClientTelemetryErrorMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Provider telemetry error task.
        /// </summary>
        public TaskCompletionSource<ClientTelemetryErrorMessage> ProviderError { get; } = new TaskCompletionSource<ClientTelemetryErrorMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Set host telemetry result.
        /// </summary>
        /// <param name="record">Persisted host telemetry sample.</param>
        public void SetHost(ServerClientTelemetrySampleRecord? record)
        {
            Host.TrySetResult(record);
        }

        /// <summary>
        /// Set provider telemetry result.
        /// </summary>
        /// <param name="records">Persisted provider telemetry samples.</param>
        public void SetProvider(IReadOnlyList<ServerProviderTelemetrySampleRecord> records)
        {
            Provider.TrySetResult(records);
        }

        /// <summary>
        /// Set telemetry error result.
        /// </summary>
        /// <param name="message">Telemetry error frame.</param>
        public void SetError(ClientTelemetryErrorMessage message)
        {
            if (message.Type == "client.providerTelemetry.error") ProviderError.TrySetResult(message);
            else HostError.TrySetResult(message);
        }

        /// <summary>
        /// Cancel pending waiters.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public void Cancel(CancellationToken token)
        {
            Host.TrySetCanceled(token);
            Provider.TrySetCanceled(token);
            HostError.TrySetCanceled(token);
            ProviderError.TrySetCanceled(token);
        }
    }
}
