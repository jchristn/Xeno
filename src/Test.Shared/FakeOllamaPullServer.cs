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

    internal sealed class FakeOllamaPullServer : IDisposable
    {
        private readonly HttpListener _Listener = new HttpListener();
        private readonly CancellationTokenSource _Cancellation = new CancellationTokenSource();
        private readonly Task _ListenTask;
        private bool _Disposed;

        internal FakeOllamaPullServer()
        {
            Port = GetFreePort();
            BaseUrl = "http://127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture);
            _Listener.Prefixes.Add(BaseUrl + "/");
            _Listener.Start();
            _ListenTask = Task.Run(async () => await ListenAsync(_Cancellation.Token).ConfigureAwait(false));
        }

        internal string BaseUrl { get; }

        internal int Port { get; }

        internal int PullCount { get; private set; }

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

        private async Task ListenAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _Listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (HttpListenerException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                _ = Task.Run(async () => await HandleAsync(context).ConfigureAwait(false), token);
            }
        }

        private async Task HandleAsync(HttpListenerContext context)
        {
            string path = context.Request.Url?.AbsolutePath ?? String.Empty;
            if (String.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase) &&
                String.Equals(path, "/api/pull", StringComparison.OrdinalIgnoreCase))
            {
                PullCount++;
                await SendPullStreamAsync(context).ConfigureAwait(false);
                return;
            }

            if (String.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase) &&
                String.Equals(path, "/api/version", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 200;
                await SendAsync(context, "{\"version\":\"0.0.0-test\"}", "application/json").ConfigureAwait(false);
                return;
            }

            context.Response.StatusCode = 404;
            await SendAsync(context, "{\"error\":\"not found\"}", "application/json").ConfigureAwait(false);
        }

        private static async Task SendPullStreamAsync(HttpListenerContext context)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/x-ndjson";
            await SendAsync(context, "{\"status\":\"pulling manifest\",\"completed\":1,\"total\":2}\n{\"status\":\"success\"}\n", "application/x-ndjson").ConfigureAwait(false);
        }

        private static async Task SendAsync(HttpListenerContext context, string body, string contentType)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            context.Response.OutputStream.Close();
        }

        private static int GetFreePort()
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
    }
}
