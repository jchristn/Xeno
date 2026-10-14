namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;
    using Xeno.Client.LocalDashboard.Models;

    /// <summary>
    /// One editable local setting, shared by the desktop app and the terminal app settings editors. Values are
    /// exchanged as strings; toggles use "true" and "false".
    /// </summary>
    public sealed class XenoSettingField
    {
        /// <summary>
        /// Stable key.
        /// </summary>
        public string Key { get; set; } = String.Empty;

        /// <summary>
        /// Section heading.
        /// </summary>
        public string Section { get; set; } = String.Empty;

        /// <summary>
        /// Label.
        /// </summary>
        public string Label { get; set; } = String.Empty;

        /// <summary>
        /// Help text.
        /// </summary>
        public string Help { get; set; } = String.Empty;

        /// <summary>
        /// Value kind.
        /// </summary>
        public XenoSettingKindEnum Kind { get; set; } = XenoSettingKindEnum.Text;

        /// <summary>
        /// Choices for <see cref="XenoSettingKindEnum.Choice"/>.
        /// </summary>
        public List<string> Choices { get; set; } = new List<string>();

        /// <summary>
        /// Minimum for numbers.
        /// </summary>
        public int Minimum { get; set; } = 0;

        /// <summary>
        /// Maximum for numbers.
        /// </summary>
        public int Maximum { get; set; } = Int32.MaxValue;

        /// <summary>
        /// True when changing the value requires restarting the client.
        /// </summary>
        public bool RequiresRestart { get; set; } = true;

        /// <summary>
        /// Reads the value from an update request.
        /// </summary>
        public Func<LocalClientSettingsUpdateRequest, string> Read { get; set; } = request => String.Empty;

        /// <summary>
        /// Writes a validated value into an update request.
        /// </summary>
        public Action<LocalClientSettingsUpdateRequest, string> Write { get; set; } = (request, value) => { };
    }
}
