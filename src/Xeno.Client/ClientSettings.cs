namespace Xeno.Client
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json.Serialization;
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
            if (settings.Providers.Count == 0)
            {
                ClientSettingsLegacyRunners? legacy = JsonUtil.Deserialize<ClientSettingsLegacyRunners>(json);
                settings.Providers = legacy?.Runners ?? new List<ClientRunnerSettings>();
            }

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
                    provider = runner.Provider,
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
    }
}
