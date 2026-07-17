namespace Xeno.Client
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json.Serialization;
    using Xeno.Core.Models;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Client settings file model.
    /// </summary>
    public class ClientSettings
    {
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
            if (!File.Exists(SettingsFile))
            {
                ClientSettings created = new ClientSettings();
                created.Runners.Add(new ClientRunnerSettings());
                created.Save();
                return created;
            }

            string json = File.ReadAllText(SettingsFile);
            ClientSettings settings = JsonUtil.Deserialize<ClientSettings>(json) ?? new ClientSettings();
            ClientSettingsDefaultProbe probe = JsonUtil.Deserialize<ClientSettingsDefaultProbe>(json) ?? new ClientSettingsDefaultProbe();
            bool rewritten = settings.ApplyDefaults(probe);
            if (settings.Providers.Count == 0)
            {
                ClientSettingsLegacyRunners? legacy = JsonUtil.Deserialize<ClientSettingsLegacyRunners>(json);
                settings.Providers = legacy?.Runners ?? new List<ClientRunnerSettings>();
                rewritten = true;
            }

            if (rewritten) settings.Save();
            return settings;
        }

        /// <summary>
        /// Save settings to disk.
        /// </summary>
        public void Save()
        {
            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllText(SettingsFile, JsonUtil.Serialize(this, true));
        }

        /// <summary>
        /// Build advertised inventory.
        /// </summary>
        /// <returns>Inventory object.</returns>
        public object BuildInventory()
        {
            return new
            {
                os = Environment.OSVersion.ToString(),
                architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                machineName = Environment.MachineName,
                processorCount = Environment.ProcessorCount,
                totalAvailableMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
                clientVersion = "0.1.0",
                providers = Runners.ConvertAll(runner => new
                {
                    name = runner.Name,
                    apiType = runner.ApiType,
                    endpoint = runner.Endpoint,
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
        /// Build typed inventory protocol message.
        /// </summary>
        /// <returns>Inventory message.</returns>
        public ClientInventoryMessage BuildInventoryMessage()
        {
            ClientInventoryMessage inventory = new ClientInventoryMessage();
            foreach (ClientRunnerSettings runner in Runners)
            {
                ClientInventorySourceMessage source = new ClientInventorySourceMessage
                {
                    Name = runner.Name,
                    ApiType = runner.ApiType
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
            if (probe.Telemetry == null) rewritten = true;
            if (probe.Providers == null) rewritten = true;
            return rewritten;
        }
    }
}
