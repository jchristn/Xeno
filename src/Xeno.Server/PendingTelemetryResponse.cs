namespace Xeno.Server
{
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
        public TaskCompletionSource<ClientTelemetrySnapshotMessage> Host { get; } = new TaskCompletionSource<ClientTelemetrySnapshotMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Provider telemetry response task.
        /// </summary>
        public TaskCompletionSource<ClientProviderTelemetrySnapshotMessage> Provider { get; } = new TaskCompletionSource<ClientProviderTelemetrySnapshotMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

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
        /// <param name="message">Host telemetry snapshot frame.</param>
        public void SetHost(ClientTelemetrySnapshotMessage message)
        {
            Host.TrySetResult(message);
        }

        /// <summary>
        /// Set provider telemetry result.
        /// </summary>
        /// <param name="message">Provider telemetry snapshot frame.</param>
        public void SetProvider(ClientProviderTelemetrySnapshotMessage message)
        {
            Provider.TrySetResult(message);
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
