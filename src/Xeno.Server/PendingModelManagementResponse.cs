namespace Xeno.Server
{
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Xeno.Core.Models;

    /// <summary>
    /// Pending response state for a dashboard-triggered client model management operation.
    /// </summary>
    internal sealed class PendingModelManagementResponse
    {
        /// <summary>
        /// Completed model management response task.
        /// </summary>
        public TaskCompletionSource<ClientModelManagementResponseMessage> Response { get; } = new TaskCompletionSource<ClientModelManagementResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Progress events emitted before the final response.
        /// </summary>
        public Channel<ClientModelManagementProgressMessage> Progress { get; } = Channel.CreateUnbounded<ClientModelManagementProgressMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        /// <summary>
        /// Complete the pending response.
        /// </summary>
        /// <param name="message">Model management response.</param>
        public void SetResponse(ClientModelManagementResponseMessage message)
        {
            Response.TrySetResult(message);
            Progress.Writer.TryComplete();
        }

        /// <summary>
        /// Add a progress message.
        /// </summary>
        /// <param name="message">Progress message.</param>
        public void AddProgress(ClientModelManagementProgressMessage message)
        {
            Progress.Writer.TryWrite(message);
        }

        /// <summary>
        /// Complete the progress channel.
        /// </summary>
        public void CompleteProgress()
        {
            Progress.Writer.TryComplete();
        }

        /// <summary>
        /// Cancel the pending operation.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public void Cancel(CancellationToken token)
        {
            Response.TrySetCanceled(token);
            Progress.Writer.TryComplete();
        }
    }
}
