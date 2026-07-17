namespace Xeno.Client.LocalInference
{
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;
    using Xeno.Core.Models;

    /// <summary>
    /// Pending local inference response state.
    /// </summary>
    public sealed class LocalInferencePendingResponse
    {
        /// <summary>
        /// Stream start or legacy final response task.
        /// </summary>
        public TaskCompletionSource<ServerLocalInferenceResponseMessage> Start { get; } = new TaskCompletionSource<ServerLocalInferenceResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Final response task.
        /// </summary>
        public TaskCompletionSource<ServerLocalInferenceResponseMessage> Final { get; } = new TaskCompletionSource<ServerLocalInferenceResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Streaming chunk channel.
        /// </summary>
        public Channel<ServerLocalInferenceChunkMessage> Chunks { get; } = Channel.CreateBounded<ServerLocalInferenceChunkMessage>(new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

        /// <summary>
        /// Complete the start response.
        /// </summary>
        /// <param name="message">Response message.</param>
        public void SetStart(ServerLocalInferenceResponseMessage message)
        {
            Start.TrySetResult(message);
        }

        /// <summary>
        /// Add a stream chunk.
        /// </summary>
        /// <param name="message">Chunk message.</param>
        public void AddChunk(ServerLocalInferenceChunkMessage message)
        {
            Chunks.Writer.TryWrite(message);
        }

        /// <summary>
        /// Complete the final response and close the chunk channel.
        /// </summary>
        /// <param name="message">Response message.</param>
        public void SetFinal(ServerLocalInferenceResponseMessage message)
        {
            Final.TrySetResult(message);
            Chunks.Writer.TryComplete();
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
