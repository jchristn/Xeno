namespace Xeno.Client
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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
        /// Server settings.
        /// </summary>
        public ClientServerSettings Server { get; set; } = new ClientServerSettings();

        /// <summary>
        /// Runner settings.
        /// </summary>
        public List<ClientRunnerSettings> Runners { get; set; } = new List<ClientRunnerSettings>();

        /// <summary>
        /// Load settings from disk.
        /// </summary>
        /// <returns>Settings.</returns>
        public static ClientSettings Load()
        {
            if (!File.Exists(SettingsFile))
            {
                ClientSettings settings = new ClientSettings();
                settings.Runners.Add(new ClientRunnerSettings());
                settings.Save();
                return settings;
            }

            string json = File.ReadAllText(SettingsFile);
            return JsonUtil.Deserialize<ClientSettings>(json) ?? new ClientSettings();
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
                runners = Runners
            };
        }
    }
}
