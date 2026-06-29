namespace Xeno.Sdk
{
    using System;
    using System.Text.Json;

    /// <summary>
    /// Xeno settings response DTO.
    /// </summary>
    public class SettingsResponse
    {
        /// <summary>
        /// Gets or sets the settings.
        /// </summary>
        public JsonElement Settings { get; set; }

        /// <summary>
        /// Gets or sets the settings file.
        /// </summary>
        public string SettingsFile { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the restart required.
        /// </summary>
        public bool RestartRequired { get; set; }
    }
}
