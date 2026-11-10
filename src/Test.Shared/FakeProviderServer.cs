namespace Test.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Scriptable in-process stand-in for an Ollama, vLLM, or OpenAI-compatible provider, bound to 127.0.0.1 on a free port.
    /// Each test supplies a handler that decides the response for a request; the server records every request it sees.
    /// </summary>
    internal sealed class FakeProviderServer : IDisposable
    {
        private readonly HttpListener _Listener = new HttpListener();
        private readonly CancellationTokenSource _Cancellation = new CancellationTokenSource();
        private readonly Func<FakeProviderRequest, FakeProviderResponse> _Handler;
        private readonly Task _ListenTask;
        private bool _Disposed;

        internal FakeProviderServer(Func<FakeProviderRequest, FakeProviderResponse> handler)
        {
            _Handler = handler ?? throw new ArgumentNullException(nameof(handler));
            Port = GetFreePort();
            BaseUrl = "http://127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture);
            _Listener.Prefixes.Add(BaseUrl + "/");
            _Listener.Start();
            _ListenTask = Task.Run(async () => await ListenAsync(_Cancellation.Token).ConfigureAwait(false));
        }

        internal string BaseUrl { get; }

        internal int Port { get; }

        internal ConcurrentQueue<FakeProviderRequest> Requests { get; } = new ConcurrentQueue<FakeProviderRequest>();

        internal int AbortedStreams;

        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            _Cancellation.Cancel();
            _Listener.Close();
            try
            {
                _ListenTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException)
            {
            }

            _Cancellation.Dispose();
        }

        internal static int GetFreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            try
            {
                listener.Start();
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        private async Task ListenAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _Listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is HttpListenerException || ex is ObjectDisposedException || ex is InvalidOperationException)
                {
                    return;
                }

                _ = Task.Run(async () => await HandleAsync(context).ConfigureAwait(false));
            }
        }

        private async Task HandleAsync(HttpListenerContext context)
        {
            string body;
            using (StreamReader reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string? key in context.Request.Headers.AllKeys)
            {
                if (key != null) headers[key] = context.Request.Headers[key] ?? String.Empty;
            }

            FakeProviderRequest request = new FakeProviderRequest
            {
                Method = context.Request.HttpMethod,
                Path = context.Request.Url?.AbsolutePath ?? String.Empty,
                Body = body,
                Headers = headers
            };
            Requests.Enqueue(request);
            FakeProviderResponse response = _Handler(request);
            try
            {
                if (response.DelayMs > 0) await Task.Delay(response.DelayMs).ConfigureAwait(false);
                context.Response.StatusCode = response.StatusCode;
                context.Response.ContentType = response.ContentType;
                foreach (KeyValuePair<string, string> header in response.Headers)
                {
                    context.Response.Headers[header.Key] = header.Value;
                }

                if (response.Chunks.Count == 0)
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(response.Body);
                    context.Response.ContentLength64 = bytes.Length;
                    await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                    context.Response.OutputStream.Close();
                    return;
                }

                context.Response.SendChunked = true;
                for (int i = 0; i < response.Chunks.Count; i++)
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(response.Chunks[i]);
                    await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                    await context.Response.OutputStream.FlushAsync().ConfigureAwait(false);
                    if (response.AbortAfterChunks.HasValue && i + 1 >= response.AbortAfterChunks.Value)
                    {
                        context.Response.Abort();
                        return;
                    }

                    if (response.ChunkDelayMs > 0) await Task.Delay(response.ChunkDelayMs).ConfigureAwait(false);
                }

                context.Response.OutputStream.Close();
            }
            catch (Exception ex) when (ex is HttpListenerException || ex is IOException || ex is ObjectDisposedException || ex is InvalidOperationException)
            {
                Interlocked.Increment(ref AbortedStreams);
            }
        }
    }
}
