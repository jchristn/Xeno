namespace Xeno.Client.LocalDashboard
{
    using System;
    using System.IO;
    using System.Reflection;

    /// <summary>
    /// Resolves embedded local dashboard assets.
    /// </summary>
    public static class LocalDashboardAssetService
    {
        /// <summary>
        /// Try to load an embedded local dashboard asset.
        /// </summary>
        /// <param name="path">Request path.</param>
        /// <param name="token">Local dashboard token.</param>
        /// <param name="asset">Resolved asset.</param>
        /// <returns>True if found.</returns>
        public static bool TryGetAsset(string path, string token, out LocalDashboardAsset asset)
        {
            asset = new LocalDashboardAsset();
            string normalized = NormalizePath(path);
            Assembly assembly = typeof(LocalDashboardAssetService).Assembly;
            using (Stream? stream = assembly.GetManifestResourceStream("local-dashboard/" + normalized))
            {
                if (stream == null) return false;
                using (MemoryStream memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    byte[] bytes = memory.ToArray();
                    if (String.Equals(normalized, "index-local.html", StringComparison.OrdinalIgnoreCase))
                    {
                        string html = System.Text.Encoding.UTF8.GetString(bytes);
                        html = html.Replace("</head>", "<script>window.__XENO_LOCAL_TOKEN__='" + System.Net.WebUtility.HtmlEncode(token) + "';</script></head>", StringComparison.OrdinalIgnoreCase);
                        bytes = System.Text.Encoding.UTF8.GetBytes(html);
                    }

                    asset.Bytes = bytes;
                    asset.ContentType = ContentType(normalized);
                    asset.CacheControl = String.Equals(normalized, "index-local.html", StringComparison.OrdinalIgnoreCase) ? "no-store" : "public, max-age=31536000, immutable";
                    return true;
                }
            }
        }

        private static string NormalizePath(string path)
        {
            string normalized = (path ?? "/").TrimStart('/');
            if (String.IsNullOrWhiteSpace(normalized) || String.Equals(normalized, "index.html", StringComparison.OrdinalIgnoreCase)) return "index-local.html";
            return normalized.Replace('\\', '/');
        }

        private static string ContentType(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".html") return "text/html";
            if (extension == ".js") return "text/javascript";
            if (extension == ".css") return "text/css";
            if (extension == ".svg") return "image/svg+xml";
            if (extension == ".png") return "image/png";
            if (extension == ".ico") return "image/x-icon";
            return "application/octet-stream";
        }
    }
}
