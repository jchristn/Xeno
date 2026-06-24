namespace Xeno.Core.Settings
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Enums;

    /// <summary>
    /// REST listener settings.
    /// </summary>
    public class RestSettings
    {
        /// <summary>
        /// Hostname.
        /// </summary>
        public string Hostname { get; set; } = "0.0.0.0";

        /// <summary>
        /// Port.
        /// </summary>
        public int Port { get; set; } = 9000;

        /// <summary>
        /// Enable TLS.
        /// </summary>
        public bool Ssl { get; set; } = false;

        /// <summary>
        /// Require authentication for management and inference routes.
        /// </summary>
        public bool RequireAuthentication { get; set; } = false;
    }
}
