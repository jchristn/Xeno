namespace Xeno.Core.Settings
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Enums;

    /// <summary>
    /// CORS settings.
    /// </summary>
    public class CorsSettings
    {
        /// <summary>
        /// Enable CORS.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Allowed origins.
        /// </summary>
        public List<string> AllowedOrigins { get; set; } = new List<string> { "http://localhost:9100" };

        /// <summary>
        /// Allowed headers.
        /// </summary>
        public List<string> AllowedHeaders { get; set; } = new List<string> { "Authorization", "Content-Type", "x-token", "x-tenant-guid", "x-tenant-id", "x-access-key", "x-secret-key" };

        /// <summary>
        /// Allowed methods.
        /// </summary>
        public List<string> AllowedMethods { get; set; } = new List<string> { "GET", "POST", "PUT", "DELETE", "OPTIONS" };
    }
}
