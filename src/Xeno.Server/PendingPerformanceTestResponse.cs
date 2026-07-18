namespace Xeno.Server
{
    using System.Threading;
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
        /// Complete the pending response.
        /// </summary>
        /// <param name="message">Performance test response.</param>
        public void SetResponse(ClientPerformanceTestResponseMessage message)
        {
            Response.TrySetResult(message);
        }

        /// <summary>
        /// Cancel the pending response.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public void Cancel(CancellationToken token)
        {
            Response.TrySetCanceled(token);
        }
    }
}
