namespace Xeno.Server
{
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Xeno.Core.Models;

    /// <summary>
    /// Pending response state for a dashboard-triggered model performance test.
    /// </summary>
    internal sealed class PendingPerformanceTestResponse
    {
        /// <summary>
        /// Completed performance test response task.
        /// </summary>
        public TaskCompletionSource<ClientPerformanceTestResponseMessage> Response { get; } = new TaskCompletionSource<ClientPerformanceTestResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Progress events emitted before the final performance test response.
        /// </summary>
        public Channel<ClientPerformanceTestProgressMessage> Progress { get; } = Channel.CreateUnbounded<ClientPerformanceTestProgressMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        /// <summary>
        /// Complete the pending response.
        /// </summary>
        /// <param name="message">Performance test response.</param>
        public void SetResponse(ClientPerformanceTestResponseMessage message)
        {
            Response.TrySetResult(message);
        }

        /// <summary>
        /// Add a progress message.
        /// </summary>
        /// <param name="message">Progress message.</param>
        public void AddProgress(ClientPerformanceTestProgressMessage message)
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
        /// Cancel the pending response.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public void Cancel(CancellationToken token)
        {
            Response.TrySetCanceled(token);
            Progress.Writer.TryComplete();
        }
    }
}
