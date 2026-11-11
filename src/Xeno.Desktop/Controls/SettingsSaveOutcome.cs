namespace Xeno.Desktop.Controls
{
    using System;
    using Xeno.Desktop.Common;

    /// <summary>
    /// Result of saving settings from a <see cref="SettingsEditor"/>.
    /// </summary>
    public sealed class SettingsSaveOutcome
    {
        /// <summary>
        /// True when the settings were written to client.json.
        /// </summary>
        public bool Saved { get; set; } = false;

        /// <summary>
        /// True when the change needs a client restart and the client is running.
        /// </summary>
        public bool RestartSuggested { get; set; } = false;

        /// <summary>
        /// Message for the user: the outcome, or the validation error. Never null.
        /// </summary>
        public string Message { get; set; } = String.Empty;

        /// <summary>
        /// Tone of <see cref="Message"/>.
        /// </summary>
        public XenoToneEnum Tone { get; set; } = XenoToneEnum.Info;
    }
}
