namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using Xeno.Client.Hosting;
    using Xeno.Client.Services;

    /// <summary>
    /// About and diagnostics summary, as plain text or Markdown, shared by the desktop app, the terminal app, and the tray agent.
    /// Contains no secrets.
    /// </summary>
    public static class XenoDiagnostics
    {
        #region Public-Methods

        /// <summary>
        /// Build the diagnostics text.
        /// </summary>
        /// <param name="productName">Product name, for example "Xeno" or "Xeno Terminal".</param>
        /// <param name="snapshot">Status snapshot.</param>
        /// <returns>Text.</returns>
        /// <exception cref="ArgumentNullException">Snapshot is null.</exception>
        public static string Build(string productName, ClientStatusSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(productName + " " + XenoProduct.Version);
            builder.AppendLine(XenoProduct.Tagline);
            builder.AppendLine();
            foreach (KeyValuePair<string, string> pair in Fields(snapshot)) builder.AppendLine(pair.Key + ": " + pair.Value);
            builder.AppendLine();
            foreach (KeyValuePair<string, string> pair in Links()) builder.AppendLine(pair.Key + ": " + pair.Value);
            return builder.ToString();
        }

        /// <summary>
        /// Build the diagnostics as Markdown, for the terminal app's viewer: a heading, the fields as a table, and the
        /// links.
        /// </summary>
        /// <param name="productName">Product name, for example "Xeno" or "Xeno Terminal".</param>
        /// <param name="snapshot">Status snapshot.</param>
        /// <returns>Markdown.</returns>
        /// <exception cref="ArgumentNullException">Snapshot is null.</exception>
        public static string BuildMarkdown(string productName, ClientStatusSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            return XenoMarkdown.Join(new List<string?>
            {
                "# " + XenoMarkdown.Escape(productName + " " + XenoProduct.Version),
                "_" + XenoMarkdown.Escape(XenoProduct.Tagline) + "_",
                XenoMarkdown.Details(Fields(snapshot)),
                String.Join("\n", Links().Select(item => "- **" + XenoMarkdown.Escape(item.Key) + ":** <" + item.Value + ">"))
            });
        }

        #endregion

        #region Private-Methods

        private static List<KeyValuePair<string, string>> Fields(ClientStatusSnapshot snapshot)
        {
            return new List<KeyValuePair<string, string>>
            {
                Pair("Status", snapshot.StatusText + " (" + snapshot.StatusDetail + ")"),
                Pair("Client", DisplayFormat.OrDash(snapshot.Name) + " / " + DisplayFormat.OrDash(snapshot.ClientId)),
                Pair("Tenant", DisplayFormat.OrDash(snapshot.TenantId)),
                Pair("Server", DisplayFormat.OrDash(snapshot.RestBaseUrl) + " (" + DisplayFormat.OrDash(snapshot.WebSocketUrl) + ")"),
                Pair("Signed in", DisplayFormat.YesNo(snapshot.HasCredential)),
                Pair("Worker process", snapshot.ProcessId == null ? DisplayFormat.Dash : snapshot.ProcessId.Value.ToString(CultureInfo.InvariantCulture)),
                Pair("Providers", snapshot.ProviderCount + ", models: " + snapshot.ModelCount),
                Pair("Local dashboard", snapshot.LocalDashboardListening ? snapshot.LocalDashboardUrl : "not listening"),
                Pair("Local inference", snapshot.LocalInferenceEnabled ? (snapshot.LocalInferenceListening ? snapshot.LocalInferenceUrl : "enabled, not listening") : "disabled"),
                Pair("Settings", snapshot.SettingsFile),
                Pair("Log", snapshot.LogFile),
                Pair(".NET", RuntimeInformation.FrameworkDescription),
                Pair("OS", RuntimeInformation.OSDescription + " (" + RuntimeInformation.OSArchitecture + ")")
            };
        }

        private static List<KeyValuePair<string, string>> Links()
        {
            return new List<KeyValuePair<string, string>>
            {
                Pair("Documentation", XenoProduct.DocumentationUrl),
                Pair("Issues", XenoProduct.IssuesUrl),
                Pair("Community", XenoProduct.CommunityUrl)
            };
        }

        private static KeyValuePair<string, string> Pair(string key, string? value)
        {
            return new KeyValuePair<string, string>(key, value ?? String.Empty);
        }

        #endregion
    }
}
