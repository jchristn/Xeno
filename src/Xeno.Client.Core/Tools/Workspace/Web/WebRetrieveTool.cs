namespace Xeno.Client.Tools.Workspace.Web
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// web_retrieve, ported from Mux without the headless browser: fetches a URL over HTTP(S) and returns its title and
    /// visible text (HTML converted to plain text, scripts not run), the final URL after redirects, the status, and
    /// optionally the HTML. Loopback, link-local, and private network addresses are refused, including after DNS
    /// resolution and on every redirect, unless <see cref="ClientChatSettings.WebRetrieveAllowPrivateNetworks"/> is on.
    /// Downloads are capped. Fetched content is never logged.
    /// </summary>
    public sealed class WebRetrieveTool : IWorkspaceTool
    {
        #region Public-Members

        /// <summary>
        /// Tool name.
        /// </summary>
        public const string ToolName = "web_retrieve";

        /// <summary>
        /// Most bytes downloaded from one URL (5 MB); the rest is not read.
        /// </summary>
        public const int MaxDownloadBytes = 5 * 1024 * 1024;

        /// <summary>
        /// Most redirects followed.
        /// </summary>
        public const int MaxRedirects = 5;

        /// <inheritdoc />
        public string Name
        {
            get { return ToolName; }
        }

        /// <inheritdoc />
        public string Description
        {
            get
            {
                return "Retrieves a URL over HTTP or HTTPS and returns its text (HTML converted to plain text), title, final URL, status, and optional HTML. "
                    + "Scripts are not run, so pages that build their content with JavaScript may return little text. Loopback and private network addresses are refused.";
            }
        }

        /// <inheritdoc />
        public ChatToolSchemaDto Schema
        {
            get
            {
                return WorkspaceSchema.Object(
                    WorkspaceSchema.Text("url", "The absolute HTTP or HTTPS URL to retrieve."),
                    WorkspaceSchema.Integer("timeout_ms", "Request timeout in milliseconds. Defaults to 30000."),
                    WorkspaceSchema.Integer("max_content_chars", "Maximum number of characters of text and HTML to return. Defaults to " + DefaultMaxContentChars + "; maximum is " + MaxContentCharsLimit + "."),
                    WorkspaceSchema.Bool("include_html", "Whether to include the document HTML in addition to the text. Defaults to false."),
                    "url");
            }
        }

        /// <inheritdoc />
        public bool Mutating
        {
            get { return false; }
        }

        #endregion

        #region Private-Members

        private const int DefaultTimeoutMs = 30000;
        private const int MaxTimeoutMs = 300000;
        private const int DefaultMaxContentChars = 60000;
        private const int MaxContentCharsLimit = 500000;

        private readonly WebHttp _Http;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the tool.
        /// </summary>
        /// <param name="handler">HTTP handler for tests, or null for the real network.</param>
        public WebRetrieveTool(HttpMessageHandler? handler = null) : this(new WebHttp(handler))
        {
        }

        internal WebRetrieveTool(WebHttp http)
        {
            _Http = http ?? throw new ArgumentNullException(nameof(http));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<object> InvokeAsync(string arguments, WorkspaceToolContext context, CancellationToken token)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            WebRetrieveArguments args = WorkspaceJson.Read<WebRetrieveArguments>(arguments);
            Uri uri = ParseHttpUri(args.Url, "Required parameter 'url' must be an absolute HTTP or HTTPS URL.");
            int timeoutMs = Math.Clamp(args.TimeoutMs ?? DefaultTimeoutMs, 1000, MaxTimeoutMs);
            int maxContentChars = Math.Clamp(args.MaxContentChars ?? DefaultMaxContentChars, 1, MaxContentCharsLimit);
            bool allowPrivate = context.Settings.WebRetrieveAllowPrivateNetworks;
            HttpClient client = _Http.ForFetch(allowPrivate);

            using (CancellationTokenSource timeout = new CancellationTokenSource(timeoutMs))
            using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token))
            {
                try
                {
                    return await FetchAsync(client, uri, allowPrivate, maxContentChars, args.IncludeHtml, linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested && !token.IsCancellationRequested)
                {
                    throw new TimeoutException("Retrieving " + uri.AbsoluteUri + " took longer than " + timeoutMs + " ms.");
                }
                catch (HttpRequestException ex) when (FindRefusal(ex) != null)
                {
                    throw FindRefusal(ex)!;
                }
                catch (HttpRequestException ex)
                {
                    throw new InvalidOperationException("Retrieving " + uri.AbsoluteUri + " failed: " + ex.Message);
                }
            }
        }

        #endregion

        #region Private-Methods

        private static async Task<WebRetrieveResult> FetchAsync(HttpClient client, Uri uri, bool allowPrivate, int maxContentChars, bool includeHtml, CancellationToken token)
        {
            Uri current = uri;
            for (int hop = 0; ; hop++)
            {
                WebAddressPolicy.CheckHost(current, allowPrivate);
                using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, current))
                {
                    request.Headers.UserAgent.ParseAdd("xeno-web-retrieve/1.0");
                    request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,text/plain;q=0.9,*/*;q=0.8");
                    using (HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
                    {
                        int status = (int)response.StatusCode;
                        if (IsRedirect(response.StatusCode) && response.Headers.Location != null)
                        {
                            if (hop >= MaxRedirects) throw new InvalidOperationException("Retrieving " + uri.AbsoluteUri + " was redirected more than " + MaxRedirects + " times.");
                            Uri next = response.Headers.Location.IsAbsoluteUri ? response.Headers.Location : new Uri(current, response.Headers.Location);
                            current = ParseHttpUri(next.AbsoluteUri, "A redirect led to '" + next + "', which is not an HTTP or HTTPS URL.");
                            continue;
                        }

                        string? mediaType = response.Content.Headers.ContentType?.MediaType;
                        string? contentType = response.Content.Headers.ContentType?.ToString();
                        if (!IsText(mediaType)) throw new InvalidOperationException("web_retrieve returns text, and " + current.AbsoluteUri + " is " + (contentType ?? "unknown content") + ".");
                        DownloadedText body = await ReadBodyAsync(response, token).ConfigureAwait(false);
                        bool isHtml = IsHtml(mediaType, body.Text);
                        string text = isHtml ? HtmlText.ToText(body.Text) : body.Text;
                        WebRetrieveResult result = new WebRetrieveResult
                        {
                            Url = uri.AbsoluteUri,
                            FinalUrl = current.AbsoluteUri,
                            Title = isHtml ? HtmlText.Title(body.Text) : String.Empty,
                            Status = status,
                            ContentType = contentType,
                            Text = Truncate(text, maxContentChars),
                            TextTruncated = text.Length > maxContentChars || body.Truncated
                        };

                        if (includeHtml)
                        {
                            result.Html = Truncate(body.Text, maxContentChars);
                            result.HtmlTruncated = body.Text.Length > maxContentChars || body.Truncated;
                        }

                        return result;
                    }
                }
            }
        }

        private static async Task<DownloadedText> ReadBodyAsync(HttpResponseMessage response, CancellationToken token)
        {
            using (Stream stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            using (MemoryStream buffer = new MemoryStream())
            {
                byte[] chunk = new byte[81920];
                bool truncated = false;
                while (true)
                {
                    int read = await stream.ReadAsync(chunk, 0, chunk.Length, token).ConfigureAwait(false);
                    if (read <= 0) break;
                    int room = MaxDownloadBytes - (int)buffer.Length;
                    if (read > room)
                    {
                        buffer.Write(chunk, 0, room);
                        truncated = true;
                        break;
                    }

                    buffer.Write(chunk, 0, read);
                }

                Encoding encoding = ResolveEncoding(response.Content.Headers.ContentType);
                return new DownloadedText { Text = encoding.GetString(buffer.GetBuffer(), 0, (int)buffer.Length), Truncated = truncated };
            }
        }

        private static Encoding ResolveEncoding(MediaTypeHeaderValue? contentType)
        {
            string? charset = contentType?.CharSet?.Trim('"', ' ');
            if (String.IsNullOrWhiteSpace(charset)) return new UTF8Encoding(false);
            try
            {
                return Encoding.GetEncoding(charset);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                return new UTF8Encoding(false);
            }
        }

        private static Uri ParseHttpUri(string? value, string error)
        {
            string raw = (value ?? String.Empty).Trim();
            if (!Uri.TryCreate(raw, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException(error);
            }

            return uri;
        }

        private static bool IsRedirect(HttpStatusCode code)
        {
            int value = (int)code;
            return value == 301 || value == 302 || value == 303 || value == 307 || value == 308;
        }

        private static bool IsText(string? mediaType)
        {
            if (String.IsNullOrWhiteSpace(mediaType)) return true;
            string type = mediaType.ToLowerInvariant();
            return type.StartsWith("text/", StringComparison.Ordinal)
                || type.EndsWith("+xml", StringComparison.Ordinal)
                || type.EndsWith("+json", StringComparison.Ordinal)
                || type == "application/json"
                || type == "application/xml"
                || type == "application/javascript"
                || type == "application/x-javascript"
                || type == "application/ecmascript";
        }

        private static bool IsHtml(string? mediaType, string body)
        {
            if (!String.IsNullOrWhiteSpace(mediaType))
            {
                string type = mediaType.ToLowerInvariant();
                return type == "text/html" || type == "application/xhtml+xml";
            }

            string start = body.TrimStart();
            return start.StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase) || start.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
        }

        private static string Truncate(string value, int maxChars)
        {
            return value.Length <= maxChars ? value : value.Substring(0, maxChars);
        }

        private static UnauthorizedAccessException? FindRefusal(Exception ex)
        {
            Exception? current = ex;
            while (current != null)
            {
                if (current is UnauthorizedAccessException refusal) return refusal;
                current = current.InnerException;
            }

            return null;
        }

        #endregion

        #region Private-Classes

        private sealed class DownloadedText
        {
            public string Text { get; set; } = String.Empty;

            public bool Truncated { get; set; } = false;
        }

        #endregion
    }
}
