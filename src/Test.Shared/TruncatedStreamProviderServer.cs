namespace Test.Shared
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Raw-socket provider that starts a chunked streaming response, sends one chunk, and drops the connection without
    /// the terminating chunk, the way a crashed provider process does. HttpListener cannot do this: its Abort ends the
    /// chunked body cleanly on some platforms. Answers every other request with an OpenAI model list.
    /// </summary>
    internal sealed class TruncatedStreamProviderServer : IDisposable
    {
        private readonly TcpListener _Listener = new TcpListener(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _Cancellation = new CancellationTokenSource();
        private readonly Task _AcceptTask;
        private readonly string _StreamPath;
        private readonly string _FirstChunk;
        private bool _Disposed;

        internal TruncatedStreamProviderServer(string streamPath, string firstChunk, string model)
        {
            _StreamPath = streamPath;
            _FirstChunk = firstChunk;
            Model = model;
            _Listener.Start();
            Port = ((IPEndPoint)_Listener.LocalEndpoint).Port;
            BaseUrl = "http://127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture);
            _AcceptTask = Task.Run(async () => await AcceptAsync(_Cancellation.Token).ConfigureAwait(false));
        }

        internal string BaseUrl { get; }

        internal int Port { get; }

        internal string Model { get; }

        internal int StreamRequests;

        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            _Cancellation.Cancel();
            _Listener.Stop();
            try
            {
                _AcceptTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException)
            {
            }

            _Cancellation.Dispose();
        }

        private async Task AcceptAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _Listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is SocketException || ex is ObjectDisposedException || ex is OperationCanceledException || ex is InvalidOperationException)
                {
                    return;
                }

                _ = Task.Run(async () => await HandleAsync(client).ConfigureAwait(false));
            }
        }

        private async Task HandleAsync(TcpClient client)
        {
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    string head = await ReadHeadAsync(stream).ConfigureAwait(false);
                    string requestLine = head.Split("\r\n")[0];
                    int contentLength = 0;
                    foreach (string line in head.Split("\r\n"))
                    {
                        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) Int32.TryParse(line.Substring(15).Trim(), out contentLength);
                    }

                    byte[] discard = new byte[contentLength];
                    int offset = 0;
                    while (offset < contentLength)
                    {
                        int read = await stream.ReadAsync(discard, offset, contentLength - offset).ConfigureAwait(false);
                        if (read <= 0) break;
                        offset += read;
                    }

                    if (requestLine.Contains(" " + _StreamPath + " ", StringComparison.Ordinal))
                    {
                        Interlocked.Increment(ref StreamRequests);
                        byte[] chunk = Encoding.UTF8.GetBytes(_FirstChunk);
                        string response = "HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nTransfer-Encoding: chunked\r\nConnection: close\r\n\r\n"
                            + chunk.Length.ToString("X", CultureInfo.InvariantCulture) + "\r\n" + _FirstChunk + "\r\n";
                        byte[] bytes = Encoding.UTF8.GetBytes(response);
                        await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                        await stream.FlushAsync().ConfigureAwait(false);
                        await Task.Delay(50).ConfigureAwait(false);
                        client.Client.LingerState = new LingerOption(true, 0);
                        return;
                    }

                    string body = "{\"object\":\"list\",\"data\":[{\"id\":\"" + Model + "\",\"object\":\"model\"}]}";
                    string plain = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + Encoding.UTF8.GetByteCount(body).ToString(CultureInfo.InvariantCulture) + "\r\nConnection: close\r\n\r\n" + body;
                    byte[] plainBytes = Encoding.UTF8.GetBytes(plain);
                    await stream.WriteAsync(plainBytes, 0, plainBytes.Length).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is SocketException || ex is ObjectDisposedException)
            {
            }
        }

        private static async Task<string> ReadHeadAsync(NetworkStream stream)
        {
            StringBuilder head = new StringBuilder();
            byte[] one = new byte[1];
            while (!head.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
            {
                int read = await stream.ReadAsync(one, 0, 1).ConfigureAwait(false);
                if (read <= 0) break;
                head.Append((char)one[0]);
            }

            return head.ToString();
        }
    }
}
