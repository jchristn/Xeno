namespace Xeno.Server
{
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Xeno.Core.Models;

    /// <summary>
    /// Pending response state for a request dispatched to a connected Xeno client.
    /// </summary>
    internal sealed class PendingClientResponse
    {
        /// <summary>
        /// Stream start or legacy final response task.
        /// </summary>
        public TaskCompletionSource<ClientResponseMessage> Start { get; } = new TaskCompletionSource<ClientResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Final response task.
        /// </summary>
        public TaskCompletionSource<ClientResponseMessage> Final { get; } = new TaskCompletionSource<ClientResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Stream chunk channel.
        /// </summary>
        public Channel<ClientResponseChunkMessage> Chunks { get; } = Channel.CreateUnbounded<ClientResponseChunkMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        /// <summary>
        /// Complete the stream start task.
        /// </summary>
        /// <param name="message">Client response message.</param>
        public void SetStart(ClientResponseMessage message)
        {
            Start.TrySetResult(message);
        }

        /// <summary>
        /// Add a streamed chunk.
        /// </summary>
        /// <param name="message">Chunk message.</param>
        public void AddChunk(ClientResponseChunkMessage message)
        {
            Chunks.Writer.TryWrite(message);
        }

        /// <summary>
        /// Complete the final response task and close the chunk channel.
        /// </summary>
        /// <param name="message">Client response message.</param>
        public void SetFinal(ClientResponseMessage message)
        {
            Final.TrySetResult(message);
            Chunks.Writer.TryComplete();
        }

        /// <summary>
        /// Wait for the first client response: a stream start, or a final response from a client that does not stream.
        /// </summary>
        /// <returns>The stream start message when one was received; otherwise the final message.</returns>
        public async Task<ClientResponseMessage> WaitForFirstResponseAsync()
        {
            Task<ClientResponseMessage> completed = await Task.WhenAny(Start.Task, Final.Task).ConfigureAwait(false);

            // Client messages are processed in order, so a completed start always precedes the final message.
            // WhenAny can still observe Final first because both sources run continuations asynchronously.
            if (Start.Task.IsCompletedSuccessfully) return Start.Task.Result;
            return await completed.ConfigureAwait(false);
        }

        /// <summary>
        /// Cancel all waiters.
        /// </summary>
        /// <param name="token">Cancellation token that caused cancellation.</param>
        public void Cancel(CancellationToken token)
        {
            Start.TrySetCanceled(token);
            Final.TrySetCanceled(token);
            Chunks.Writer.TryComplete();
        }
    }
}
