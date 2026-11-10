namespace Xeno.Client
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Text.Json.Serialization;
    using Xeno.Client.Services;
    using Xeno.Core.Models;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Client settings file model.
    /// </summary>
    public class ClientSettings
    {
        private static readonly object _FileLock = new object();
        private int _DefaultMaxConcurrentRequests = 4;

        /// <summary>
        /// Settings directory.
        /// </summary>
        public static string SettingsDirectory
        {
            get
            {
                string? overrideDirectory = Environment.GetEnvironmentVariable("XENO_CLIENT_HOME");
                if (!String.IsNullOrWhiteSpace(overrideDirectory)) return Path.GetFullPath(Environment.ExpandEnvironmentVariables(overrideDirectory));

                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".xeno");
            }
        }

        /// <summary>
        /// Settings file path.
        /// </summary>
        public static string SettingsFile
        {
            get
            {
                return Path.Combine(SettingsDirectory, "client.json");
            }
        }

        /// <summary>
        /// Client ID.
        /// </summary>
        public string? ClientId { get; set; } = null;

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; } = "ten_default";

        /// <summary>
        /// Client display name.
        /// </summary>
        public string Name { get; set; } = Environment.MachineName;

        /// <summary>
        /// Enable colored CLI output.
        /// </summary>
        public bool UseColors { get; set; } = true;

        /// <summary>
        /// Server settings.
        /// </summary>
        public ClientServerSettings Server { get; set; } = new ClientServerSettings();

        /// <summary>
        /// Local dashboard settings.
        /// </summary>
        public ClientLocalDashboardSettings LocalDashboard { get; set; } = new ClientLocalDashboardSettings();

        /// <summary>
        /// Local inference server settings.
        /// </summary>
        public ClientLocalInferenceServerSettings LocalInferenceServer { get; set; } = new ClientLocalInferenceServerSettings();

        /// <summary>
        /// Local telemetry settings.
        /// </summary>
        public ClientTelemetrySettings Telemetry { get; set; } = new ClientTelemetrySettings();

        /// <summary>
        /// Listener port this load migrated away from, when the stored port was the pre-0.2.0 default. Null when no migration happened during this load. Not persisted; the one-time notice is recorded in client state.
        /// </summary>
        [JsonIgnore]
        public int? LocalInferencePortMigratedFrom { get; private set; } = null;

        /// <summary>
        /// Default maximum concurrent requests per provider, used when a provider sets no limit. Shared by network requests and local listener requests. Default is 4. Minimum 1, maximum 256; out-of-range values are clamped.
        /// </summary>
        public int DefaultMaxConcurrentRequests
        {
            get
            {
                return _DefaultMaxConcurrentRequests;
            }
            set
            {
                _DefaultMaxConcurrentRequests = Math.Min(256, Math.Max(1, value));
            }
        }

        /// <summary>
        /// Provider endpoint settings.
        /// </summary>
        public List<ClientRunnerSettings> Providers { get; set; } = new List<ClientRunnerSettings>();

        /// <summary>
        /// Backward-compatible provider endpoint alias.
        /// </summary>
        [JsonIgnore]
        public List<ClientRunnerSettings> Runners
        {
            get
            {
                return Providers;
            }
            set
            {
                Providers = value ?? new List<ClientRunnerSettings>();
            }
        }

        /// <summary>
        /// Load settings from disk.
        /// </summary>
        /// <returns>Settings.</returns>
        public static ClientSettings Load()
        {
            // One load at a time in this process: a first load creates the file and a migrating load rewrites it, and
            // concurrent front-end refreshes would otherwise race each other on the same file.
            lock (_FileLock)
            {
                return LoadLocked();
            }
        }

        private static ClientSettings LoadLocked()
        {
            if (!File.Exists(SettingsFile))
            {
                ClientSettings created = new ClientSettings();
                created.Runners.Add(new ClientRunnerSettings());
                created.Save();
                return created;
            }

            string json = ClientFileIo.ReadAllText(SettingsFile);
            ClientSettings settings = JsonUtil.Deserialize<ClientSettings>(json) ?? new ClientSettings();
            ClientSettingsDefaultProbe probe = JsonUtil.Deserialize<ClientSettingsDefaultProbe>(json) ?? new ClientSettingsDefaultProbe();
            bool rewritten = settings.ApplyDefaults(probe);
            if (ServerTargets.UpgradeInsecureCloudUrls(settings)) rewritten = true;
            if (settings.Providers.Count == 0)
            {
                ClientSettingsLegacyRunners? legacy = JsonUtil.Deserialize<ClientSettingsLegacyRunners>(json);
                settings.Providers = legacy?.Runners ?? new List<ClientRunnerSettings>();
                rewritten = true;
            }

            if (rewritten) settings.Save();
            if (settings.LocalInferencePortMigratedFrom.HasValue)
            {
                ClientConnectionState.RecordLocalInferencePortMigration(settings.LocalInferencePortMigratedFrom.Value, settings.LocalInferenceServer.Port);
            }

            return settings;
        }

        /// <summary>
        /// Save settings to disk.
        /// </summary>
        public void Save()
        {
            string json = JsonUtil.Serialize(this, true);
            lock (_FileLock)
            {
                ClientFileIo.WriteAllTextAtomic(SettingsFile, json);
            }
        }

        /// <summary>
        /// Build advertised inventory. Providers with ShareWithNetwork set to false are left out.
        /// </summary>
        /// <returns>Inventory object.</returns>
        /// <exception cref="InvalidOperationException">A provider endpoint points at the enabled local inference listener.</exception>
        public object BuildInventory()
        {
            ValidateLocalInferenceProviderEndpoints();
            return new
            {
                os = Environment.OSVersion.ToString(),
                architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                machineName = Environment.MachineName,
                processorCount = Environment.ProcessorCount,
                totalAvailableMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
                clientVersion = "0.2.0",
                providers = Runners.FindAll(runner => runner != null && runner.ShareWithNetwork).ConvertAll(runner => new
                {
                    name = runner.Name,
                    apiType = runner.ApiType,
                    endpoint = runner.Endpoint,
                    modelManagementAllowed = IsOllamaProvider(runner.ApiType) && runner.AllowModelManagement,
                    timeoutMs = runner.TimeoutMs,
                    models = runner.Models.ConvertAll(model => new
                    {
                        name = model.Name,
                        supportsCompletions = model.SupportsCompletions,
                        supportsEmbeddings = model.SupportsEmbeddings
                    })
                })
            };
        }

        /// <summary>
        /// Build typed inventory protocol message. Providers with ShareWithNetwork set to false are left out.
        /// </summary>
        /// <returns>Inventory message.</returns>
        /// <exception cref="InvalidOperationException">A provider endpoint points at the enabled local inference listener.</exception>
        public ClientInventoryMessage BuildInventoryMessage()
        {
            ValidateLocalInferenceProviderEndpoints();
            ClientInventoryMessage inventory = new ClientInventoryMessage();
            foreach (ClientRunnerSettings runner in Runners)
            {
                if (runner == null || !runner.ShareWithNetwork) continue;
                ClientInventorySourceMessage source = new ClientInventorySourceMessage
                {
                    Name = runner.Name,
                    ApiType = runner.ApiType,
                    ModelManagementAllowed = IsOllamaProvider(runner.ApiType) && runner.AllowModelManagement
                };
                foreach (ClientModelSettings model in runner.Models)
                {
                    source.Models.Add(new ClientInventoryModelMessage
                    {
                        Name = model.Name,
                        SupportsCompletions = model.SupportsCompletions,
                        SupportsEmbeddings = model.SupportsEmbeddings
                    });
                }

                inventory.Providers.Add(source);
            }

            return inventory;
        }

        /// <summary>
        /// Validate that no advertised provider endpoint points at the enabled local inference listener.
        /// </summary>
        /// <exception cref="InvalidOperationException">A provider endpoint points at the enabled local inference listener.</exception>
        public void ValidateLocalInferenceProviderEndpoints()
        {
            ValidateLocalInferenceProviderEndpoints(LocalInferenceServer);
        }

        /// <summary>
        /// Validate that no advertised provider endpoint points at the supplied local inference listener settings.
        /// </summary>
        /// <param name="localInferenceServer">Local inference listener settings to validate against.</param>
        /// <exception cref="InvalidOperationException">A provider endpoint points at the enabled local inference listener.</exception>
        public void ValidateLocalInferenceProviderEndpoints(ClientLocalInferenceServerSettings localInferenceServer)
        {
            ArgumentNullException.ThrowIfNull(localInferenceServer);
            if (!localInferenceServer.Enabled) return;
            if (Providers == null) return;
            foreach (ClientRunnerSettings runner in Runners)
            {
                if (runner == null) continue;
                ValidateProviderEndpointAgainstLocalInference(runner.Name, runner.Endpoint, localInferenceServer);
            }
        }

        /// <summary>
        /// Validate one provider endpoint against the configured local inference listener.
        /// </summary>
        /// <param name="providerName">Provider endpoint name used in error messages.</param>
        /// <param name="endpoint">Provider endpoint URL.</param>
        /// <exception cref="InvalidOperationException">The provider endpoint points at the enabled local inference listener.</exception>
        public void ValidateProviderEndpointAgainstLocalInference(string providerName, string endpoint)
        {
            ValidateProviderEndpointAgainstLocalInference(providerName, endpoint, LocalInferenceServer);
        }

        /// <summary>
        /// Validate one provider endpoint against supplied local inference listener settings.
        /// </summary>
        /// <param name="providerName">Provider endpoint name used in error messages.</param>
        /// <param name="endpoint">Provider endpoint URL.</param>
        /// <param name="localInferenceServer">Local inference listener settings to validate against.</param>
        /// <exception cref="InvalidOperationException">The provider endpoint points at the enabled local inference listener.</exception>
        public void ValidateProviderEndpointAgainstLocalInference(string providerName, string endpoint, ClientLocalInferenceServerSettings localInferenceServer)
        {
            ArgumentNullException.ThrowIfNull(localInferenceServer);
            if (!ProviderEndpointMatchesLocalInference(endpoint, localInferenceServer)) return;

            string name = String.IsNullOrWhiteSpace(providerName) ? "(unnamed)" : providerName;
            string localEndpoint = LocalInferenceEndpoint(localInferenceServer);
            throw new InvalidOperationException("Provider endpoint '" + name + "' points at the local inference server " + localEndpoint + ". This would route Xeno requests back into this Xeno client and can create an infinite loop. Change the provider endpoint or LocalInferenceServer host/port before starting the client.");
        }

        private bool ApplyDefaults(ClientSettingsDefaultProbe probe)
        {
            bool rewritten = false;
            if (Server == null)
            {
                Server = new ClientServerSettings();
                rewritten = true;
            }

            if (LocalDashboard == null)
            {
                LocalDashboard = new ClientLocalDashboardSettings();
                rewritten = true;
            }

            if (LocalInferenceServer == null)
            {
                LocalInferenceServer = new ClientLocalInferenceServerSettings();
                rewritten = true;
            }

            if (Telemetry == null)
            {
                Telemetry = new ClientTelemetrySettings();
                rewritten = true;
            }

            if (Providers == null)
            {
                Providers = new List<ClientRunnerSettings>();
                rewritten = true;
            }

            if (probe.Server == null) rewritten = true;
            if (probe.LocalDashboard == null) rewritten = true;
            if (probe.LocalInferenceServer == null) rewritten = true;
            else
            {
                ClientLocalInferenceServerSettingsProbe inference = probe.LocalInferenceServer;
                if (!inference.RequestTimeoutMs.HasValue) rewritten = true;
                else if (inference.RequestTimeoutMs.Value == 120000)
                {
                    LocalInferenceServer.RequestTimeoutMs = 300000;
                    rewritten = true;
                }

                if (inference.Port.HasValue && inference.Port.Value == ClientLocalInferenceServerSettings.LegacyDefaultPort)
                {
                    LocalInferenceServer.Port = ClientLocalInferenceServerSettings.DefaultPort;
                    LocalInferencePortMigratedFrom = ClientLocalInferenceServerSettings.LegacyDefaultPort;
                    rewritten = true;
                }

                if (!inference.Port.HasValue) rewritten = true;
                if (String.IsNullOrWhiteSpace(inference.RoutingMode)) rewritten = true;
                if (inference.Routes == null) rewritten = true;
                if (!inference.DiscoverLocalModels.HasValue) rewritten = true;
                if (!inference.HealthCheckIntervalMs.HasValue) rewritten = true;
                if (!inference.LocalQueueTimeoutMs.HasValue) rewritten = true;
            }

            if (!probe.DefaultMaxConcurrentRequests.HasValue) rewritten = true;

            if (probe.Telemetry == null) rewritten = true;
            if (probe.Providers == null) rewritten = true;
            else if (HasOllamaProviderMissingModelManagement(probe.Providers)) rewritten = true;
            else if (HasProviderMissingRoutingFlags(probe.Providers)) rewritten = true;
            return rewritten;
        }

        private static bool HasOllamaProviderMissingModelManagement(List<ClientRunnerSettingsProbe> providers)
        {
            foreach (ClientRunnerSettingsProbe provider in providers)
            {
                string apiType = String.IsNullOrWhiteSpace(provider.ApiType) ? provider.Provider ?? String.Empty : provider.ApiType;
                if (IsOllamaProvider(apiType) && !provider.AllowModelManagement.HasValue) return true;
            }

            return false;
        }

        private static bool HasProviderMissingRoutingFlags(List<ClientRunnerSettingsProbe> providers)
        {
            foreach (ClientRunnerSettingsProbe provider in providers)
            {
                if (!provider.ShareWithNetwork.HasValue || !provider.RouteLocalRequests.HasValue) return true;
            }

            return false;
        }

        private static bool IsOllamaProvider(string? apiType)
        {
            return String.Equals(apiType, "Ollama", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ProviderEndpointMatchesLocalInference(string endpoint, ClientLocalInferenceServerSettings localInferenceServer)
        {
            if (!localInferenceServer.Enabled) return false;
            if (String.IsNullOrWhiteSpace(endpoint)) return false;
            if (!IsLoopbackAlias(localInferenceServer.Host)) return false;
            Uri? providerUri = TryBuildProviderEndpointUri(endpoint);
            if (providerUri == null) return false;
            if (!String.Equals(providerUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)) return false;
            if (providerUri.Port != localInferenceServer.Port) return false;
            return IsLoopbackAlias(providerUri.Host);
        }

        private static Uri? TryBuildProviderEndpointUri(string endpoint)
        {
            if (Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out Uri? uri)) return uri;
            return null;
        }

        private static bool IsLoopbackAlias(string host)
        {
            string normalized = NormalizeHost(host);
            if (String.Equals(normalized, "localhost", StringComparison.OrdinalIgnoreCase)) return true;
            if (IPAddress.TryParse(normalized, out IPAddress? address)) return IPAddress.IsLoopback(address);
            return false;
        }

        private static string NormalizeHost(string host)
        {
            string normalized = (host ?? String.Empty).Trim();
            if (normalized.StartsWith("[", StringComparison.Ordinal) && normalized.EndsWith("]", StringComparison.Ordinal) && normalized.Length > 2)
            {
                normalized = normalized.Substring(1, normalized.Length - 2);
            }

            return normalized;
        }

        private static string LocalInferenceEndpoint(ClientLocalInferenceServerSettings localInferenceServer)
        {
            string host = String.Equals(localInferenceServer.Host, "[::1]", StringComparison.OrdinalIgnoreCase) ? "[::1]" : localInferenceServer.Host;
            return "http://" + host + ":" + localInferenceServer.Port.ToString(CultureInfo.InvariantCulture);
        }
    }
}
