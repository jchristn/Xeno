namespace Xeno.Core.Chat
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// Sends an OpenAI-shaped chat completion request to a model and hands back the response bytes as they arrive.
    /// The client process implements it with the local inference router, so chat follows the same routes, limits,
    /// and fallback rules as the local inference listener.
    /// </summary>
    public interface IChatCompletionTransport
    {
        /// <summary>
        /// Send a request.
        /// </summary>
        /// <param name="request">Request.</param>
        /// <param name="onBytes">Called with each piece of the response body: the bytes and how many of them to read.</param>
        /// <param name="token">Cancellation token; cancels the model call.</param>
        /// <returns>Where the request went and how it ended.</returns>
        Task<ChatTransportResult> SendAsync(ChatCompletionRequestDto request, Func<byte[], int, Task> onBytes, CancellationToken token);
    }
}
