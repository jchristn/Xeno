namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Client.Services;

    /// <summary>
    /// The editable local settings, in display order, for the desktop app and terminal app settings editors. Applying
    /// values produces a <see cref="LocalClientSettingsUpdateRequest"/> that <see cref="LocalSettingsEditor"/> validates,
    /// the same request the local web dashboard sends.
    /// </summary>
    public static class XenoSettingsCatalog
    {
        #region Public-Members

        /// <summary>
        /// Section names in display order.
        /// </summary>
        public static IReadOnlyList<string> Sections
        {
            get { return Fields.Select(item => item.Section).Distinct(StringComparer.Ordinal).ToList(); }
        }

        /// <summary>
        /// Every editable setting.
        /// </summary>
        public static readonly IReadOnlyList<XenoSettingField> Fields = new List<XenoSettingField>
        {
            Toggle("dashboard.enabled", "Web dashboard", "Serve the local web dashboard", "The browser dashboard for this machine, served by the client while it runs.", r => r.Enabled, (r, v) => r.Enabled = v),
            Choice("dashboard.host", "Web dashboard", "Listen address", "127.0.0.1 keeps the dashboard on this machine; 0.0.0.0 exposes it to your network.", new List<string> { "127.0.0.1", "localhost", "::1", "[::1]", "0.0.0.0" }, r => r.Host, (r, v) => r.Host = v),
            Number("dashboard.port", "Web dashboard", "Port", "Default 9343.", 1, 65535, r => r.Port, (r, v) => r.Port = v),
            Toggle("dashboard.token", "Web dashboard", "Require the local access token", "Recommended. The token is injected into the page the client serves.", r => r.RequireLocalToken, (r, v) => r.RequireLocalToken = v),
            Toggle("dashboard.open", "Web dashboard", "Open the dashboard when the client starts", String.Empty, r => r.OpenBrowserOnStart, (r, v) => r.OpenBrowserOnStart = v),

            Toggle("inference.enabled", "Local inference server", "Serve OpenAI and Ollama APIs on this machine", "Local apps call this endpoint; requests are routed through the Xeno network.", r => Inference(r).Enabled, (r, v) => Inference(r).Enabled = v),
            Choice("inference.host", "Local inference server", "Listen address", "Loopback only.", new List<string> { "127.0.0.1", "localhost", "::1", "[::1]" }, r => Inference(r).Host, (r, v) => Inference(r).Host = v),
            Number("inference.port", "Local inference server", "Port", "Default 11434. Change it if Ollama already uses this port.", 1, 65535, r => Inference(r).Port, (r, v) => Inference(r).Port = v),
            Toggle("inference.cors", "Local inference server", "Allow browser apps (CORS)", String.Empty, r => Inference(r).EnableCors, (r, v) => Inference(r).EnableCors = v),
            Number("inference.timeout", "Local inference server", "Request timeout (ms)", String.Empty, 1000, Int32.MaxValue, r => Inference(r).RequestTimeoutMs, (r, v) => Inference(r).RequestTimeoutMs = v),
            Number("inference.maxBody", "Local inference server", "Maximum request body (bytes)", String.Empty, 1024, Int32.MaxValue, r => Inference(r).MaxRequestBodyBytes, (r, v) => Inference(r).MaxRequestBodyBytes = v),

            Number("history.maxEntries", "Request history", "Requests to keep", "Older requests are pruned. Default 1000.", 1, Int32.MaxValue, r => History(r).MaxEntries, (r, v) => History(r).MaxEntries = v, false),
            Toggle("history.bodies", "Request history", "Store request and response bodies", "Bodies can contain prompts and completions. Turn off to keep only metadata.", r => History(r).PersistBodies, (r, v) => History(r).PersistBodies = v, false),
            Number("history.maxBody", "Request history", "Maximum stored body size (bytes)", String.Empty, 0, Int32.MaxValue, r => History(r).MaxBodyBytes, (r, v) => History(r).MaxBodyBytes = v, false),

            Toggle("telemetry.enabled", "Telemetry", "Collect host telemetry", "CPU, memory, disk, and GPU readings stored on this machine.", r => Telemetry(r).Enabled, (r, v) => Telemetry(r).Enabled = v),
            Number("telemetry.interval", "Telemetry", "Host collection interval (ms)", String.Empty, 1000, Int32.MaxValue, r => Telemetry(r).CollectionIntervalMs, (r, v) => Telemetry(r).CollectionIntervalMs = v),
            Number("telemetry.retention", "Telemetry", "Retention (days)", String.Empty, 1, 3650, r => Telemetry(r).RetentionDays, (r, v) => Telemetry(r).RetentionDays = v),
            Toggle("telemetry.gpu", "Telemetry", "Collect GPU telemetry", String.Empty, r => Telemetry(r).Gpu.Enabled, (r, v) => Telemetry(r).Gpu.Enabled = v),
            Toggle("telemetry.providers", "Telemetry", "Collect provider telemetry", "Health, loaded models, and queue depth from local providers.", r => Telemetry(r).ProviderTelemetry.Enabled, (r, v) => Telemetry(r).ProviderTelemetry.Enabled = v),
            Number("telemetry.providerInterval", "Telemetry", "Provider collection interval (ms)", String.Empty, 1000, Int32.MaxValue, r => Telemetry(r).ProviderTelemetry.CollectionIntervalMs, (r, v) => Telemetry(r).ProviderTelemetry.CollectionIntervalMs = v)
        };

        #endregion

        #region Public-Methods

        /// <summary>
        /// Read every field's current value.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <returns>Values by key.</returns>
        /// <exception cref="ArgumentNullException">Settings is null.</exception>
        public static Dictionary<string, string> Read(ClientSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            LocalClientSettingsUpdateRequest request = LocalSettingsEditor.FromSettings(settings);
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (XenoSettingField field in Fields) values[field.Key] = field.Read(request);
            return values;
        }

        /// <summary>
        /// Validate values and build an update request from the current settings with the values applied.
        /// </summary>
        /// <param name="settings">Current client settings (loaded fresh; its nested objects are reused).</param>
        /// <param name="values">Values by key; missing keys keep their current values.</param>
        /// <returns>Update request.</returns>
        /// <exception cref="ArgumentNullException">Settings or values is null.</exception>
        /// <exception cref="ArgumentException">A value is not valid for its field.</exception>
        public static LocalClientSettingsUpdateRequest BuildRequest(ClientSettings settings, IReadOnlyDictionary<string, string> values)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(values);
            LocalClientSettingsUpdateRequest request = LocalSettingsEditor.FromSettings(settings);
            foreach (XenoSettingField field in Fields)
            {
                if (!values.TryGetValue(field.Key, out string? value)) continue;
                field.Write(request, Normalize(field, value ?? String.Empty));
            }

            return request;
        }

        /// <summary>
        /// Determine whether any changed value requires a client restart.
        /// </summary>
        /// <param name="before">Values before editing.</param>
        /// <param name="after">Values after editing.</param>
        /// <returns>True when a restart is required.</returns>
        public static bool RequiresRestart(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after)
        {
            ArgumentNullException.ThrowIfNull(before);
            ArgumentNullException.ThrowIfNull(after);
            foreach (XenoSettingField field in Fields)
            {
                if (!field.RequiresRestart) continue;
                before.TryGetValue(field.Key, out string? left);
                after.TryGetValue(field.Key, out string? right);
                if (!String.Equals(left, right, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        #endregion

        #region Private-Methods

        private static string Normalize(XenoSettingField field, string value)
        {
            string trimmed = value.Trim();
            switch (field.Kind)
            {
                case XenoSettingKindEnum.Toggle:
                    if (String.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase)) return "true";
                    if (String.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase)) return "false";
                    throw new ArgumentException(field.Label + " must be true or false.");
                case XenoSettingKindEnum.Number:
                    if (!Int32.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) || number < field.Minimum || number > field.Maximum)
                    {
                        string range = field.Maximum == Int32.MaxValue ? "at least " + field.Minimum : "from " + field.Minimum + " through " + field.Maximum;
                        throw new ArgumentException(field.Label + " must be a whole number " + range + ".");
                    }

                    return number.ToString(CultureInfo.InvariantCulture);
                case XenoSettingKindEnum.Choice:
                    string? choice = field.Choices.FirstOrDefault(item => String.Equals(item, trimmed, StringComparison.OrdinalIgnoreCase));
                    if (choice == null) throw new ArgumentException(field.Label + " must be one of: " + String.Join(", ", field.Choices) + ".");
                    return choice;
                default:
                    return trimmed;
            }
        }

        private static ClientLocalInferenceServerSettings Inference(LocalClientSettingsUpdateRequest request)
        {
            if (request.LocalInferenceServer == null) request.LocalInferenceServer = new ClientLocalInferenceServerSettings();
            return request.LocalInferenceServer;
        }

        private static ClientLocalHistorySettings History(LocalClientSettingsUpdateRequest request)
        {
            if (request.HistoryRetention == null) request.HistoryRetention = new ClientLocalHistorySettings();
            return request.HistoryRetention;
        }

        private static ClientTelemetrySettings Telemetry(LocalClientSettingsUpdateRequest request)
        {
            if (request.Telemetry == null) request.Telemetry = new ClientTelemetrySettings();
            return request.Telemetry;
        }

        private static XenoSettingField Toggle(string key, string section, string label, string help, Func<LocalClientSettingsUpdateRequest, bool> read, Action<LocalClientSettingsUpdateRequest, bool> write, bool restart = true)
        {
            return new XenoSettingField
            {
                Key = key,
                Section = section,
                Label = label,
                Help = help,
                Kind = XenoSettingKindEnum.Toggle,
                RequiresRestart = restart,
                Read = request => read(request) ? "true" : "false",
                Write = (request, value) => write(request, value == "true")
            };
        }

        private static XenoSettingField Number(string key, string section, string label, string help, int minimum, int maximum, Func<LocalClientSettingsUpdateRequest, int> read, Action<LocalClientSettingsUpdateRequest, int> write, bool restart = true)
        {
            return new XenoSettingField
            {
                Key = key,
                Section = section,
                Label = label,
                Help = help,
                Kind = XenoSettingKindEnum.Number,
                Minimum = minimum,
                Maximum = maximum,
                RequiresRestart = restart,
                Read = request => read(request).ToString(CultureInfo.InvariantCulture),
                Write = (request, value) => write(request, Int32.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture))
            };
        }

        private static XenoSettingField Choice(string key, string section, string label, string help, List<string> choices, Func<LocalClientSettingsUpdateRequest, string> read, Action<LocalClientSettingsUpdateRequest, string> write, bool restart = true)
        {
            return new XenoSettingField
            {
                Key = key,
                Section = section,
                Label = label,
                Help = help,
                Kind = XenoSettingKindEnum.Choice,
                Choices = choices,
                RequiresRestart = restart,
                Read = request => read(request),
                Write = (request, value) => write(request, value)
            };
        }

        #endregion
    }
}
