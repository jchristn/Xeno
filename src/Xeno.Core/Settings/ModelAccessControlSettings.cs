namespace Xeno.Core.Settings
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Enums;

    /// <summary>
    /// Model access control settings.
    /// </summary>
    public class ModelAccessControlSettings
    {
        /// <summary>
        /// Enable model access control.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Enforcement mode.
        /// </summary>
        public ModelAccessModeEnum Mode { get; set; } = ModelAccessModeEnum.Monitor;
    }
}
