namespace Xeno.Sdk
{
    using System;

    /// <summary>
    /// Xeno settings response DTO.
    /// </summary>
    public class SettingsResponse
    {
        /// <summary>
        /// Gets or sets the settings.
        /// </summary>
        public RawJsonValue Settings { get; set; } = new RawJsonValue();

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
